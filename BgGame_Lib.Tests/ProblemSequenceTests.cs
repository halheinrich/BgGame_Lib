namespace BgGame_Lib.Tests;

using System.Globalization;
using System.Runtime.CompilerServices;
using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;

public class ProblemSequenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly CooperativeYielder Yielder = new(TimeProvider.System);

    private static DecisionId Id(int n) => new XgpDecisionId($"d{n}.xgp");

    private static BgDecisionData Decision(int n) =>
        TestRecords.CheckerPlay(id: Id(n), position: PositionOf(n));

    private static PositionData PositionOf(int n) => TestRecords.Position(
        session: n == 0
            ? TestRecords.MoneySession(isJacoby: true)
            : TestRecords.MatchSession(length: n, onRollNeeds: n, opponentNeeds: n));

    private static int NumberOf(BgDecisionData item) =>
        item.Session.Match(money: static _ => 0, match: static m => m.OnRollNeeds);

    private static IReadOnlyList<BgDecisionData> Decisions(int count) =>
        Enumerable.Range(0, count).Select(Decision).ToList();

    private static BgDecisionData Copy(int n, string file) =>
        TestRecords.CheckerPlay(id: new XgpDecisionId(file), position: PositionOf(n));

    private static ProblemKey Key(int n) => ProblemKey.From(Decision(n));

    private static ProblemStats SeenCorrect(int n, int daysAgo = 1) =>
        new(Key(n), new ScoreSegment(1, 1, 0.0), Now - TimeSpan.FromDays(daysAgo));

    private static ProblemStats SeenWrong(int n, int daysAgo = 1) =>
        new(Key(n), new ScoreSegment(1, 0, 0.1), Now - TimeSpan.FromDays(daysAgo));

    private static ProblemStatsDocument Doc(params ProblemStats[] stats) => ProblemStatsDocument.FromStats(stats);

    private static QuizMixEntry Entry(QuizCategory category, int percent) => new(category, percent);

    private static readonly QuizMix Everything = new([Entry(QuizCategory.EverythingElse, 100)]);

    private static ValueTask<ProblemSequence> Build(
        IReadOnlyList<BgDecisionData> pool,
        QuizMix mix,
        ProblemStatsDocument? doc,
        int seed = 7,
        ProblemOrder order = ProblemOrder.Shuffled,
        PlayRanking ranking = PlayRanking.Equity,
        CancellationToken cancellationToken = default) =>
        ProblemSequence.BuildAsync(pool, mix, order, ranking, doc, Now, new Random(seed), Yielder, cancellationToken);

    private static async Task<List<int>> NumbersAsync(
        IReadOnlyList<BgDecisionData> pool, QuizMix mix, ProblemStatsDocument? doc,
        int seed = 7, ProblemOrder order = ProblemOrder.Shuffled) =>
        (await Build(pool, mix, doc, seed, order)).Problems.Select(NumberOf).ToList();

    // ── Blank mix: the pool in the order, the stats document never read ─────────

    [Fact]
    public async Task BlankMix_PresentsThePoolInSourceOrder_WithNoComposition()
    {
        var sequence = await Build(Decisions(10), QuizMix.Empty, ProblemStatsDocument.Empty, order: ProblemOrder.AsRead);
        Assert.Equal(Enumerable.Range(0, 10), sequence.Problems.Select(NumberOf));
        Assert.Null(sequence.Composition);
    }

    [Fact]
    public async Task BlankMix_DoesNotDedupe()
    {
        var items = new[] { Decision(0), Decision(0), Decision(1) };
        Assert.Equal([0, 0, 1], await NumbersAsync(items, QuizMix.Empty, null, order: ProblemOrder.AsRead));
    }

    [Fact]
    public async Task BlankMix_NeedsNoStatsDocument_AndReadsNoneGiven()
    {
        var items = Varied();
        var doc = Doc(RecordOf(items[1], 1, 0.9));
        foreach (var order in Enum.GetValues<ProblemOrder>())
        {
            var without = await NumbersAsync(items, QuizMix.Empty, null, seed: 11, order: order);
            var with = await NumbersAsync(items, QuizMix.Empty, doc, seed: 11, order: order);
            Assert.Equal(without, with);
        }
    }

    [Fact]
    public async Task BlankMix_ByError_OrdersByXgsError_NeverTheStatsDocument()
    {
        var items = Varied();
        var doc = Doc(RecordOf(items[1], 1, 0.9));
        Assert.Equal([3, 4, 1, 5, 2, 6], await NumbersAsync(items, QuizMix.Empty, doc, order: ProblemOrder.ByErrorLargestFirst));
    }

    [Fact]
    public async Task BlankMix_ByMoveNumber_OrdersTheWholePool()
    {
        var sequence = await Build(Varied(), QuizMix.Empty, null, order: ProblemOrder.ByMoveNumberAscending);
        Assert.Equal([5, 2, 6, 4, 1, 3], sequence.Problems.Select(NumberOf));
        Assert.Equal(6, sequence.Problems.Count);
    }

    [Fact]
    public async Task Shuffled_IsAPermutation_SameSeedSameOrder_DifferentSeedsDiffer()
    {
        var items = Decisions(30);
        var a = await NumbersAsync(items, QuizMix.Empty, null, seed: 42);
        var b = await NumbersAsync(items, QuizMix.Empty, null, seed: 42);
        var c = await NumbersAsync(items, QuizMix.Empty, null, seed: 1);
        Assert.Equal(Enumerable.Range(0, 30), a.Order());
        Assert.NotEqual(Enumerable.Range(0, 30), a);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    public static TheoryData<ProblemOrder> EveryOrder => new(Enum.GetValues<ProblemOrder>());

    [Theory]
    [MemberData(nameof(EveryOrder))]
    public async Task EveryOrder_PresentsEachItemOnce(ProblemOrder order)
    {
        var items = Decisions(12);
        var numbers = await NumbersAsync(items, QuizMix.Empty, null, seed: 3, order: order);
        Assert.Equal(items.Select(NumberOf).Order(), numbers.Order());
    }

    [Fact]
    public async Task AsRead_KeepsSourceOrder_DuplicatesIncluded()
    {
        var items = new[] { Decision(3), Decision(1), Decision(3), Decision(2) };
        Assert.Equal([3, 1, 3, 2], await NumbersAsync(items, QuizMix.Empty, null, order: ProblemOrder.AsRead));
    }

    // ── The orders themselves, through a blank mix ───────────────────────────────

    private static CheckerPlayDecision Play(
        string file, int move = 1, int game = 1, int? userPlayIndex = 0, double? unlistedPlayError = null) =>
        TestRecords.CheckerPlay(
            id: new XgDecisionId(file, Game: game, MoveNumber: move, IsCube: false),
            decision: TestRecords.CheckerPlayData(userPlayIndex: userPlayIndex, unlistedPlayError: unlistedPlayError));

    private static async Task<List<DecisionId>> OrderOfAsync(
        IReadOnlyList<BgDecisionData> items, ProblemOrder order, PlayRanking ranking = PlayRanking.Equity, int seed = 1)
    {
        var sequence = await ProblemSequence.BuildAsync(
            items, QuizMix.Empty, order, ranking, stats: null, Now, new Random(seed), Yielder);
        return sequence.Problems.Select(item => item.Id).ToList();
    }

    /// <summary>
    /// The tie rule (SPEC-filtering.md §7, ruled 2026-10-15): over twelve fixed seeds, the
    /// keyed prefix is always <paramref name="sorted"/>, the rest is always the whole
    /// <paramref name="tied"/> group, the same seed gives the same order, and the group's
    /// order is not the same every time. Twelve seeds over a group of two or more leave a
    /// one-in-two-thousand chance of a false "never varied" for a correct implementation
    /// at worst, and the seeds are fixed, so the verdict is the same on every run.
    /// </summary>
    private static async Task AssertTiesFallRandomlyAsync(
        IReadOnlyList<BgDecisionData> pool, ProblemOrder order, DecisionId[] sorted, DecisionId[] tied)
    {
        var tieOrders = new HashSet<string>();
        for (int seed = 1; seed <= 12; seed++)
        {
            var ids = await OrderOfAsync(pool, order, seed: seed);
            Assert.Equal(sorted, ids.Take(sorted.Length));
            Assert.True(tied.ToHashSet().SetEquals(ids.Skip(sorted.Length)), "the tie group is not whole");
            Assert.Equal(ids, await OrderOfAsync(pool, order, seed: seed));
            tieOrders.Add(string.Join(",", ids.Skip(sorted.Length)));
        }

        Assert.True(tieOrders.Count > 1, "equal keys never varied across twelve seeds");
    }

    [Fact]
    public async Task ByError_PresentsTheLargestErrorFirst()
    {
        var correct = Play("a.xg", userPlayIndex: 0);          // 0
        var smaller = Play("b.xg", userPlayIndex: 1);          // 0.1731
        var larger = Play("c.xg", userPlayIndex: 2);           // 0.1813
        Assert.Equal(
            [larger.Id, smaller.Id, correct.Id],
            await OrderOfAsync([correct, smaller, larger], ProblemOrder.ByErrorLargestFirst));
    }

    [Fact]
    public async Task ByError_AnUnstatedMove_UsesTheAnalysersError()
    {
        var scored = Play("a.xg", userPlayIndex: 1);                                  // 0.1731
        var unstated = Play("b.xg", userPlayIndex: null, unlistedPlayError: 0.25);    // 0.25
        var correct = Play("c.xg", userPlayIndex: 0);                                 // 0
        Assert.Equal(
            [unstated.Id, scored.Id, correct.Id],
            await OrderOfAsync([scored, unstated, correct], ProblemOrder.ByErrorLargestFirst));
    }

    [Fact]
    public async Task ByError_NoErrorCountsAsZero_AndEqualErrorsFallInARandomOrder_FreshPerBuild()
    {
        var correctFirst = Play("a.xg", userPlayIndex: 0);
        var notRecorded = Play("b.xg", userPlayIndex: null);
        var erred = Play("c.xg", userPlayIndex: 1);
        var correctLast = Play("d.xg", userPlayIndex: 0);
        await AssertTiesFallRandomlyAsync(
            [correctFirst, notRecorded, erred, correctLast], ProblemOrder.ByErrorLargestFirst,
            sorted: [erred.Id], tied: [correctFirst.Id, notRecorded.Id, correctLast.Id]);
    }

    [Fact]
    public async Task ByError_ACubeDecision_ReadsItsCubeError()
    {
        var cube = TestRecords.Cube(
            id: new XgDecisionId("a.xg", Game: 1, MoveNumber: 4, IsCube: true),
            decision: TestRecords.CubeData(
                userDoublerAction: null, userTakerAction: null, unstatedDoublerActionError: 0.175));
        var below = Play("b.xg", userPlayIndex: 1);    // 0.1731
        var above = Play("c.xg", userPlayIndex: 2);    // 0.1813
        Assert.Equal(
            [above.Id, cube.Id, below.Id],
            await OrderOfAsync([below, cube, above], ProblemOrder.ByErrorLargestFirst));
    }

    [Fact]
    public async Task ByError_ReadsTheErrorUnderTheGivenRanking()
    {
        var rankingDependent = TestRecords.CheckerPlay(
            id: new XgDecisionId("a.xg", Game: 1, MoveNumber: 1, IsCube: false),
            decision: TestRecords.CheckerPlayData(
                plays:
                [
                    TestRecords.Candidate(analysisLevel: AnalysisLevel.Ply2, equity: 0.10),
                    TestRecords.Candidate(play: [new(13, 10), new(6, 5)], analysisLevel: AnalysisLevel.Ply3, equity: 0.05),
                ],
                userPlayIndex: 1));
        var unstated = Play("b.xg", userPlayIndex: null, unlistedPlayError: 0.03);
        Assert.Equal(
            [rankingDependent.Id, unstated.Id],
            await OrderOfAsync([rankingDependent, unstated], ProblemOrder.ByErrorLargestFirst, PlayRanking.Equity));
        Assert.Equal(
            [unstated.Id, rankingDependent.Id],
            await OrderOfAsync([rankingDependent, unstated], ProblemOrder.ByErrorLargestFirst, PlayRanking.DepthFirst));
    }

    [Fact]
    public async Task ByMoveNumber_IsTheNumberAlone_AcrossGamesAndFiles()
    {
        var a = Play("a.xg", game: 1, move: 7);
        var b = Play("b.xg", game: 3, move: 2);
        var c = Play("a.xg", game: 2, move: 5);
        Assert.Equal([b.Id, c.Id, a.Id], await OrderOfAsync([a, b, c], ProblemOrder.ByMoveNumberAscending));
    }

    [Fact]
    public async Task ByMoveNumber_EqualNumbersFallInARandomOrder_AfterTheSmallerNumber()
    {
        var first = Play("b.xg", game: 2, move: 3);
        var second = Play("a.xg", game: 1, move: 3);
        var earlier = Play("c.xg", game: 1, move: 1);
        await AssertTiesFallRandomlyAsync(
            [first, second, earlier], ProblemOrder.ByMoveNumberAscending,
            sorted: [earlier.Id], tied: [first.Id, second.Id]);
    }

    [Fact]
    public async Task ByMoveNumber_ANonStandardStartsNumber_IsUsedAsRecorded()
    {
        var nonStandard = TestRecords.CheckerPlay(
            id: new XgDecisionId("a.xg", Game: 1, MoveNumber: 4, IsCube: false),
            descriptive: TestRecords.Descriptive(isStandardStart: false));
        var three = Play("b.xg", move: 3);
        var five = Play("c.xg", move: 5);
        Assert.Equal(
            [three.Id, nonStandard.Id, five.Id],
            await OrderOfAsync([five, nonStandard, three], ProblemOrder.ByMoveNumberAscending));
    }

    [Fact]
    public async Task ByMoveNumber_DecisionsWithNoMoveNumber_GoLast_InARandomOrderAmongThemselves()
    {
        var positionB = TestRecords.CheckerPlay(id: new XgpDecisionId("b.xgp"));
        var late = Play("a.xg", move: 9);
        var positionA = TestRecords.CheckerPlay(id: new XgpDecisionId("a.xgp"));
        var early = Play("c.xg", move: 2);
        await AssertTiesFallRandomlyAsync(
            [positionB, late, positionA, early], ProblemOrder.ByMoveNumberAscending,
            sorted: [early.Id, late.Id], tied: [positionB.Id, positionA.Id]);
    }

    // ── Active mix: composition ──────────────────────────────────────────────────

    [Fact]
    public async Task EmptyPool_ComposesNothing_WithHonestTelemetry()
    {
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)], quizLength: 5);
        var sequence = await Build([], mix, ProblemStatsDocument.Empty);
        Assert.Empty(sequence.Problems);
        var composition = sequence.Composition!;
        Assert.Equal(5, composition.TargetCount);
        Assert.Equal(0, composition.DrawnCount);
        Assert.Equal(5, composition.Entries[0].Requested);
        Assert.Equal(0, composition.Entries[0].Drawn);
    }

    [Fact]
    public async Task EmptyStatsDocument_MakesEverythingNeverSeen()
    {
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)]);
        Assert.Equal(Enumerable.Range(0, 10), await NumbersAsync(Decisions(10), mix, ProblemStatsDocument.Empty, order: ProblemOrder.AsRead));
    }

    [Fact]
    public async Task SingleDecision_ComposesAOneItemQuiz()
    {
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)]);
        var sequence = await Build(Decisions(1), mix, ProblemStatsDocument.Empty);
        Assert.Equal([0], sequence.Problems.Select(NumberOf));
        Assert.Equal(1, sequence.Composition!.DrawnCount);
    }

    [Fact]
    public async Task ActiveMix_DedupesByDecisionId_FirstOccurrenceWins()
    {
        var items = new[] { Decision(0), Decision(0), Decision(1) };
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)]);
        Assert.Equal([0, 1], await NumbersAsync(items, mix, ProblemStatsDocument.Empty, order: ProblemOrder.AsRead));
    }

    [Fact]
    public async Task UnmatchedDecisions_AreUnreachableWithoutEverythingElse()
    {
        var doc = Doc(SeenWrong(0), SeenCorrect(2));
        var mix = new QuizMix([Entry(QuizCategory.GotWrong, 100)]);
        Assert.Equal([0], await NumbersAsync(Decisions(3), mix, doc, order: ProblemOrder.AsRead));
    }

    [Fact]
    public async Task EverythingElse_CollectsExactlyTheUnmatched()
    {
        var doc = Doc(SeenWrong(0), SeenCorrect(2));
        var mix = new QuizMix([Entry(QuizCategory.GotWrong, 50), Entry(QuizCategory.EverythingElse, 50)]);
        var sequence = await Build(Decisions(3), mix, doc, order: ProblemOrder.AsRead);
        Assert.Equal([0, 1, 2], sequence.Problems.Select(NumberOf));
        var report = sequence.Composition!;
        Assert.Equal(3, report.DrawnCount);
        Assert.Equal(1, report.Entries[0].PoolSize);   // GotWrong: {0}
        Assert.Equal(2, report.Entries[1].PoolSize);   // EverythingElse: {1, 2}
        Assert.Equal(1, report.Entries[0].Drawn);
        Assert.Equal(2, report.Entries[1].Drawn);
    }

    [Fact]
    public async Task OverlappingCategories_PresentADecisionAtMostOnce()
    {
        var doc = Doc(SeenWrong(0, daysAgo: 40), SeenCorrect(1, daysAgo: 40));
        var mix = new QuizMix([Entry(QuizCategory.GotWrong, 50), Entry(QuizCategory.NotSeenInDays(30), 50)]);
        var sequence = await Build(Decisions(2), mix, doc, order: ProblemOrder.AsRead);
        Assert.Equal([0, 1], sequence.Problems.Select(NumberOf));
        Assert.Equal(2, sequence.Composition!.DrawnCount);
    }

    [Fact]
    public async Task OverlapContest_GoesToTheEarlierEntry()
    {
        var doc = Doc(SeenWrong(0, daysAgo: 40), SeenCorrect(1, daysAgo: 40));
        var items = Decisions(2);
        var wrongFirst = await Build(items, new QuizMix(
            [Entry(QuizCategory.GotWrong, 50), Entry(QuizCategory.NotSeenInDays(30), 50)]), doc, order: ProblemOrder.AsRead);
        Assert.Equal(1, wrongFirst.Composition!.Entries[0].Drawn);   // GotWrong took 0
        Assert.Equal(1, wrongFirst.Composition.Entries[1].Drawn);    // stale entry got 1

        var staleFirst = await Build(items, new QuizMix(
            [Entry(QuizCategory.NotSeenInDays(30), 50), Entry(QuizCategory.GotWrong, 50)]), doc, order: ProblemOrder.AsRead);
        Assert.Equal(2, staleFirst.Composition!.Entries[0].Drawn);   // stale took 0 AND absorbed the shortfall
        Assert.Equal(0, staleFirst.Composition.Entries[1].Drawn);    // GotWrong's only candidate was contested away
    }

    [Fact]
    public async Task Apportionment_UsesLargestRemainder_TiesToEarlierEntry()
    {
        var mix = new QuizMix(
        [
            Entry(QuizCategory.NeverSeen, 34),
            Entry(QuizCategory.SeenFewerThan(1), 33),
            Entry(QuizCategory.NotSeenInDays(1), 33),
        ], quizLength: 10);
        var sequence = await Build(Decisions(12), mix, ProblemStatsDocument.Empty, order: ProblemOrder.AsRead);
        Assert.Equal(Enumerable.Range(0, 10), sequence.Problems.Select(NumberOf));
        var report = sequence.Composition!;
        Assert.Equal([4, 3, 3], report.Entries.Select(e => e.Requested));
        Assert.Equal([4, 3, 3], report.Entries.Select(e => e.Drawn));
        Assert.Equal(report.TargetCount, report.Entries.Sum(e => e.Requested));
    }

    [Fact]
    public async Task QuizLength_CapsTheComposition()
    {
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)], quizLength: 4);
        Assert.Equal([0, 1, 2, 3], await NumbersAsync(Decisions(10), mix, ProblemStatsDocument.Empty, order: ProblemOrder.AsRead));
    }

    [Fact]
    public async Task QuizLengthBeyondSupply_DrawsEverythingReachable_AndReportsIt()
    {
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)], quizLength: 10);
        var sequence = await Build(Decisions(3), mix, ProblemStatsDocument.Empty);
        Assert.Equal(3, sequence.Problems.Count);
        Assert.Equal(10, sequence.Composition!.TargetCount);
        Assert.Equal(3, sequence.Composition.DrawnCount);
        Assert.Equal(10, sequence.Composition.Entries[0].Requested);
        Assert.Equal(3, sequence.Composition.Entries[0].Drawn);
    }

    [Fact]
    public async Task HasRequestedLength_RecordsWhereTheTargetCameFrom()
    {
        var capless = await Build(Decisions(3), new QuizMix([Entry(QuizCategory.NeverSeen, 100)]), ProblemStatsDocument.Empty);
        Assert.False(capless.Composition!.HasRequestedLength);
        Assert.Equal(3, capless.Composition.TargetCount);   // the reachable union

        var capped = await Build(Decisions(3), new QuizMix([Entry(QuizCategory.NeverSeen, 100)], quizLength: 2), ProblemStatsDocument.Empty);
        Assert.True(capped.Composition!.HasRequestedLength);
        Assert.Equal(2, capped.Composition.TargetCount);

        var cappedAtUnion = await Build(Decisions(3), new QuizMix([Entry(QuizCategory.NeverSeen, 100)], quizLength: 3), ProblemStatsDocument.Empty);
        Assert.True(cappedAtUnion.Composition!.HasRequestedLength);
        Assert.Equal(
            (capless.Composition.TargetCount, capless.Composition.DrawnCount),
            (cappedAtUnion.Composition.TargetCount, cappedAtUnion.Composition.DrawnCount));
    }

    [Fact]
    public async Task Shortfall_RedistributesToEntriesWithSupply()
    {
        var doc = Doc(Enumerable.Range(2, 8).Select(n => SeenWrong(n)).ToArray());
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 50), Entry(QuizCategory.GotWrong, 50)], quizLength: 10);
        var sequence = await Build(Decisions(10), mix, doc, order: ProblemOrder.AsRead);
        Assert.Equal(Enumerable.Range(0, 10), sequence.Problems.Select(NumberOf));
        var report = sequence.Composition!;
        Assert.Equal(10, report.DrawnCount);
        Assert.Equal(new MixCompositionEntry(QuizCategory.NeverSeen, 50, 2, 5, 2), report.Entries[0]);
        Assert.Equal(new MixCompositionEntry(QuizCategory.GotWrong, 50, 8, 5, 8), report.Entries[1]);
    }

    [Fact]
    public async Task SameSeed_SameInputs_YieldIdenticalCompositions_DifferentSeedsDrawAfresh()
    {
        var items = Decisions(30);
        var doc = Doc(Enumerable.Range(0, 15).Select(n => SeenWrong(n)).ToArray());
        var mix = new QuizMix([Entry(QuizCategory.GotWrong, 50), Entry(QuizCategory.EverythingElse, 50)], quizLength: 10);
        var a = await NumbersAsync(items, mix, doc, seed: 42);
        var b = await NumbersAsync(items, mix, doc, seed: 42);
        var c = await NumbersAsync(items, mix, doc, seed: 43);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public async Task AsRead_IsFullyDeterministic_WithoutASeed()
    {
        var doc = Doc(SeenCorrect(0), SeenCorrect(2), SeenCorrect(4));
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)]);
        var first = await ProblemSequence.BuildAsync(Decisions(6), mix, ProblemOrder.AsRead, PlayRanking.Equity, doc, Now, new Random(), Yielder);
        var second = await ProblemSequence.BuildAsync(Decisions(6), mix, ProblemOrder.AsRead, PlayRanking.Equity, doc, Now, new Random(), Yielder);
        Assert.Equal([1, 3, 5], first.Problems.Select(NumberOf));
        Assert.Equal([1, 3, 5], second.Problems.Select(NumberOf));
    }

    [Fact]
    public async Task ActiveMix_Shuffled_ShufflesPresentation()
    {
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)]);
        var numbers = await NumbersAsync(Decisions(30), mix, ProblemStatsDocument.Empty, seed: 7);
        Assert.Equal(Enumerable.Range(0, 30), numbers.Order());
        Assert.NotEqual(Enumerable.Range(0, 30), numbers);
    }

    [Fact]
    public async Task ActiveMix_WithoutAStatsDocument_IsAConsumerError()
    {
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await Build(Decisions(3), Everything, null));
        Assert.Equal("stats", ex.ParamName);
    }

    // ── Classification is by the problem's record (content identity) ────────────

    [Fact]
    public async Task Classification_JudgesACopyByTheProblemsRecord_NotItsOwnId()
    {
        var doc = Doc(SeenWrong(0));
        var mix = new QuizMix([Entry(QuizCategory.GotWrong, 100)]);
        Assert.Equal([0], await NumbersAsync([Copy(0, "other-file.xgp"), Decision(1)], mix, doc, order: ProblemOrder.AsRead));
    }

    [Fact]
    public async Task Classification_ACopyOfAProblemAnsweredCorrectly_IsNotGotWrong()
    {
        var doc = Doc(SeenCorrect(0));
        var mix = new QuizMix([Entry(QuizCategory.GotWrong, 100)]);
        Assert.Empty(await NumbersAsync([Copy(0, "other-file.xgp"), Decision(1)], mix, doc, order: ProblemOrder.AsRead));
    }

    [Fact]
    public async Task Classification_SightingsReadTheProblemsFullRecord_AcrossCopies()
    {
        var doc = Doc(SeenCorrect(0));
        var items = new[] { Copy(0, "other-file.xgp") };
        Assert.Equal([0], await NumbersAsync(items, new QuizMix([Entry(QuizCategory.SeenFewerThan(2), 100)]), doc, order: ProblemOrder.AsRead));
        Assert.Empty(await NumbersAsync(items, new QuizMix([Entry(QuizCategory.SeenFewerThan(1), 100)]), doc, order: ProblemOrder.AsRead));
    }

    [Fact]
    public async Task AverageLoss_JudgesACubeProblemByItsLossPerAnswer()
    {
        var cube = TestRecords.Cube(id: Id(5), position: PositionOf(5));
        var doc = Doc(new ProblemStats(ProblemKey.From(cube), new ScoreSegment(2, 1, 0.30), Now));
        var mix = new QuizMix([Entry(QuizCategory.AvgEquityLossOver(0.15), 100)]);
        Assert.Equal([5], await NumbersAsync([cube, Decision(2)], mix, doc, order: ProblemOrder.AsRead));
    }

    [Fact]
    public async Task ContentEqualCopies_AreDistinctRecordsHere_BothClassified()
    {
        var doc = Doc(SeenWrong(0));
        var mix = new QuizMix([Entry(QuizCategory.GotWrong, 100)]);
        Assert.Equal([0, 0], await NumbersAsync([Decision(0), Copy(0, "other-file.xgp")], mix, doc, order: ProblemOrder.AsRead));
    }

    // ── Active mix: the order inside and across rows ────────────────────────────

    private static BgDecisionData PlayOf(
        int n, int? userPlayIndex = 0, double? unlistedPlayError = null, int? move = null) =>
        TestRecords.CheckerPlay(
            id: move is int m ? new XgDecisionId($"d{n}.xg", Game: 1, MoveNumber: m, IsCube: false) : Id(n),
            position: PositionOf(n),
            decision: TestRecords.CheckerPlayData(userPlayIndex: userPlayIndex, unlistedPlayError: unlistedPlayError));

    private static ProblemStats RecordOf(BgDecisionData decision, int submitted, double loss) =>
        new(ProblemKey.From(decision), new ScoreSegment(submitted, 0, loss), Now - TimeSpan.FromDays(1));

    private static IReadOnlyList<BgDecisionData> Varied() =>
    [
        PlayOf(1, userPlayIndex: 1, move: 7),
        PlayOf(2, userPlayIndex: 0, move: 2),
        PlayOf(3, userPlayIndex: null, unlistedPlayError: 0.3),
        PlayOf(4, userPlayIndex: 2, move: 5),
        PlayOf(5, userPlayIndex: 1, move: 1),
        PlayOf(6, userPlayIndex: 0, move: 3),
    ];

    [Fact]
    public async Task ActiveMix_ByError_ReadsTheLossPerAnswer_WhereTheRecordHoldsSubmissions_ElseXgsError()
    {
        var played1 = PlayOf(1, userPlayIndex: 2);                             // XG 0.1813; record: 0.05 an answer
        var played2 = PlayOf(2, userPlayIndex: 0);                             // XG 0;      record: 0.30 an answer
        var played3 = PlayOf(3, userPlayIndex: 1);                             // XG 0.1731; no record
        var played4 = PlayOf(4, userPlayIndex: null, unlistedPlayError: 0.25);  // XG 0.25;   a record with no submissions
        var played5 = PlayOf(5, userPlayIndex: null, unlistedPlayError: 0.20);  // XG 0.20;   a real record with no loss: 0.0
        var played6 = PlayOf(6, userPlayIndex: null);                          // no error from either source: 0.0
        var cube7 = TestRecords.Cube(id: Id(7), position: PositionOf(7));       // XG 0;      two answers, 0.44: 0.22 an answer
        var doc = Doc(
            RecordOf(played1, 2, 0.10),
            RecordOf(played2, 1, 0.30),
            new ProblemStats(ProblemKey.From(played4), ScoreSegment.Empty, Now),
            RecordOf(played5, 3, 0.0),
            RecordOf(cube7, 4, 0.44));
        var numbers = await NumbersAsync([played1, played2, played3, played4, played5, played6, cube7], Everything, doc, order: ProblemOrder.ByErrorLargestFirst);
        Assert.Equal([2, 4, 7, 3, 1], numbers.Take(5));
        Assert.Equal([5, 6], numbers.Skip(5).Order());   // the 0.0 tie group, in whichever order the seed gave
    }

    [Fact]
    public async Task ActiveMix_ByError_FillsEachRowFromTheFrontOfItsOrderedSet()
    {
        var items = new[] { PlayOf(1), PlayOf(2), PlayOf(3), PlayOf(4) };
        var doc = Doc(
            RecordOf(items[0], 1, 0.1), RecordOf(items[1], 1, 0.4),
            RecordOf(items[2], 1, 0.2), RecordOf(items[3], 1, 0.3));
        var mix = new QuizMix([Entry(QuizCategory.GotWrong, 100)], quizLength: 2);
        Assert.Equal([2, 4], await NumbersAsync(items, mix, doc, order: ProblemOrder.ByErrorLargestFirst));
        Assert.Equal([1, 2], await NumbersAsync(items, mix, doc, order: ProblemOrder.AsRead));
    }

    [Fact]
    public async Task ActiveMix_SortsTheWholeSelectionAgain_AcrossRows()
    {
        var neverSeen = new[] { PlayOf(1, move: 9), PlayOf(2, move: 1), PlayOf(3, move: 5) };
        var gotWrong = new[] { PlayOf(4, move: 2), PlayOf(5, move: 8), PlayOf(6, move: 3) };
        var doc = Doc(gotWrong.Select(d => RecordOf(d, 1, 0.1)).ToArray());
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 50), Entry(QuizCategory.GotWrong, 50)], quizLength: 4);
        Assert.Equal([2, 4, 6, 3], await NumbersAsync([.. neverSeen, .. gotWrong], mix, doc, order: ProblemOrder.ByMoveNumberAscending));
    }

    [Fact]
    public async Task ActiveMix_ByMoveNumber_DecisionsWithNoMoveNumberGoLast_InARandomOrder()
    {
        var items = new[] { PlayOf(1), PlayOf(2, move: 6), PlayOf(3), PlayOf(4, move: 2) };
        var numbers = await NumbersAsync(items, Everything, ProblemStatsDocument.Empty, order: ProblemOrder.ByMoveNumberAscending);
        Assert.Equal([4, 2], numbers.Take(2));
        Assert.Equal([1, 3], numbers.Skip(2).Order());
    }

    [Fact]
    public async Task ActiveMix_ARowsQuotaFromATieGroup_IsARandomChoice_NotTheEarliestFiles()
    {
        // Four got-wrong decisions with one loss each: one tie group under By error, a quota
        // of two. Over twelve seeds the pair drawn is not always the same (SPEC-filtering.md
        // §7, ruled 2026-10-15: the quota from a tie group is a uniform random choice).
        var items = new[] { PlayOf(1), PlayOf(2), PlayOf(3), PlayOf(4) };
        var doc = Doc(items.Select(d => RecordOf(d, 1, 0.1)).ToArray());
        var mix = new QuizMix([Entry(QuizCategory.GotWrong, 100)], quizLength: 2);
        var pairs = new HashSet<string>();
        for (int seed = 1; seed <= 12; seed++)
        {
            var numbers = await NumbersAsync(items, mix, doc, seed, ProblemOrder.ByErrorLargestFirst);
            Assert.Equal(2, numbers.Count);
            pairs.Add(string.Join(",", numbers.Order()));
        }

        Assert.True(pairs.Count > 1, "the quota always took the earliest files");
    }

    // ── Arguments, cancellation, retention ──────────────────────────────────────

    [Fact]
    public async Task BuildAsync_RejectsNulls()
    {
        var pool = Decisions(1);
        var random = new Random(1);
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await ProblemSequence.BuildAsync(null!, QuizMix.Empty, ProblemOrder.AsRead, PlayRanking.Equity, null, Now, random, Yielder));
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await ProblemSequence.BuildAsync(pool, null!, ProblemOrder.AsRead, PlayRanking.Equity, null, Now, random, Yielder));
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await ProblemSequence.BuildAsync(pool, QuizMix.Empty, ProblemOrder.AsRead, PlayRanking.Equity, null, Now, null!, Yielder));
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await ProblemSequence.BuildAsync(pool, QuizMix.Empty, ProblemOrder.AsRead, PlayRanking.Equity, null, Now, random, null!));
    }

    [Fact]
    public async Task BuildAsync_RejectsAnUndefinedOrderOrRanking()
    {
        var pool = Decisions(1);
        var order = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await Build(pool, QuizMix.Empty, null, order: (ProblemOrder)99));
        Assert.Equal("order", order.ParamName);
        var ranking = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await Build(pool, QuizMix.Empty, null, ranking: (PlayRanking)99));
        Assert.Equal("ranking", ranking.ParamName);
    }

    [Theory]
    [MemberData(nameof(EveryOrder))]
    public async Task BuildAsync_HonoursCancellation_UnderEveryOrder(ProblemOrder order)
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await Build(Varied(), Everything, ProblemStatsDocument.Empty, order: order, cancellationToken: cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await Build(Varied(), QuizMix.Empty, null, order: order, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task TheSequence_KeepsNoReferenceToTheStatsDocument()
    {
        var (sequence, document) = await BuildAgainstAFreshDocumentAsync();
        Assert.NotEmpty(sequence.Problems);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(document.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<(ProblemSequence Sequence, WeakReference Document)> BuildAgainstAFreshDocumentAsync()
    {
        var document = Doc(SeenWrong(9));
        var sequence = await Build(Decisions(6), new QuizMix([Entry(QuizCategory.NeverSeen, 100)]), document, order: ProblemOrder.ByErrorLargestFirst);
        return (sequence, new WeakReference(document));
    }

    [Fact]
    public void ErrorKey_WithARecordHoldingSubmissions_IsItsLossPerAnswer_ElseXgsError()
    {
        var decision = Play("a.xg", userPlayIndex: 2);   // XG 0.1813
        var key = ProblemKey.From(decision);
        double xg = TestRecords.Row(decision).Error!.Value;
        Assert.Equal(0.125, ProblemOrdering.ErrorKey(
            decision, PlayRanking.Equity, new ProblemStats(key, new ScoreSegment(4, 1, 0.5), default)));
        Assert.Equal(0.0, ProblemOrdering.ErrorKey(
            decision, PlayRanking.Equity, new ProblemStats(key, new ScoreSegment(2, 2, 0.0), default)));
        Assert.Equal(xg, ProblemOrdering.ErrorKey(
            decision, PlayRanking.Equity, new ProblemStats(key, ScoreSegment.Empty, default)));
        Assert.Equal(xg, ProblemOrdering.ErrorKey(decision, PlayRanking.Equity, record: null));
    }

    [Fact]
    public void ErrorKey_IsTheFilterFacetsError_OrZero()
    {
        BgDecisionData[] decisions =
        [
            Play("a.xg", userPlayIndex: 0),
            Play("b.xg", userPlayIndex: 2),
            Play("c.xg", userPlayIndex: null, unlistedPlayError: 0.04),
            Play("d.xg", userPlayIndex: null),
            TestRecords.Cube(),
        ];
        foreach (var ranking in Enum.GetValues<PlayRanking>())
        {
            foreach (var decision in decisions)
            {
                Assert.Equal(
                    TestRecords.Row(decision, ranking).Error ?? 0.0,
                    ProblemOrdering.ErrorKey(decision, ranking, record: null));
            }
        }
    }

    [Fact]
    public void NumberOf_ReadsTheTestsOwnEncoding()
    {
        // The tests above encode a decision's number in its match score; this pins the helper they lean on.
        Assert.Equal(0, NumberOf(Decision(0)));
        Assert.Equal(4, NumberOf(Decision(4)));
        Assert.Equal("d4.xgp", Decision(4).Id.ToString().Contains("d4", StringComparison.Ordinal) ? "d4.xgp" : Decision(4).SourceFile);
        _ = CultureInfo.InvariantCulture;
    }
}
