namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// A pool with one problem per distinct position (<see cref="ProblemKey"/>), the first
/// occurrence kept, and the duplicate classes it collapsed. Deduplicate after filtering,
/// never before: copies are not interchangeable to a filter (players, dates, error bands),
/// so deduplicating first could keep the copy the filter rejects and lose the one it would
/// pass.
/// </summary>
public sealed class DistinctProblems
{
    private DistinctProblems(
        IReadOnlyList<BgDecisionData> problems, IReadOnlyList<DuplicatePositionClass> duplicateClasses)
    {
        Problems = problems;
        DuplicateClasses = duplicateClasses;
    }

    /// <summary>The surviving problems, in first-occurrence order.</summary>
    public IReadOnlyList<BgDecisionData> Problems { get; }

    /// <summary>
    /// Every position seen more than once: its key and its members in occurrence order, the
    /// survivor first. Classes are in the survivors' order.
    /// </summary>
    public IReadOnlyList<DuplicatePositionClass> DuplicateClasses { get; }

    /// <summary>How many problems were dropped: every member of every class past its survivor.</summary>
    public int DuplicatesCollapsed => DuplicateClasses.Sum(duplicateClass => duplicateClass.Members.Count - 1);

    /// <summary>
    /// Deduplicate <paramref name="decisions"/> by position, yielding to the caller's thread on
    /// <paramref name="yielder"/>'s budget.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public static async ValueTask<DistinctProblems> OfAsync(
        IReadOnlyList<BgDecisionData> decisions,
        CooperativeYielder yielder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(yielder);

        var survivors = new List<BgDecisionData>();
        var indexByKey = new Dictionary<ProblemKey, int>();
        var classesBySlot = new Dictionary<int, (ProblemKey Key, List<DecisionId> Members)>();
        foreach (var item in decisions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = ProblemKey.From(item);
            if (!indexByKey.TryGetValue(key, out int index))
            {
                indexByKey.Add(key, survivors.Count);
                survivors.Add(item);
            }
            else
            {
                if (!classesBySlot.TryGetValue(index, out var duplicateClass))
                    classesBySlot[index] = duplicateClass = (key, [survivors[index].Id]);
                duplicateClass.Members.Add(item.Id);
            }

            await yielder.YieldIfDueAsync();
        }

        var classes = ImmutableCopy.Of(
            classesBySlot
                .OrderBy(entry => entry.Key)
                .Select(entry => new DuplicatePositionClass(entry.Value.Key, entry.Value.Members)),
            nameof(DuplicateClasses));
        return new DistinctProblems(ImmutableCopy.Of(survivors, nameof(Problems)), classes);
    }
}
