namespace BgGame_Lib;

using System.Runtime.CompilerServices;
using BgDataTypes_Lib;

/// <summary>
/// <see cref="IProblemSetSource"/> decorator that builds a run's problem
/// stream from an inner source: a <see cref="QuizMix"/> selects which
/// decisions form the quiz, by lifetime performance, and a
/// <see cref="ProblemOrder"/> presents them (SPEC-filtering.md §5, "The mix
/// selects; the order presents", and §7). The order is applied exactly once,
/// and which application it gets is decided here, never by the consumer: by
/// the plain key when the mix is blank, by the mixed key when it is active.
///
/// <para>
/// <b>Blank mix: the order alone.</b> When the mix
/// <see cref="QuizMix.IsPassthrough"/>, nothing is composed: the stream is
/// the order's plain application (<see cref="OrderedProblemSetSource"/>) over
/// the inner source — every decision, no dedupe, the by-error key XG's error;
/// as read is fully inert. The stats provider is never called (none is
/// needed), <see cref="Count"/> passes through, and
/// <see cref="LastComposition"/> is null.
/// </para>
///
/// <para>
/// <b>Active mix: classification is by content identity.</b> Each decision's
/// lifetime record is looked up by its derived <see cref="ProblemKey"/>
/// (<see cref="ProblemKey.From"/>, which every record has;
/// SPEC-stats-identity.md §4; halheinrich/backgammon#95), so the classifier
/// judges the <i>problem's</i> full record wherever the drawn copy came from
/// — a copy under a different <c>DecisionId</c> than the one once quizzed
/// still classifies as GotWrong / seen.
/// </para>
///
/// <para>
/// <b>Composition per enumeration.</b> An active mix materializes the inner
/// source (the <see cref="OrderedProblemSetSource"/> tradeoff — composition
/// needs the whole set), deduping by <see cref="BgDecisionData.Id"/> (first
/// occurrence wins: a quiz presents each decision at most once). One stats
/// document and one timestamp are resolved per pass, so every predicate sees
/// the same "now". Each decision joins the pool of every non-residual entry
/// whose category matches; decisions matched by no non-residual entry form
/// the residual pool, reachable only when an
/// <see cref="QuizCategory.EverythingElse"/> entry is selected — otherwise
/// they are simply not drawn. The target count is the mix's
/// <see cref="QuizMix.QuizLength"/> (or the deduped union of the selected
/// pools when unset), apportioned across entries by percent via largest
/// remainder; shortfall from dry pools is redistributed proportionally
/// across the entries that still have supply. The result:
/// <see cref="MixComposition.DrawnCount"/> decisions, each drawn once.
/// </para>
///
/// <para>
/// <b>Composition is paced, and leaves nothing behind.</b> It runs inside a
/// run's prepare, beside the current run's own parse, so its per-decision
/// passes — classification, and the order's key pass, which reads each
/// reachable decision's key once — yield on the cooperative time budget
/// (<see cref="CooperativeYielder"/>) and honour cancellation, as the
/// presentation does; the sorts and draws stay synchronous. What it builds
/// per decision, and the stats document it read, are released once it is
/// done: an enumeration keeps the drawn decisions and the telemetry.
/// </para>
///
/// <para>
/// <b>The mix selects; the order presents.</b> Each row's eligible set is
/// put in the order and the row's quota is filled from its front, an already
/// drawn decision skipped without consuming quota; then the whole selection
/// is sorted again in the same order. So <see cref="ProblemOrder.Shuffled"/>
/// draws each row uniformly without replacement and shuffles the
/// presentation, and <see cref="ProblemOrder.AsRead"/> draws and presents in
/// source order with no randomness consumed at all. The by-error key here is
/// the player's lifetime loss per answer where the decision's record — the
/// one the composition classified against — holds submissions, and XG's
/// error otherwise; a decision with neither counts as 0.0
/// (<see cref="ProblemOrder"/> states every order's key).
/// </para>
///
/// <para>
/// <b>Entry order is contractual.</b> Entries draw in the mix's declared
/// order, so when categories overlap, contested decisions go to the earlier
/// entry — reordering entries changes the composition. Consumers rendering
/// or persisting entry lists must preserve the order.
/// </para>
///
/// <para>
/// <b>Seeded form.</b> The seeded constructor fixes every draw and shuffle
/// this instance makes — the blank mix's plain order included — mirroring
/// <see cref="OrderedProblemSetSource"/> (stable within a .NET runtime
/// version, not audit-grade). Only <see cref="ProblemOrder.Shuffled"/>
/// consumes the generator.
/// </para>
///
/// <para>
/// <b>Stats provider seam and Restart.</b> The decorator takes a
/// current-document getter, resolved fresh at each composing enumeration: the
/// stats document is immutable and replaced fold-by-fold, so a quiz
/// controller's Restart re-composes against the lifetime record as it stands
/// — including decisions answered this session. With no stats document there
/// is no provider to give, and a blank mix needs none; an active mix without
/// one is a consumer error, refused at construction. A provider that returns
/// null is a wiring bug too and throws <see cref="InvalidOperationException"/>
/// rather than silently composing as if nothing had ever been quizzed. (An
/// <i>empty document</i> is the legitimate everything-never-seen input.)
/// Re-iterability follows: each call composes and orders afresh; do not
/// assume one enumeration's contents or order carry to the next.
/// </para>
///
/// <para>
/// <b>Telemetry.</b> <see cref="LastComposition"/> carries the most recent
/// enumeration's requested-vs-actual composition, assigned before the first
/// decision is yielded so the consumer can render an honest shortfall notice
/// at quiz start.
/// </para>
///
/// Not thread-safe — <see cref="Random"/> is not, and
/// <see cref="LastComposition"/> is per-instance state written by each
/// enumeration.
/// </summary>
public sealed class MixedProblemSetSource : IProblemSetSource
{
    private readonly IProblemSetSource _inner;
    private readonly ProblemOrder _order;
    private readonly PlayRanking _ranking;
    private readonly QuizMix _mix;
    private readonly Func<ProblemStatsDocument>? _statsProvider;
    private readonly TimeProvider _clock;
    private readonly Random _random;

