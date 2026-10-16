namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// The problems a run presents, in order. A blank mix presents the whole pool in the order;
/// an active mix composes a selection from the pool by its categories and quotas against the
/// stats document, then arranges the selection in the order (SPEC-filtering.md §5 and §7: the
/// mix selects, the order presents).
/// </summary>
public sealed class ProblemSequence
{
    private ProblemSequence(IReadOnlyList<BgDecisionData> problems, MixComposition? composition)
    {
        Problems = problems;
        Composition = composition;
    }

    /// <summary>The problems, in presentation order.</summary>
    public IReadOnlyList<BgDecisionData> Problems { get; }

    /// <summary>How the mix composed the selection; null for a blank mix.</summary>
    public MixComposition? Composition { get; }

    /// <summary>
    /// Build the sequence a run presents from <paramref name="pool"/>. A blank
    /// <paramref name="mix"/> keeps every problem and reads nothing from
    /// <paramref name="stats"/>; an active mix needs the document and judges each problem's
    /// record as of <paramref name="now"/>. <paramref name="random"/> decides shuffles and
    /// draws, so a seeded instance makes the result reproducible. The work yields to the
    /// caller's thread on <paramref name="yielder"/>'s budget.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// An argument is null, or <paramref name="stats"/> is null while the mix is active.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The order or the ranking is not a defined value.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public static async ValueTask<ProblemSequence> BuildAsync(
        IReadOnlyList<BgDecisionData> pool,
        QuizMix mix,
        ProblemOrder order,
        PlayRanking ranking,
        ProblemStatsDocument? stats,
        DateTimeOffset now,
        Random random,
        CooperativeYielder yielder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(mix);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(yielder);
        ProblemOrdering.RequireDefined(order, ranking);
        if (stats is null && !mix.IsPassthrough)
        {
            throw new ArgumentNullException(
                nameof(stats),
                "An active mix composes against the stats document (an empty document is valid); a blank mix needs none.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (mix.IsPassthrough)
        {
            var positions = Enumerable.Range(0, pool.Count).ToList();
            var ordering = await ProblemOrdering.OverAsync(
                pool, positions, order, ranking, records: null, random, yielder, cancellationToken);
            ordering.Arrange(positions);
            return new ProblemSequence(
                ImmutableCopy.Of(positions.Select(position => pool[position]), nameof(Problems)), composition: null);
        }

        var (drawn, composition) = await ComposeAsync(
            pool, mix, order, ranking, stats!, now, random, yielder, cancellationToken);
        return new ProblemSequence(ImmutableCopy.Of(drawn, nameof(Problems)), composition);
    }

    private static async ValueTask<(List<BgDecisionData> Drawn, MixComposition Composition)> ComposeAsync(
        IReadOnlyList<BgDecisionData> pool,
        QuizMix mix,
        ProblemOrder order,
        PlayRanking ranking,
        ProblemStatsDocument stats,
        DateTimeOffset now,
        Random random,
        CooperativeYielder yielder,
        CancellationToken cancellationToken)
    {
        // One record per decision id: a pool that repeats an id contributes it once.
        var items = new List<BgDecisionData>(pool.Count);
        var seenIds = new HashSet<DecisionId>();
        foreach (var item in pool)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (seenIds.Add(item.Id))
                items.Add(item);
        }

        var entries = mix.Entries;
        int n = entries.Count;
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

        // Classify by the problem's lifetime record (content identity), never by its own id.
        var records = new ProblemStats?[items.Count];
        for (int idx = 0; idx < items.Count; idx++)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
        foreach (var categoryPool in pools)
        {
            foreach (var idx in categoryPool)
            {
                if (!reachable[idx])
                {
                    reachable[idx] = true;
                    unionCount++;
                }
            }
        }

        var reachablePositions = new List<int>(unionCount);
        for (int idx = 0; idx < items.Count; idx++)
        {
            if (reachable[idx])
                reachablePositions.Add(idx);
        }

        int? requestedLength = mix.QuizLength;
        int target = requestedLength ?? unionCount;
        int drawGoal = Math.Min(target, unionCount);
        var percents = new int[n];
        for (int i = 0; i < n; i++)
            percents[i] = entries[i].Percent;
        var requested = Apportion(target, percents);

        // Each pool is arranged in the order, so a quota is filled from the front of its ordered set.
        var ordering = await ProblemOrdering.OverAsync(
            items, reachablePositions, order, ranking, records, random, yielder, cancellationToken);
        foreach (var categoryPool in pools)
            ordering.Arrange(categoryPool);

        var drawnFlags = new bool[items.Count];
        var drawnPerEntry = new int[n];
        int totalDrawn = 0;
        var cursors = new int[n];

        void DrawFrom(int i, int quota)
        {
            var categoryPool = pools[i];
            while (quota > 0 && cursors[i] < categoryPool.Count)
            {
                int idx = categoryPool[cursors[i]++];
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
            var categoryPool = pools[i];
            for (int c = cursors[i]; c < categoryPool.Count; c++)
            {
                if (!drawnFlags[categoryPool[c]])
                    return true;
            }

            return false;
        }

        for (int i = 0; i < n; i++)
            DrawFrom(i, requested[i]);

        // A shortfall is redistributed among the entries that still have supply, by their weights.
        while (totalDrawn < drawGoal)
        {
            var eligible = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (HasSupply(i))
                    eligible.Add(i);
            }

            if (eligible.Count == 0)
                break;   // unreachable (drawGoal <= union); defensive against an invariant break
            var extra = Apportion(
                drawGoal - totalDrawn,
                eligible.ConvertAll(i => percents[i]).ToArray());
            for (int k = 0; k < eligible.Count; k++)
                DrawFrom(eligible[k], extra[k]);
        }

        // The whole selection is arranged again, across rows, for presentation.
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

    /// <summary>Largest-remainder apportionment of <paramref name="total"/> by <paramref name="weights"/>; ties go to the earlier entry.</summary>
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
