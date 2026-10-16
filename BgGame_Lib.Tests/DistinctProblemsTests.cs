namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;

public class DistinctProblemsTests
{
    private static readonly CooperativeYielder Yielder = new(TimeProvider.System);

    private static DecisionId Id(string file) => new XgpDecisionId(file);

    private static BgDecisionData Decision(
        string file, int position, int matchLength = 7, bool isCube = false) =>
        Decision(file, TestRecords.MatchSession(
            length: matchLength, onRollNeeds: position, opponentNeeds: position), isCube);

    private static BgDecisionData Decision(string file, Session session, bool isCube = false)
    {
        var id = new XgpDecisionId(file);
        var position = TestRecords.Position(session: session);
        return isCube
            ? TestRecords.Cube(id: id, position: position)
            : TestRecords.CheckerPlay(id: id, position: position);
    }

    private static ValueTask<DistinctProblems> Distinct(
        IReadOnlyList<BgDecisionData> items, CancellationToken cancellationToken = default) =>
        DistinctProblems.OfAsync(items, Yielder, cancellationToken);

    private static IEnumerable<DecisionId> Ids(DistinctProblems distinct) => distinct.Problems.Select(item => item.Id);

    [Fact]
    public async Task DuplicatePositions_AcrossDistinctIds_CollapseToFirstOccurrence()
    {
        var distinct = await Distinct(
        [
            Decision("a.xgp", position: 1),
            Decision("b.xgp", position: 1),
            Decision("c.xgp", position: 2),
        ]);
        Assert.Equal([Id("a.xgp"), Id("c.xgp")], Ids(distinct));
    }

    [Fact]
    public async Task CrossFileDuplicates_CollapseIdentically()
    {
        var distinct = await Distinct(
        [
            Decision("match-copy1.xgp", position: 1),
            Decision("match-copy1(2).xgp", position: 2),
            Decision("match-copy2.xgp", position: 1),
            Decision("match-copy2(2).xgp", position: 2),
        ]);
        Assert.Equal([Id("match-copy1.xgp"), Id("match-copy1(2).xgp")], Ids(distinct));
    }

    [Fact]
    public async Task SameAwayScores_AcrossMatchLengths_Collapse()
    {
        var seven = Decision("seven.xgp", position: 5, matchLength: 7);
        var eleven = Decision("eleven.xgp", position: 5, matchLength: 11);
        Assert.NotEqual(seven.Xgid, eleven.Xgid);
        Assert.Equal([Id("seven.xgp")], Ids(await Distinct([seven, eleven])));
    }

    [Fact]
    public async Task MirrorTurnDuplicates_Collapse()
    {
        var terms = new MatchTerms { Length = 7 };
        var distinct = await Distinct(
        [
            Decision("as-player1.xgp", Session.Create(
                terms, new MatchStanding { Away1 = 3, Away2 = 5, IsCrawford = false }, Seat.Player1)),
            Decision("as-player2.xgp", Session.Create(
                terms, new MatchStanding { Away1 = 5, Away2 = 3, IsCrawford = false }, Seat.Player2)),
        ]);
        Assert.Equal([Id("as-player1.xgp")], Ids(distinct));
    }

    [Fact]
    public async Task SamePosition_PlayAndCubeDecisions_StayDistinct()
    {
        var distinct = await Distinct(
        [
            Decision("play.xgp", position: 1),
            Decision("cube.xgp", position: 1, isCube: true),
        ]);
        Assert.Equal([Id("play.xgp"), Id("cube.xgp")], Ids(distinct));
    }

    [Fact]
    public async Task AllDuplicates_CollapseToOne()
    {
        var distinct = await Distinct(
        [
            Decision("a.xgp", position: 1),
            Decision("b.xgp", position: 1),
            Decision("c.xgp", position: 1),
            Decision("d.xgp", position: 1),
        ]);
        Assert.Equal([Id("a.xgp")], Ids(distinct));
        Assert.Equal(3, distinct.DuplicatesCollapsed);
    }

    [Fact]
    public async Task Output_PreservesFirstOccurrenceOrder_AcrossUniqueAndDuplicate()
    {
        var distinct = await Distinct(
        [
            Decision("a.xgp", position: 1),
            Decision("b.xgp", position: 2),
            Decision("dup.xgp", position: 1),
            Decision("c.xgp", position: 3),
            Decision("dup2.xgp", position: 2),
            Decision("d.xgp", position: 4),
        ]);
        Assert.Equal([Id("a.xgp"), Id("b.xgp"), Id("c.xgp"), Id("d.xgp")], Ids(distinct));
    }

    [Fact]
    public async Task DuplicateClasses_HoldOnlyMultiMemberClasses_MembersInOccurrenceOrder()
    {
        var a = Decision("a.xgp", position: 1);
        var distinct = await Distinct(
        [
            a,
            Decision("b.xgp", position: 2),
            Decision("c.xgp", position: 1),
            Decision("d.xgp", position: 3),
            Decision("e.xgp", position: 2),
            Decision("f.xgp", position: 1),
        ]);
        var classes = distinct.DuplicateClasses;
        Assert.Equal(2, classes.Count);
        Assert.Equal(ProblemKey.From(a), classes[0].Key);
        Assert.Equal([Id("a.xgp"), Id("c.xgp"), Id("f.xgp")], classes[0].Members);
        Assert.Equal([Id("b.xgp"), Id("e.xgp")], classes[1].Members);
        Assert.Equal(3, distinct.DuplicatesCollapsed);
    }

    [Fact]
    public async Task DuplicateClasses_EmptyWhenNothingCollapsed()
    {
        var distinct = await Distinct(
        [
            Decision("a.xgp", position: 1),
            Decision("b.xgp", position: 2),
        ]);
        Assert.Empty(distinct.DuplicateClasses);
        Assert.Equal(0, distinct.DuplicatesCollapsed);
    }

    [Fact]
    public async Task EmptyPool_YieldsEmpty()
    {
        var distinct = await Distinct([]);
        Assert.Empty(distinct.Problems);
        Assert.Empty(distinct.DuplicateClasses);
    }

    [Fact]
    public async Task SingleItem_PassesThrough()
    {
        Assert.Equal([Id("a.xgp")], Ids(await Distinct([Decision("a.xgp", position: 1)])));
    }

    [Fact]
    public async Task TheSameInput_GivesTheSameResult()
    {
        BgDecisionData[] items =
        [
            Decision("a.xgp", position: 1),
            Decision("b.xgp", position: 1),
            Decision("c.xgp", position: 2),
        ];
        Assert.Equal(Ids(await Distinct(items)), Ids(await Distinct(items)));
    }

    [Fact]
    public async Task OfAsync_HonoursCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await Distinct([Decision("a.xgp", position: 1)], cts.Token));
    }

    [Fact]
    public async Task OfAsync_RejectsNulls()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await DistinctProblems.OfAsync(null!, Yielder));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await DistinctProblems.OfAsync([], null!));
    }
}