    /// <summary>The order's plain application, for a blank mix; <see langword="null"/> when the mix is active.</summary>
    private readonly OrderedProblemSetSource? _plain;

    private MixedProblemSetSource(
        IProblemSetSource inner,
        ProblemOrder order,
        PlayRanking ranking,
        QuizMix mix,
        Func<ProblemStatsDocument>? statsProvider,
        TimeProvider clock,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(mix);
        ArgumentNullException.ThrowIfNull(clock);
        ProblemOrdering.RequireDefined(order, ranking);
        if (statsProvider is null && !mix.IsPassthrough)
            throw new ArgumentNullException(
                nameof(statsProvider),
                "An active mix composes against the current stats document, so it needs a stats " +
                "provider; with no stats document a mix cannot apply. A blank mix needs none.");
        _inner = inner;
        _order = order;
        _ranking = ranking;
        _mix = mix;
        _statsProvider = statsProvider;
        _clock = clock;
        _random = random;
        _plain = mix.IsPassthrough ? new OrderedProblemSetSource(inner, order, ranking, random) : null;
    }

    /// <summary>
    /// Construct a run's source over <paramref name="inner"/> whose shuffles
    /// and draws are seeded from a non-deterministic source — for production
    /// use where reproducibility isn't required.
    /// </summary>
    /// <param name="inner">The source whose decisions are composed and ordered.</param>
    /// <param name="order">The order the run presents its problems in.</param>
    /// <param name="ranking">
    /// The run's ranking: the by-error order reads XG's error under it where
    /// no lifetime record supplies the key. Always a defined ranking.
    /// </param>
    /// <param name="mix">The composition config; a blank mix composes nothing.</param>
    /// <param name="statsProvider">
    /// Getter for the consumer's <b>current</b> stats document, resolved
    /// fresh at each composing enumeration (see the type remarks on Restart),
    /// and never returning <see langword="null"/>; or <see langword="null"/>
    /// when there is no stats document, which only a blank mix allows.
    /// </param>
    /// <param name="clock">
    /// Source of the per-enumeration classification timestamp (its
    /// <see cref="TimeProvider.GetUtcNow"/>) — the clock seam for
    /// <see cref="QuizCategory.NotSeenInDays"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="inner"/>, <paramref name="mix"/> or
    /// <paramref name="clock"/> is null, or <paramref name="statsProvider"/>
    /// is null while <paramref name="mix"/> is active.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="order"/> or <paramref name="ranking"/> is not a defined member.
    /// </exception>
    public MixedProblemSetSource(
        IProblemSetSource inner,
        ProblemOrder order,
        PlayRanking ranking,
        QuizMix mix,
        Func<ProblemStatsDocument>? statsProvider,
        TimeProvider clock)
        : this(inner, order, ranking, mix, statsProvider, clock, new Random())
    {
    }

