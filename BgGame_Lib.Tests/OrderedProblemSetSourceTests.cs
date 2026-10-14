namespace BgGame_Lib.Tests;

using System.Globalization;
using System.Runtime.CompilerServices;
using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;

public class OrderedProblemSetSourceTests
{
    /// <summary>In-memory source with a fixed item list, re-iterable from the start.</summary>
    private sealed class InMemorySource : IProblemSetSource
    {
        private readonly IReadOnlyList<BgDecisionData> _items;
        public InMemorySource(string name, IReadOnlyList<BgDecisionData> items)
            : this(name, items, items.Count)
        {
        }

        public InMemorySource(string name, IReadOnlyList<BgDecisionData> items, int? count)
        {
            Name = name;
            _items = items;
            Count = count;
        }

        public string Name { get; }
        public int? Count { get; }

        /// <summary>How many items this source has yielded, over every enumeration.</summary>
        public int Yielded { get; private set; }

        public async IAsyncEnumerable<BgDecisionData> EnumerateAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var item in _items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Yielded++;
                yield return item;
                await Task.Yield();
            }
        }
    }

    private static IReadOnlyList<BgDecisionData> Decisions(int count) =>
        Enumerable.Range(0, count).Select(Decision).ToList();

    private static BgDecisionData Decision(int seed) =>
        TestRecords.CheckerPlay(id: new XgpDecisionId($"test-{seed}.xgp"));

    /// <summary>The seed a decision was built for, read back off its file name.</summary>
    private static int SeedOf(BgDecisionData item) =>
        int.Parse(item.SourceFile["test-".Length..^".xgp".Length], CultureInfo.InvariantCulture);

    private static async Task<List<int>> IdsAsync(IProblemSetSource source, CancellationToken ct = default)
    {
        var ids = new List<int>();
        await foreach (var item in source.EnumerateAsync(ct))
            ids.Add(SeedOf(item));
        return ids;
    }

    [Fact]
    public async Task SameSeed_YieldsSameOrder()
    {
        var items = Decisions(30);
        var a = new OrderedProblemSetSource(new InMemorySource("Test", items), ProblemOrder.Shuffled, PlayRanking.Equity, seed: 42);
        var b = new OrderedProblemSetSource(new InMemorySource("Test", items), ProblemOrder.Shuffled, PlayRanking.Equity, seed: 42);

        Assert.Equal(await IdsAsync(a), await IdsAsync(b));
    }

    [Fact]
    public async Task DifferentSeeds_YieldDifferentOrder()
    {
        var items = Decisions(30);
        var a = new OrderedProblemSetSource(new InMemorySource("Test", items), ProblemOrder.Shuffled, PlayRanking.Equity, seed: 1);
        var b = new OrderedProblemSetSource(new InMemorySource("Test", items), ProblemOrder.Shuffled, PlayRanking.Equity, seed: 2);

        Assert.NotEqual(await IdsAsync(a), await IdsAsync(b));
    }

    [Fact]
    public async Task Shuffle_IsPermutationOfOriginal()
    {
        var items = Decisions(20);
        var source = new OrderedProblemSetSource(new InMemorySource("Test", items), ProblemOrder.Shuffled, PlayRanking.Equity, seed: 7);

        var shuffled = await IdsAsync(source);

        Assert.Equal(items.Select(SeedOf).Order(), shuffled.Order());
        Assert.NotEqual(items.Select(SeedOf), shuffled);
    }

    [Fact]
    public async Task Restart_ReshufflesRatherThanRepeating()
    {
        var items = Decisions(30);
        var source = new OrderedProblemSetSource(new InMemorySource("Test", items), ProblemOrder.Shuffled, PlayRanking.Equity, seed: 7);

        var first = await IdsAsync(source);
        var second = await IdsAsync(source);

        Assert.NotEqual(first, second);
        Assert.Equal(first.Order(), second.Order());
    }

    [Fact]
    public async Task NoShuffle_UnwrappedSourceStillYieldsFileOrder()
    {
        var items = Decisions(20);
        var inner = new InMemorySource("Test", items);

        // The decorator is additive: using the inner source directly (without
        // wrapping it) still yields strict file order, unaffected by
        // OrderedProblemSetSource existing at all.
        var ids = await IdsAsync(inner);

        Assert.Equal(items.Select(SeedOf), ids);
    }

    [Fact]
    public void Name_PassesThroughFromInner()
    {
        var inner = new InMemorySource("My Set", Decisions(3));
        var source = new OrderedProblemSetSource(inner, ProblemOrder.Shuffled, PlayRanking.Equity, seed: 1);

        Assert.Equal("My Set", source.Name);
    }

    [Fact]
    public void Count_PassesThroughFromInner()
    {
        var inner = new InMemorySource("Test", Decisions(9));
        var source = new OrderedProblemSetSource(inner, ProblemOrder.Shuffled, PlayRanking.Equity, seed: 1);

        Assert.Equal(9, source.Count);
    }

    [Fact]
    public void Count_PassesThroughNull_WhenInnerIsStreaming()
    {
        var streamingInner = new InMemorySource("Test", Decisions(4), count: null);
        var source = new OrderedProblemSetSource(streamingInner, ProblemOrder.Shuffled, PlayRanking.Equity, seed: 1);

        Assert.Null(source.Count);
    }

    [Fact]
    public async Task EnumerateAsync_HonoursCancellation()
    {
        var inner = new InMemorySource("Test", Decisions(5));
        var source = new OrderedProblemSetSource(inner, ProblemOrder.Shuffled, PlayRanking.Equity, seed: 1);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in source.EnumerateAsync(cts.Token)) { }
        });
    }

    [Fact]
    public void Constructor_NullInner_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new OrderedProblemSetSource(null!, ProblemOrder.Shuffled, PlayRanking.Equity, seed: 1));
        Assert.Throws<ArgumentNullException>(() => new OrderedProblemSetSource(null!, ProblemOrder.Shuffled, PlayRanking.Equity));
    }

    // =======================================================================
    //  Every order (SPEC-filtering.md §7)
    // =======================================================================

    public static TheoryData<ProblemOrder> EveryOrder => new(Enum.GetValues<ProblemOrder>());

    [Theory]
    [MemberData(nameof(EveryOrder))]
    public void NameAndCount_PassThrough_UnderEveryOrder(ProblemOrder order)
    {
        var counted = new OrderedProblemSetSource(new InMemorySource("My Set", Decisions(4)), order, PlayRanking.Equity, seed: 1);
        var streaming = new OrderedProblemSetSource(new InMemorySource("My Set", Decisions(4), count: null), order, PlayRanking.Equity, seed: 1);

        Assert.Equal("My Set", counted.Name);
        Assert.Equal(4, counted.Count);
        Assert.Null(streaming.Count);
    }

    [Theory]
    [MemberData(nameof(EveryOrder))]
    public async Task EveryOrder_PresentsEachItemOnce(ProblemOrder order)
    {
        var items = Decisions(12);
        var source = new OrderedProblemSetSource(new InMemorySource("Test", items), order, PlayRanking.Equity, seed: 3);

        Assert.Equal(items.Select(SeedOf).Order(), (await IdsAsync(source)).Order());
    }

    [Fact]
    public void Constructor_RejectsAnUndefinedOrderOrRanking()
    {
        var inner = new InMemorySource("Test", Decisions(1));

        Assert.Equal("order", Assert.Throws<ArgumentOutOfRangeException>(
            () => new OrderedProblemSetSource(inner, (ProblemOrder)99, PlayRanking.Equity)).ParamName);
        Assert.Equal("ranking", Assert.Throws<ArgumentOutOfRangeException>(
            () => new OrderedProblemSetSource(inner, ProblemOrder.AsRead, (PlayRanking)99, seed: 1)).ParamName);
    }

    // -----------------------------------------------------------------------
    //  As read — inert
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AsRead_YieldsSourceOrder_DuplicatesIncluded()
    {
        var items = new[] { Decision(3), Decision(1), Decision(3), Decision(2) };
        var source = new OrderedProblemSetSource(new InMemorySource("Test", items), ProblemOrder.AsRead, PlayRanking.Equity);

        Assert.Equal([3, 1, 3, 2], await IdsAsync(source));
    }

    [Fact]
    public async Task AsRead_StreamsWithoutMaterializing()
    {
        // At the first item out, as read has drawn one item in; an order that
        // sorts or shuffles has drained the whole inner source first.
        var asReadInner = new InMemorySource("Test", Decisions(10));
        var shuffledInner = new InMemorySource("Test", Decisions(10));

        await FirstAsync(new OrderedProblemSetSource(asReadInner, ProblemOrder.AsRead, PlayRanking.Equity));
        await FirstAsync(new OrderedProblemSetSource(shuffledInner, ProblemOrder.Shuffled, PlayRanking.Equity, seed: 1));

        Assert.Equal(1, asReadInner.Yielded);
        Assert.Equal(10, shuffledInner.Yielded);

        static async Task FirstAsync(IProblemSetSource source)
        {
            await foreach (var _ in source.EnumerateAsync())
                break;
        }
    }

    // -----------------------------------------------------------------------
    //  By error, largest first — XG's error under the given ranking, exactly
    //  as the filter's error facet reads it
    // -----------------------------------------------------------------------

    /// <summary>
    /// The opening 3-1 in <paramref name="file"/>, at <paramref name="move"/>
    /// of <paramref name="game"/>, played as the candidate at
    /// <paramref name="userPlayIndex"/>: 0 is the best play (error 0), 1 errs
    /// 0.1731, 2 errs 0.1813. With no index, <paramref name="unlistedPlayError"/>
    /// is the analyser's error for an unlisted play, and with neither no play
    /// is recorded at all.
    /// </summary>
    private static CheckerPlayDecision Play(
        string file, int move = 1, int game = 1, int? userPlayIndex = 0, double? unlistedPlayError = null) =>
        TestRecords.CheckerPlay(
            id: new XgDecisionId(file, Game: game, MoveNumber: move, IsCube: false),
            decision: TestRecords.CheckerPlayData(userPlayIndex: userPlayIndex, unlistedPlayError: unlistedPlayError));

    private static async Task<List<DecisionId>> OrderOfAsync(
        IReadOnlyList<BgDecisionData> items, ProblemOrder order, PlayRanking ranking = PlayRanking.Equity)
    {
        var source = new OrderedProblemSetSource(new InMemorySource("Test", items), order, ranking, seed: 1);
        var ids = new List<DecisionId>();
        await foreach (var item in source.EnumerateAsync())
            ids.Add(item.Id);
        return ids;
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
    public async Task ByError_NoErrorCountsAsZero_AndEqualErrorsKeepSourceOrder()
    {
        // A correct play's error is 0 and an unrecorded move's counts as 0.0:
        // the three tie, so they keep the order they were read in, behind the
        // one decision with an error.
        var correctFirst = Play("a.xg", userPlayIndex: 0);
        var notRecorded = Play("b.xg", userPlayIndex: null);
        var erred = Play("c.xg", userPlayIndex: 1);
        var correctLast = Play("d.xg", userPlayIndex: 0);

        Assert.Equal(
            [erred.Id, correctFirst.Id, notRecorded.Id, correctLast.Id],
            await OrderOfAsync([correctFirst, notRecorded, erred, correctLast], ProblemOrder.ByErrorLargestFirst));
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
        // The played move is the deeper analysis of two: under equity the
        // shallower, higher play is best and the move errs 0.05; depth first
        // makes the played move best, so its error is 0. The unstated move's
        // 0.03 is the same under both — and lands on either side of it.
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
    public void ErrorKey_WithARecordHoldingSubmissions_IsItsLossPerAnswer_ElseXgsError()
    {
        // The key's mixed context, which the composer supplies: the record it
        // classified against. A real record with no loss is 0.0, not XG's error.
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
        // With no record — the plain context — the key and the filter's error
        // facet agree, decision by decision and ranking by ranking (DecisionRow
        // reads the same PlayerResult).
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

    // -----------------------------------------------------------------------
    //  By move number, ascending
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ByMoveNumber_IsTheNumberAlone_AcrossGamesAndFiles()
    {
        var a = Play("a.xg", game: 1, move: 7);
        var b = Play("b.xg", game: 3, move: 2);
        var c = Play("a.xg", game: 2, move: 5);

        Assert.Equal(
            [b.Id, c.Id, a.Id],
            await OrderOfAsync([a, b, c], ProblemOrder.ByMoveNumberAscending));
    }

    [Fact]
    public async Task ByMoveNumber_EqualNumbersKeepSourceOrder()
    {
        var first = Play("b.xg", game: 2, move: 3);
        var second = Play("a.xg", game: 1, move: 3);
        var earlier = Play("c.xg", game: 1, move: 1);

        Assert.Equal(
            [earlier.Id, first.Id, second.Id],
            await OrderOfAsync([first, second, earlier], ProblemOrder.ByMoveNumberAscending));
    }

    [Fact]
    public async Task ByMoveNumber_ADecisionWithNoMoveNumber_GoesLast_InSourceOrder()
    {
        // A position file's decision has no move number.
        var positionB = TestRecords.CheckerPlay(id: new XgpDecisionId("b.xgp"));
        var late = Play("a.xg", move: 9);
        var positionA = TestRecords.CheckerPlay(id: new XgpDecisionId("a.xgp"));
        var early = Play("c.xg", move: 2);

        Assert.Equal(
            [early.Id, late.Id, positionB.Id, positionA.Id],
            await OrderOfAsync([positionB, late, positionA, early], ProblemOrder.ByMoveNumberAscending));
    }

    // -----------------------------------------------------------------------
    //  Every enumeration re-applies the order to the inner source's state now
    // -----------------------------------------------------------------------

    [Fact]
    public async Task EachEnumeration_OrdersTheInnersCurrentItems()
    {
        var a = Play("a.xg", move: 4);
        var b = Play("b.xg", move: 2);
        var c = Play("c.xg", move: 3);
        var items = new List<BgDecisionData> { a, b };
        var source = new OrderedProblemSetSource(
            new InMemorySource("Test", items, count: null), ProblemOrder.ByMoveNumberAscending, PlayRanking.Equity);

        var first = new List<DecisionId>();
        await foreach (var item in source.EnumerateAsync())
            first.Add(item.Id);

        items.Add(c);
        var second = new List<DecisionId>();
        await foreach (var item in source.EnumerateAsync())
            second.Add(item.Id);

        Assert.Equal([b.Id, a.Id], first);
        Assert.Equal([b.Id, c.Id, a.Id], second);
    }
}
