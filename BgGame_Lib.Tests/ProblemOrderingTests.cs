namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;

/// <summary>
/// The order's key pass (<see cref="ProblemOrdering.OverAsync"/>), shared by
/// the plain decorator and the composer: one key per keyed decision, read
/// before any sort, paced by the host's <see cref="CooperativeYielder"/> and
/// cancellable decision by decision. What the orders do with the keys is
/// pinned in <see cref="OrderedProblemSetSourceTests"/> and
/// <see cref="MixedProblemSetSourceTests"/>.
/// </summary>
public class ProblemOrderingTests
{
    /// <summary>
    /// A monotonic clock that counts its reads — how these tests observe the
    /// pass consulting the yielder, race-free across an awaited call (see
    /// <see cref="CooperativeYielderTests"/>): a check that does not yield
    /// reads it once; one that yields reads it again after resuming.
    /// <see cref="StepPerRead"/> advances it after every read, so a large step
    /// makes every check due; <see cref="OnRead"/> runs after every read.
    /// </summary>
    private sealed class CountingClock : TimeProvider
    {
        private long _ticks;

        public int Reads { get; private set; }
        public TimeSpan StepPerRead { get; init; }
        public Action<int>? OnRead { get; init; }

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            long now = _ticks;
            _ticks += StepPerRead.Ticks;
            Reads++;
            OnRead?.Invoke(Reads);
            return now;
        }
    }

    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(50);

    /// <summary>Five decisions with distinct errors and move numbers.</summary>
    private static IReadOnlyList<BgDecisionData> Decisions() =>
    [
        Play("a.xg", move: 4, userPlayIndex: 1),
        Play("b.xg", move: 2, userPlayIndex: 0),
        Play("c.xg", move: 5, userPlayIndex: 2),
        Play("d.xg", move: 1, userPlayIndex: null, unlistedPlayError: 0.3),
        Play("e.xg", move: 3, userPlayIndex: null, unlistedPlayError: 0.05),
    ];

    private static BgDecisionData Play(string file, int move, int? userPlayIndex, double? unlistedPlayError = null) =>
        TestRecords.CheckerPlay(
            id: new XgDecisionId(file, Game: 1, MoveNumber: move, IsCube: false),
            decision: TestRecords.CheckerPlayData(userPlayIndex: userPlayIndex, unlistedPlayError: unlistedPlayError));

    private static ValueTask<ProblemOrdering> OverAsync(
        IReadOnlyList<BgDecisionData> decisions, IReadOnlyList<int> keyed, ProblemOrder order,
        CooperativeYielder yielder, CancellationToken cancellationToken = default) =>
        ProblemOrdering.OverAsync(
            decisions, keyed, order, TestReadings.DoublersError(), records: null, new Random(1), yielder, cancellationToken);

    private static List<int> All(IReadOnlyList<BgDecisionData> decisions) =>
        Enumerable.Range(0, decisions.Count).ToList();

    public static TheoryData<ProblemOrder> KeyedOrders =>
        new(ProblemOrder.ByErrorLargestFirst, ProblemOrder.ByMoveNumberAscending);

    [Theory]
    [MemberData(nameof(KeyedOrders))]
    public async Task TheKeyPass_ConsultsTheYielderOncePerKeyedDecision(ProblemOrder order)
    {
        var clock = new CountingClock();
        var yielder = new CooperativeYielder(clock, Budget);   // one read: the window starts
        var decisions = Decisions();

        await OverAsync(decisions, All(decisions), order, yielder);

        Assert.Equal(1 + decisions.Count, clock.Reads);
    }

    [Theory]
    [MemberData(nameof(KeyedOrders))]
    public async Task TheKeyPass_YieldsWhenTheBudgetHasElapsed(ProblemOrder order)
    {
        // Every read moves the clock past the budget, so every decision's
        // check is due and yields: two reads a decision.
        var clock = new CountingClock { StepPerRead = TimeSpan.FromSeconds(1) };
        var yielder = new CooperativeYielder(clock, Budget);
        var decisions = Decisions();

        await OverAsync(decisions, All(decisions), order, yielder);

        Assert.Equal(1 + (2 * decisions.Count), clock.Reads);
    }

    [Theory]
    [MemberData(nameof(KeyedOrders))]
    public async Task TheKeyPass_HonoursCancellation_DecisionByDecision(ProblemOrder order)
    {
        // Cancelled as the third decision's key is read: the fourth is never
        // reached.
        using var cancellation = new CancellationTokenSource();
        var clock = new CountingClock { OnRead = reads => { if (reads == 1 + 3) cancellation.Cancel(); } };
        var yielder = new CooperativeYielder(clock, Budget);
        var decisions = Decisions();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await OverAsync(decisions, All(decisions), order, yielder, cancellation.Token));

        Assert.Equal(1 + 3, clock.Reads);
    }

    [Fact]
    public async Task TheKeyPass_ReadsTheKeyedPositionsAlone_AndTheyArrangeByThem()
    {
        // Positions 3 (unstated 0.3) and 2 (0.1813) are keyed; the other three
        // are never read — and never arranged.
        var clock = new CountingClock();
        var yielder = new CooperativeYielder(clock, Budget);
        var decisions = Decisions();

        var ordering = await OverAsync(decisions, [2, 3], ProblemOrder.ByErrorLargestFirst, yielder);
        var positions = new List<int> { 2, 3 };
        ordering.Arrange(positions);

        Assert.Equal(1 + 2, clock.Reads);
        Assert.Equal([3, 2], positions);
    }

    [Theory]
    [InlineData(ProblemOrder.AsRead)]
    [InlineData(ProblemOrder.Shuffled)]
    public void AnOrderWithNoKey_ReadsNothing_AndCompletesSynchronously(ProblemOrder order)
    {
        var clock = new CountingClock { StepPerRead = TimeSpan.FromSeconds(1) };
        var yielder = new CooperativeYielder(clock, Budget);
        var decisions = Decisions();

        var pending = OverAsync(decisions, All(decisions), order, yielder);

        Assert.True(pending.IsCompletedSuccessfully);
        Assert.Equal(1, clock.Reads);
    }
}