    /// <summary>
    /// Construct a run's source whose draws and shuffles are determined by
    /// <paramref name="seed"/> — for deterministic tests and replayable
    /// sessions. (Under <see cref="ProblemOrder.Shuffled"/> only; every other
    /// order is deterministic either way.)
    /// </summary>
    /// <param name="inner">The source whose decisions are composed and ordered.</param>
    /// <param name="order">The order the run presents its problems in.</param>
    /// <param name="ranking">The run's ranking; the by-error order's fallback reads XG's error under it.</param>
    /// <param name="mix">The composition config; a blank mix composes nothing.</param>
    /// <param name="statsProvider">
    /// Getter for the consumer's <b>current</b> stats document, never
    /// returning <see langword="null"/>; or <see langword="null"/> when there
    /// is no stats document, which only a blank mix allows.
    /// </param>
    /// <param name="clock">Source of the per-enumeration classification timestamp.</param>
    /// <param name="seed">Fixes the draw and shuffle sequence.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="inner"/>, <paramref name="mix"/> or
    /// <paramref name="clock"/> is null, or <paramref name="statsProvider"/>
    /// is null while <paramref name="mix"/> is active.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="order"/> or <paramref name="ranking"/> is not a defined member.
    /// </exception>
    public MixedProblemSetSource(
        IProblemSetSource inner,
        ProblemOrder order,
        PlayRanking ranking,
        QuizMix mix,
        Func<ProblemStatsDocument>? statsProvider,
        TimeProvider clock,
        int seed)
        : this(inner, order, ranking, mix, statsProvider, clock, new Random(seed))
    {
    }

    /// <inheritdoc/>
    public string Name => _inner.Name;

    /// <summary>
    /// Passthrough from the inner source when the mix is blank; otherwise
    /// <see langword="null"/> — a composition's size depends on the stats
    /// document and clock at enumeration time, so it cannot be promised up
    /// front. Read <see cref="MixComposition.DrawnCount"/> off
    /// <see cref="LastComposition"/> instead.
    /// </summary>
    public int? Count => _plain?.Count;

    /// <summary>
    /// Telemetry from the most recent enumeration: requested vs. actual,
    /// overall and per entry. Null before the first enumeration and after a
    /// blank-mix enumeration. Assigned before the first decision is yielded,
    /// once the composition is complete — a composition cancelled part-way
    /// assigns nothing. Per-instance state — see the thread-safety note in the
    /// type remarks.
    /// </summary>
    public MixComposition? LastComposition { get; private set; }

