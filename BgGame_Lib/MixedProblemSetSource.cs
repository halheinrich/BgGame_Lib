namespace BgGame_Lib;

using System.Runtime.CompilerServices;
using BgDataTypes_Lib;

/// <summary>
/// <see cref="IProblemSetSource"/> decorator that composes a quiz from an
/// inner source by lifetime performance: each enumeration classifies the
/// inner decisions against a <see cref="QuizMix"/>'s categories (using the
/// consumer's current <see cref="ProblemStatsDocument"/>) and draws
/// per-entry pools to the percentage composition. Composition, not ordering:
/// the mix decides <i>which</i> decisions form the quiz.
///
/// <para>
/// <b>Classification is by content identity.</b> Each decision's lifetime
/// record is looked up by its derived <see cref="ProblemKey"/>
/// (SPEC-stats-identity.md §4; halheinrich/backgammon#95), so the classifier
/// judges the <i>problem's</i> full record wherever the drawn copy came from
/// — a copy under a different <c>DecisionId</c> than the one once quizzed
/// still classifies as GotWrong / seen. A decision whose key is underivable
/// (the no-key rung) has no record by construction — such submissions are
/// never recorded — and classifies as never-seen.
/// </para>
///
/// <para>
/// <b>Blank mix = passthrough.</b> When the mix
/// <see cref="QuizMix.IsPassthrough"/>, the layer is fully inert: the inner
/// source streams through unchanged (no materialization, no RNG consumption,
/// no dedupe), <see cref="Count"/> passes through, and
/// <see cref="LastComposition"/> is null.
/// </para>
///
/// <para>
/// <b>Composition per enumeration.</b> An active mix materializes the inner
/// source (the <see cref="ShuffledProblemSetSource"/> tradeoff — composition
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
/// <b>Entry order is contractual.</b> Entries draw in the mix's declared
/// order, so when categories overlap, contested decisions go to the earlier
/// entry — reordering entries changes the composition. Consumers rendering
/// or persisting entry lists must preserve the order.
/// </para>
///
/// <para>
/// <b>The random toggle.</b> <see cref="QuizMix.RandomOrder"/> on (the
/// default): draws are uniform without replacement and the drawn set is
/// shuffled for presentation. Off: fully deterministic — pools are drawn in
/// source order and presented in source order, with no RNG dependence at
/// all. The seeded constructor fixes the random-mode sequence for
/// deterministic tests, mirroring <see cref="ShuffledProblemSetSource"/>
/// (stable within a .NET runtime version, not audit-grade).
/// </para>
///
/// <para>
/// <b>Stats provider seam and Restart.</b> The decorator takes a
/// current-document getter, resolved fresh at each enumeration: the stats
/// document is immutable and replaced fold-by-fold, so a quiz controller's
/// Restart re-composes against the lifetime record as it stands — including
/// decisions answered this session. The provider must never return null;
/// the no-stats case is "don't wire this decorator at all", so a null return
/// is a consumer wiring bug and throws <see cref="InvalidOperationException"/>
/// rather than silently composing as if nothing had ever been quizzed. (An
/// <i>empty document</i> is the legitimate everything-never-seen input.)
/// Re-iterability follows: each call composes and (in random mode) shuffles
/// afresh; do not assume one enumeration's contents or order carry to the
/// next.
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
    private readonly Func<ProblemStatsDocument> _statsProvider;
    private readonly QuizMix _mix;
    private readonly TimeProvider _clock;
    private readonly Random _random;

    private MixedProblemSetSource(
        IProblemSetSource inner,
        Func<ProblemStatsDocument> statsProvider,
        QuizMix mix,
        TimeProvider clock,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(statsProvider);
        ArgumentNullException.ThrowIfNull(mix);
        ArgumentNullException.ThrowIfNull(clock);
        _inner = inner;
        _statsProvider = statsProvider;
        _mix = mix;
        _clock = clock;
        _random = random;
    }

    /// <summary>
    /// Construct a composing view over <paramref name="inner"/> seeded from a
    /// non-deterministic source — for production use where reproducibility
    /// isn't required.
    /// </summary>
    /// <param name="inner">The source whose decisions are composed.</param>
    /// <param name="statsProvider">
    /// Getter for the consumer's <b>current</b> stats document, resolved
    /// fresh at each enumeration (see the type remarks on Restart). Must
    /// never return <see langword="null"/>.
    /// </param>
    /// <param name="mix">The composition config.</param>
    /// <param name="clock">
    /// Source of the per-enumeration classification timestamp (its
    /// <see cref="TimeProvider.GetUtcNow"/>) — the clock seam for
    /// <see cref="QuizCategory.NotSeenInDays"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public MixedProblemSetSource(
        IProblemSetSource inner,
        Func<ProblemStatsDocument> statsProvider,
        QuizMix mix,
        TimeProvider clock)
        : this(inner, statsProvider, mix, clock, new Random())
    {
    }

    /// <summary>
    /// Construct a composing view whose random-mode draw/shuffle sequence is
    /// determined by <paramref name="seed"/> — for deterministic tests and
    /// replayable sessions. (With <see cref="QuizMix.RandomOrder"/> off the
    /// seed is irrelevant: composition is fully deterministic either way.)
    /// </summary>
    /// <param name="inner">The source whose decisions are composed.</param>
    /// <param name="statsProvider">
    /// Getter for the consumer's <b>current</b> stats document, resolved
    /// fresh at each enumeration. Must never return <see langword="null"/>.
    /// </param>
    /// <param name="mix">The composition config.</param>
    /// <param name="clock">Source of the per-enumeration classification timestamp.</param>
    /// <param name="seed">Fixes the draw/shuffle sequence.</param>
    /// <exception cref="ArgumentNullException">Any reference argument is null.</exception>
    public MixedProblemSetSource(
        IProblemSetSource inner,
        Func<ProblemStatsDocument> statsProvider,
        QuizMix mix,
        TimeProvider clock,
        int seed)
        : this(inner, statsProvider, mix, clock, new Random(seed))
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
    public int? Count => _mix.IsPassthrough ? _inner.Count : null;

    /// <summary>
    /// Telemetry from the most recent enumeration: requested vs. actual,
    /// overall and per entry. Null before the first enumeration and after a
    /// passthrough (blank-mix) enumeration. Assigned before the first
    /// decision is yielded. Per-instance state — see the thread-safety note
    /// in the type remarks.
    /// </summary>
    public MixComposition? LastComposition { get; private set; }

    /// <inheritdoc/>
    public async IAsyncEnumerable<BgDecisionData> EnumerateAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_mix.IsPassthrough)
        {
            LastComposition = null;
            await foreach (var item in _inner.EnumerateAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
            }
            yield break;
        }

        var stats = _statsProvider()
            ?? throw new InvalidOperationException(
                "The stats provider returned null. A MixedProblemSetSource requires the " +
                "current ProblemStatsDocument (an empty document is valid — it means " +
                "nothing has been quizzed); when there is no stats document at all, do not " +
                "wire the composing decorator.");
        var now = _clock.GetUtcNow();

        // Materialize (composition needs the whole set), deduping by record
        // identity (DecisionId) — first occurrence wins; a quiz presents each
        // record at most once. Deliberately not the content key: this guard is
        // about the same record streaming through twice, while content dedupe
        // (ProblemKey) is DistinctPositionProblemSetSource's concern and the
        // consumer's wiring choice.
        var items = new List<BgDecisionData>();
        var seenIds = new HashSet<DecisionId>();
        await foreach (var item in _inner.EnumerateAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (seenIds.Add(item.Id))
                items.Add(item);
        }

        var drawn = Compose(items, stats, now, out var composition);
        LastComposition = composition;

        // Cooperative yielding is time-budgeted, not per-item (see
        // CooperativeYielder). The pacing clock is deliberately TimeProvider.System,
        // not _clock: _clock is the semantic classification timestamp and reusing
        // it here would couple yield pacing to a fixed test clock.
        var yielder = new CooperativeYielder(TimeProvider.System);
        foreach (var item in drawn)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
            await yielder.YieldIfDueAsync();
        }
    }

    /// <summary>
    /// One composition pass: classify into per-entry pools, apportion the
    /// target by percent, draw with the global dedupe (declared entry order —
    /// contested overlap goes to the earlier entry), redistribute shortfall
    /// proportionally across entries with remaining supply, and order the
    /// result for presentation.
    /// </summary>
    private List<BgDecisionData> Compose(
        List<BgDecisionData> items,
        ProblemStatsDocument stats,
        DateTimeOffset now,
        out MixComposition composition)
    {
        var entries = _mix.Entries;
        int n = entries.Count;

        // Classify into pools (source order). The residual pool collects
        // decisions matched by no non-residual entry.
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

        for (int idx = 0; idx < items.Count; idx++)
        {
            // Lifetime record by content identity; no derivable key (the
            // no-key rung) means no record — such submissions are never
            // recorded — so the decision classifies as never-seen.
            ProblemStats? record = null;
            if (ProblemKey.TryDerive(items[idx], out var key))
                stats.Problems.TryGetValue(key, out record);
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

        // Draw state. Random mode consumes candidates from a mutable pool
        // copy via swap-remove; deterministic mode advances a cursor through
        // the source-ordered pool. Either way an already-drawn decision is
        // skipped without consuming quota.
        var drawnFlags = new bool[items.Count];
        var drawnPerEntry = new int[n];
        int totalDrawn = 0;
        var randomRemaining = _mix.RandomOrder
            ? Array.ConvertAll(pools, p => new List<int>(p))
            : null;
        var cursors = new int[n];

        void DrawFrom(int i, int quota)
        {
            if (randomRemaining is not null)
            {
                var remaining = randomRemaining[i];
                while (quota > 0 && remaining.Count > 0)
                {
                    int j = _random.Next(remaining.Count);
                    int idx = remaining[j];
                    remaining[j] = remaining[^1];
                    remaining.RemoveAt(remaining.Count - 1);
                    if (!drawnFlags[idx])
                    {
                        drawnFlags[idx] = true;
                        drawnPerEntry[i]++;
                        totalDrawn++;
                        quota--;
                    }
                }
            }
            else
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
        }

        bool HasSupply(int i)
        {
            if (randomRemaining is not null)
                return randomRemaining[i].Exists(idx => !drawnFlags[idx]);
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

        // Presentation: collect in source order, then shuffle in random mode.
        var result = new List<BgDecisionData>(totalDrawn);
        for (int idx = 0; idx < items.Count; idx++)
        {
            if (drawnFlags[idx])
                result.Add(items[idx]);
        }
        if (_mix.RandomOrder)
        {
            for (int i = result.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (result[i], result[j]) = (result[j], result[i]);
            }
        }

        var entryReports = new MixCompositionEntry[n];
        for (int i = 0; i < n; i++)
        {
            entryReports[i] = new MixCompositionEntry(
                entries[i].Category, entries[i].Percent,
                PoolSize: pools[i].Count,
                Requested: requested[i],
                Drawn: drawnPerEntry[i]);
        }
        composition = new MixComposition(
            target,
            HasRequestedLength: requestedLength is not null,
            totalDrawn,
            entryReports);

        return result;
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
