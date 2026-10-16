namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// The one application of a <see cref="ProblemOrder"/>: puts a set of
/// decisions — named by their positions in one enumeration's materialized
/// list — in the order. Built once per build over that list, so each
/// decision's key is read once however many sets it is then put in.
/// <see cref="ProblemSequence"/> is its one host: a blank mix puts the whole
/// pool in the order; an active mix puts each row's eligible set in it before
/// filling the row's quota from the front, then the whole selection again.
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
/// <b>What a key costs, and where it is paid.</b> The by-error key reads XG's
/// error through a filter view (<see cref="BgDecisionData.ViewFor"/>), which
/// reads the ranked plays and computes after-play boards each time one is
/// built — and the order runs inside a run's prepare, beside the current
/// run's own parse, on a WebAssembly heap that a large sample already fills
/// to about half its limit. So <see cref="OverAsync"/> reads each key once,
/// into an array indexed by position, for the positions the ordering will
/// arrange and no others; a comparison only reads the array. The pass is
/// paced by the host's <see cref="CooperativeYielder"/> and honours
/// cancellation, decision by decision, like the rest of the enumeration
/// path; the sorts in <see cref="Arrange"/> stay synchronous. The view is
/// dropped as soon as its error is read: what outlives the pass, per
/// decision, is the key alone, held by this ordering, which its host drops
/// with the enumeration's composition.
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
    /// order sorts by are read here — once per keyed position, never inside a
    /// comparison — yielding on <paramref name="yielder"/>'s time budget and
    /// honouring <paramref name="cancellationToken"/> as it goes. Orders with
    /// no key (as read, shuffled) read nothing and complete synchronously.
    /// </summary>
    /// <param name="decisions">The enumeration's decisions, in source order.</param>
    /// <param name="keyed">
    /// The positions this ordering will arrange: every set later handed to
    /// <see cref="Arrange"/> is drawn from them. Keys are read for these alone,
    /// so a decision no set can reach costs nothing.
    /// </param>
    /// <param name="order">The order; a defined value (see <see cref="RequireDefined"/>).</param>
    /// <param name="ranking">The ranking the by-error key reads XG's error under.</param>
    /// <param name="records">
    /// The by-error key's context. With an active mix: each decision's
    /// lifetime record, by position, as the composition looked it up in the
    /// stats document it classified against (<see langword="null"/> where the
    /// problem has none). Without a mix: <see langword="null"/> — no document.
    /// </param>
    /// <param name="random">The host's generator, which a shuffle advances.</param>
    /// <param name="yielder">The host's enumeration-path pacer, consulted once per keyed decision.</param>
    /// <param name="cancellationToken">Observed once per keyed decision.</param>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled during the pass.</exception>
    internal static async ValueTask<ProblemOrdering> OverAsync(
        IReadOnlyList<BgDecisionData> decisions,
        IReadOnlyList<int> keyed,
        ProblemOrder order,
        PlayRanking ranking,
        IReadOnlyList<ProblemStats?>? records,
        Random random,
        CooperativeYielder yielder,
        CancellationToken cancellationToken)
    {
        switch (order)
        {
            case ProblemOrder.AsRead:
                return new(random, static (a, b) => a.CompareTo(b));

            case ProblemOrder.Shuffled:
                return new(random, comparison: null);

            case ProblemOrder.ByErrorLargestFirst:
            {
                var keys = new double[decisions.Count];
                for (int k = 0; k < keyed.Count; k++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int position = keyed[k];
                    keys[position] = ErrorKey(decisions[position], ranking, records?[position]);
                    await yielder.YieldIfDueAsync();
                }
                return new(random, LargestFirst(keys));
            }

            case ProblemOrder.ByMoveNumberAscending:
            {
                var numbers = new int?[decisions.Count];
                for (int k = 0; k < keyed.Count; k++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int position = keyed[k];
                    numbers[position] = decisions[position].MoveNumber;
                    await yielder.YieldIfDueAsync();
                }
                return new(random, AscendingNoneLast(numbers));
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(order), order, "Not a defined problem order.");
        }
    }

    /// <summary>
    /// The by-error key — one definition, in two contexts (SPEC-filtering.md
    /// §7). Where <paramref name="record"/> holds submissions, the player's
    /// lifetime loss per answer (<see cref="QuizStatsMeasures.LossPerAnswer"/>;
    /// a real record with no loss reads 0.0). Otherwise the error XG recorded
    /// for the move actually played, under <paramref name="ranking"/>, exactly
    /// as the filter's error facet reads it
    /// (<see cref="PlayerResult.TryGetError"/>): a correct decision's is 0, and
    /// an unstated move's is the analyser's. A decision with no error from
    /// either source counts as 0.0.
    /// </summary>
    /// <param name="decision">The decision.</param>
    /// <param name="ranking">The run's ranking.</param>
    /// <param name="record">
    /// The problem's lifetime record, read from the stats document an active
    /// mix's composition classified against; <see langword="null"/> when it
    /// has none, and always without a mix, where no document is consulted.
    /// </param>
    internal static double ErrorKey(BgDecisionData decision, PlayRanking ranking, ProblemStats? record) =>
        QuizStatsMeasures.LossPerAnswer(decision, record)
        ?? (decision.ViewFor(ranking).PlayerResult.TryGetError(out double error) ? error : 0.0);

    /// <summary>
    /// Put <paramref name="positions"/> in the order, in place: positions
    /// into the list this ordering was built over, drawn from the keyed ones
    /// it was prepared for, in any arrangement. Synchronous — the keys were
    /// read when the ordering was prepared.
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