    /// <inheritdoc/>
    public async IAsyncEnumerable<BgDecisionData> EnumerateAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_plain is not null)
        {
            LastComposition = null;
            await foreach (var item in _plain.EnumerateAsync(cancellationToken))
                yield return item;
            yield break;
        }

        // Cooperative yielding is time-budgeted, not per-item (see
        // CooperativeYielder). One yielder paces the whole path: the
        // classification and key passes inside the composition, then the
        // presentation. The pacing clock is deliberately TimeProvider.System,
        // not _clock: _clock is the semantic classification timestamp and reusing
        // it here would couple yield pacing to a fixed test clock.
        var yielder = new CooperativeYielder(TimeProvider.System);
        var (drawn, composition) = await ComposeAsync(yielder, cancellationToken);
        LastComposition = composition;

        foreach (var item in drawn)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
            await yielder.YieldIfDueAsync();
        }
    }

    /// <summary>
    /// Materialize the inner source (composition needs the whole set),
    /// deduping by record identity (<see cref="DecisionId"/>) — first
    /// occurrence wins; a quiz presents each record at most once. Deliberately
    /// not the content key: this guard is about the same record streaming
    /// through twice, while content dedupe (<see cref="ProblemKey"/>) is
    /// <see cref="DistinctPositionProblemSetSource"/>'s concern and the
    /// consumer's wiring choice. A method of its own so the identity set is
    /// released before composition begins.
    /// </summary>
    private async ValueTask<List<BgDecisionData>> MaterializeAsync(CancellationToken cancellationToken)
    {
        var items = new List<BgDecisionData>();
        var seenIds = new HashSet<DecisionId>();
        await foreach (var item in _inner.EnumerateAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (seenIds.Add(item.Id))
                items.Add(item);
        }
        return items;
    }

    /// <summary>
    /// One composition pass: resolve the stats document and the timestamp,
    /// materialize, classify into per-entry pools, put each pool in the
    /// order, apportion the target by percent, fill each entry's quota from
    /// the front of its pool with the global dedupe (declared entry order —
    /// contested overlap goes to the earlier entry), redistribute shortfall
    /// proportionally across entries with remaining supply, and put the
    /// selection in the order again for presentation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Paced and cancellable.</b> The per-decision passes — classification,
    /// and the order's key pass (<see cref="ProblemOrdering"/>) — yield on
    /// <paramref name="yielder"/>'s time budget and observe
    /// <paramref name="cancellationToken"/> decision by decision; the sorts and
    /// the draws stay synchronous. A composition cancelled part-way assigns no
    /// telemetry.
    /// </para>
    /// <para>
    /// <b>What it leaves behind.</b> Everything it builds per decision — the
    /// materialized list, the records, the pools, the keys — and the stats
    /// document it read are its own locals, released when it returns: the
    /// enumeration keeps the drawn decisions and the telemetry, and nothing
    /// else.
    /// </para>
    /// </remarks>
    private async ValueTask<(List<BgDecisionData> Drawn, MixComposition Composition)> ComposeAsync(
        CooperativeYielder yielder, CancellationToken cancellationToken)
    {
        // An active mix was given a provider at construction.
        var stats = _statsProvider!()
            ?? throw new InvalidOperationException(
                "The stats provider returned null. A MixedProblemSetSource with an active mix " +
                "requires the current ProblemStatsDocument (an empty document is valid — it " +
                "means nothing has been quizzed); when there is no stats document at all, pass " +
                "no provider, which only a blank mix allows.");
        var now = _clock.GetUtcNow();

        var items = await MaterializeAsync(cancellationToken);

        var entries = _mix.Entries;
        int n = entries.Count;

        // Classify into pools (source order). The residual pool collects
        // decisions matched by no non-residual entry. Each decision's record
        // is kept: the by-error order reads the same records the
        // classification judged.
        var pools = new List<int>[n];
        var predicates = new IQuizCategoryPredicate?[n];
        int residualIndex = -1;
        for (int i = 0; i < n; i++)
        {
            pools[i] = [];
            if (entries[i].Category.IsResidual)
                residualIndex = i;
            else
                predicates[i] = entries[i].Category.BuildPredicate();
        }

        var records = new ProblemStats?[items.Count];
        for (int idx = 0; idx < items.Count; idx++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Lifetime record by content identity; none means never quizzed.
            stats.Problems.TryGetValue(ProblemKey.From(items[idx]), out var record);
            records[idx] = record;
            bool matchedAny = false;
            for (int i = 0; i < n; i++)
            {
                if (predicates[i] is { } predicate && predicate.Matches(items[idx], record, now))
                {
                    pools[i].Add(idx);
                    matchedAny = true;
                }
            }
            if (!matchedAny && residualIndex >= 0)
                pools[residualIndex].Add(idx);

            await yielder.YieldIfDueAsync();
        }

        // The deduped union of the selected pools bounds what any draw can reach.
        var reachable = new bool[items.Count];
        int unionCount = 0;
        foreach (var pool in pools)
        {
            foreach (var idx in pool)
            {
                if (!reachable[idx])
                {
                    reachable[idx] = true;
                    unionCount++;
                }
            }
        }

        // The reachable positions, in source order: the only ones the order
        // ever arranges, so the only ones whose key is read.
        var reachablePositions = new List<int>(unionCount);
        for (int idx = 0; idx < items.Count; idx++)
        {
            if (reachable[idx])
                reachablePositions.Add(idx);
        }

        // The capped/capless split, read once and reported once
        // (halheinrich/backgammon#12): the target is the requested length
        // exactly when the mix set one, and MixComposition carries which —
        // the counts cannot say, since a capless target *is* unionCount and a
        // capped one may ask for that same number.
        int? requestedLength = _mix.QuizLength;
        int target = requestedLength ?? unionCount;
        int drawGoal = Math.Min(target, unionCount);

        var percents = new int[n];
        for (int i = 0; i < n; i++)
            percents[i] = entries[i].Percent;
        var requested = Apportion(target, percents);

        // Each row's eligible set, put in the order: its quota is filled from
        // the front. Shuffled makes each fill a uniform draw without
        // replacement; as read leaves the pools in source order. The keys are
        // read once here, for the reachable decisions, paced and cancellable;
        // the sorts are synchronous.
        var ordering = await ProblemOrdering.OverAsync(
            items, reachablePositions, _order, _ranking, records, _random, yielder, cancellationToken);
        foreach (var pool in pools)
            ordering.Arrange(pool);

        // Draw state: a cursor per entry through its ordered pool. An already
        // drawn decision is skipped without consuming quota.
        var drawnFlags = new bool[items.Count];
        var drawnPerEntry = new int[n];
        int totalDrawn = 0;
        var cursors = new int[n];

        void DrawFrom(int i, int quota)
        {
            var pool = pools[i];
            while (quota > 0 && cursors[i] < pool.Count)
            {
                int idx = pool[cursors[i]++];
                if (!drawnFlags[idx])
                {
                    drawnFlags[idx] = true;
                    drawnPerEntry[i]++;
                    totalDrawn++;
                    quota--;
                }
            }
        }

        bool HasSupply(int i)
        {
            var pool = pools[i];
            for (int c = cursors[i]; c < pool.Count; c++)
            {
                if (!drawnFlags[pool[c]])
                    return true;
            }
            return false;
        }

        // First pass: declared entry order — contested overlap goes to the
        // earlier entry.
        for (int i = 0; i < n; i++)
            DrawFrom(i, requested[i]);

        // Shortfall redistribution: share the deficit proportionally (by
        // percent, largest remainder) across the entries that still have
        // supply. Terminates: drawGoal ≤ union guarantees supply exists, and
        // the first eligible entry given a nonzero share draws at least one.
        while (totalDrawn < drawGoal)
        {
            var eligible = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (HasSupply(i))
                    eligible.Add(i);
            }
            if (eligible.Count == 0)
                break;   // unreachable (drawGoal ≤ union); defensive against an invariant break

            var extra = Apportion(
                drawGoal - totalDrawn,
                eligible.ConvertAll(i => percents[i]).ToArray());
            for (int k = 0; k < eligible.Count; k++)
                DrawFrom(eligible[k], extra[k]);
        }

        // Presentation: the whole selection, put in the order again.
        var selection = new List<int>(totalDrawn);
        for (int idx = 0; idx < items.Count; idx++)
        {
            if (drawnFlags[idx])
                selection.Add(idx);
        }
        ordering.Arrange(selection);
        var result = selection.ConvertAll(idx => items[idx]);

        var entryReports = new MixCompositionEntry[n];
        for (int i = 0; i < n; i++)
        {
            entryReports[i] = new MixCompositionEntry(
                entries[i].Category, entries[i].Percent,
                PoolSize: pools[i].Count,
                Requested: requested[i],
                Drawn: drawnPerEntry[i]);
        }
        var composition = new MixComposition(
            target,
            HasRequestedLength: requestedLength is not null,
            totalDrawn,
            entryReports);

        return (result, composition);
    }

    /// <summary>
    /// Largest-remainder apportionment of <paramref name="total"/> units
    /// across <paramref name="weights"/>: floor each proportional share, then
    /// hand the leftover units to the largest fractional remainders, ties to
    /// the earlier entry. The returned counts always sum to
    /// <paramref name="total"/>.
    /// </summary>
    private static int[] Apportion(int total, int[] weights)
    {
        var counts = new int[weights.Length];
        if (total == 0 || weights.Length == 0)
            return counts;

        long weightSum = 0;
        foreach (var weight in weights)
            weightSum += weight;

        var remainders = new long[weights.Length];
        int assigned = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            long share = (long)total * weights[i];
            counts[i] = (int)(share / weightSum);
            remainders[i] = share % weightSum;
            assigned += counts[i];
        }

        var order = Enumerable.Range(0, weights.Length)
            .OrderByDescending(i => remainders[i])
            .ThenBy(i => i)
            .ToArray();
        for (int k = 0; k < total - assigned; k++)
            counts[order[k]]++;

        return counts;
    }
}
