namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// The one application of a <see cref="ProblemOrder"/>: puts a set of
/// decisions — named by their positions in one enumeration's materialized
/// list — in the order. Built once per enumeration over that list, so each
/// decision's key is read once however many sets it is then put in.
/// </summary>
/// <remarks>
/// <para>
/// <b>Positions, not decisions.</b> A position is a decision's index in the
/// source-ordered list the ordering was built over, so position order
/// <i>is</i> source order. Every deterministic order breaks a tie by position,
/// ascending, which is what keeps equal keys in source order whatever
/// arrangement the positions arrive in — and what makes each sort a total
/// order, so its result is fully determined even though
/// <see cref="List{T}.Sort(Comparison{T})"/> is not itself stable.
/// </para>
/// <para>
/// <b>Shuffled</b> is a Fisher–Yates pass with the host's
/// <see cref="Random"/>: uniform, and fresh on every call. Every other order
/// consumes no randomness at all.
/// </para>
/// <para>
/// Not thread-safe: <see cref="Arrange"/> may advance the host's
/// <see cref="Random"/>.
/// </para>
/// </remarks>
internal sealed class ProblemOrdering
{
    private readonly Random _random;

    /// <summary>The deterministic order's comparison of two positions; <see langword="null"/> for <see cref="ProblemOrder.Shuffled"/>.</summary>
    private readonly Comparison<int>? _comparison;

    private ProblemOrdering(Random random, Comparison<int>? comparison)
    {
        _random = random;
        _comparison = comparison;
    }

    /// <summary>
    /// Prepare <paramref name="order"/> over <paramref name="decisions"/>,
    /// one enumeration's materialized list in source order: the keys the
    /// order sorts by are read here, once per decision.
    /// </summary>
    /// <param name="decisions">The enumeration's decisions, in source order.</param>
    /// <param name="order">The order; a defined value (see <see cref="RequireDefined"/>).</param>
    /// <param name="ranking">The ranking the by-error key reads XG's error under.</param>
    /// <param name="random">The host's generator, which a shuffle advances.</param>
    internal static ProblemOrdering Over(
        IReadOnlyList<BgDecisionData> decisions,
        ProblemOrder order,
        PlayRanking ranking,
        Random random)
    {
        Comparison<int>? comparison = order switch
        {
            ProblemOrder.AsRead => static (a, b) => a.CompareTo(b),
            ProblemOrder.Shuffled => null,
            ProblemOrder.ByErrorLargestFirst => LargestFirst(ErrorKeys(decisions, ranking)),
            ProblemOrder.ByMoveNumberAscending => AscendingNoneLast(MoveNumbers(decisions)),
            _ => throw new ArgumentOutOfRangeException(nameof(order), order, "Not a defined problem order."),
        };
        return new(random, comparison);
    }

    /// <summary>
    /// The by-error key: the error XG recorded for the move actually played,
    /// under <paramref name="ranking"/>, exactly as the filter's error facet
    /// reads it; 0.0 for a decision with no error. A correct decision's error
    /// is 0, and an unstated move's is the analyser's
    /// (<see cref="PlayerResult.TryGetError"/>).
    /// </summary>
    internal static double ErrorKey(BgDecisionData decision, PlayRanking ranking) =>
        decision.ViewFor(ranking).PlayerResult.TryGetError(out double error) ? error : 0.0;

    /// <summary>
    /// Put <paramref name="positions"/> in the order, in place: positions
    /// into the list this ordering was built over, in any arrangement.
    /// </summary>
    internal void Arrange(List<int> positions)
    {
        if (_comparison is not null)
        {
            positions.Sort(_comparison);
            return;
        }

        // Fisher–Yates: for each index from the end, swap with a uniformly
        // chosen earlier-or-equal index.
        for (int i = positions.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (positions[i], positions[j]) = (positions[j], positions[i]);
        }
    }

    /// <summary>
    /// Refuses an undefined <paramref name="order"/> or
    /// <paramref name="ranking"/> — the hosts' construction-time check, so a
    /// bad value fails where it is passed rather than at the first
    /// enumeration.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Either value is not a defined member.</exception>
    internal static void RequireDefined(ProblemOrder order, PlayRanking ranking)
    {
        if (!Enum.IsDefined(order))
            throw new ArgumentOutOfRangeException(nameof(order), order, "Not a defined problem order.");
        if (!Enum.IsDefined(ranking))
            throw new ArgumentOutOfRangeException(nameof(ranking), ranking, "Not a defined play ranking.");
    }

    private static double[] ErrorKeys(IReadOnlyList<BgDecisionData> decisions, PlayRanking ranking)
    {
        var keys = new double[decisions.Count];
        for (int i = 0; i < keys.Length; i++)
            keys[i] = ErrorKey(decisions[i], ranking);
        return keys;
    }

    private static int?[] MoveNumbers(IReadOnlyList<BgDecisionData> decisions)
    {
        var numbers = new int?[decisions.Count];
        for (int i = 0; i < numbers.Length; i++)
            numbers[i] = decisions[i].MoveNumber;
        return numbers;
    }

    /// <summary>Largest key first; equal keys by position.</summary>
    private static Comparison<int> LargestFirst(double[] keys) => (a, b) =>
    {
        int byKey = keys[b].CompareTo(keys[a]);
        return byKey != 0 ? byKey : a.CompareTo(b);
    };

    /// <summary>Smallest number first, no number last; equal numbers, and the numberless, by position.</summary>
    private static Comparison<int> AscendingNoneLast(int?[] numbers) => (a, b) =>
    {
        int byNumber = (numbers[a], numbers[b]) switch
        {
            (int x, int y) => x.CompareTo(y),
            (null, null) => 0,
            (null, _) => 1,
            (_, null) => -1,
        };
        return byNumber != 0 ? byNumber : a.CompareTo(b);
    };
}
