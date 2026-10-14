namespace BgGame_Lib.Tests;

using System.Runtime.CompilerServices;
using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;

public class MixedProblemSetSourceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Deterministic <see cref="TimeProvider"/>: returns whatever
    /// <paramref name="now"/> it was constructed with.
    /// </summary>
    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly TimeProvider Clock = new TestClock(Now);

    /// <summary>In-memory source with a fixed item list, re-iterable from the start.</summary>
    private sealed class InMemorySource(string name, IReadOnlyList<BgDecisionData> items)
        : IProblemSetSource
    {
        public string Name { get; } = name;
        public int? Count { get; } = items.Count;

        public async IAsyncEnumerable<BgDecisionData> EnumerateAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
                await Task.Yield();
            }
        }
    }

    private static DecisionId Id(int n) => new XgpDecisionId($"d{n}.xgp");

    /// <summary>
    /// Synthesized decision <paramref name="n"/>: classification looks its
    /// lifetime record up by derived <see cref="ProblemKey"/>, so the facts
    /// must be real and key-derivable — the opening 3-1 on the standard
    /// starting board, distinguished (and read back by <c>IdsAsync</c>) via
    /// the away-scores pair <c>n</c>-away/<c>n</c>-away, or money for 0.
    ///
    /// <para>
    /// The money fixture is under the Jacoby rule, which the key grammar
    /// spells for money alone; a money session always states its rule, so
    /// the value is arbitrary here.
    /// </para>
    /// </summary>
    private static BgDecisionData Decision(int n) =>
        TestRecords.CheckerPlay(id: Id(n), position: PositionOf(n));

    /// <summary>
    /// Decision <paramref name="n"/>'s position: the standard start, 0-0 in
    /// an <paramref name="n"/>-point match, or a Jacoby money session for 0.
    /// </summary>
    private static PositionData PositionOf(int n) => TestRecords.Position(
        session: n == 0
            ? TestRecords.MoneySession(isJacoby: true)
            : TestRecords.MatchSession(length: n, onRollNeeds: n, opponentNeeds: n));

    /// <summary>The <c>n</c> a decision was built for: its away score, or 0 for money.</summary>
    private static int NumberOf(BgDecisionData item) =>
        item.Session.Match(money: static _ => 0, match: static m => m.OnRollNeeds);

    private static IReadOnlyList<BgDecisionData> Decisions(int count) =>
        Enumerable.Range(0, count).Select(Decision).ToList();

    /// <summary>
    /// A content-equal copy of decision <paramref name="n"/> under a
    /// different file-relative identity — same <see cref="ProblemKey"/>,
    /// different <see cref="DecisionId"/>.
    /// </summary>
    private static BgDecisionData Copy(int n, string file) =>
        TestRecords.CheckerPlay(id: new XgpDecisionId(file), position: PositionOf(n));

    /// <summary>Decision <paramref name="n"/>'s content key, via the one blessed factory.</summary>
    private static ProblemKey Key(int n) => ProblemKey.From(Decision(n));

    private static ProblemStats SeenCorrect(int n, int daysAgo = 1) =>
        new(Key(n), new ScoreSegment(1, 1, 0.0), Now - TimeSpan.FromDays(daysAgo));

    private static ProblemStats SeenWrong(int n, int daysAgo = 1) =>
        new(Key(n), new ScoreSegment(1, 0, 0.1), Now - TimeSpan.FromDays(daysAgo));

    private static ProblemStatsDocument Doc(params ProblemStats[] stats) =>
        ProblemStatsDocument.FromStats(stats);

    private static QuizMixEntry Entry(QuizCategory category, int percent) =>
        new(category, percent);

    /// <summary>
    /// The composer over <paramref name="items"/>, its mix composed against
    /// <paramref name="doc"/>. Shuffled by default — the random mode the
    /// retired toggle's default selected; the deterministic mode is
    /// <see cref="ProblemOrder.AsRead"/>.
    /// </summary>
    private static MixedProblemSetSource Source(
        IReadOnlyList<BgDecisionData> items,
        ProblemStatsDocument doc,
        QuizMix mix,
        int seed = 7,
        ProblemOrder order = ProblemOrder.Shuffled,
        PlayRanking ranking = PlayRanking.Equity) =>
        new(new InMemorySource("Test", items), order, ranking, mix, () => doc, Clock, seed);

    private static async Task<List<int>> IdsAsync(
        IProblemSetSource source, CancellationToken ct = default)
    {
        var ids = new List<int>();
        await foreach (var item in source.EnumerateAsync(ct))
            ids.Add(NumberOf(item));
        return ids;
    }

    // -----------------------------------------------------------------------
    //  Passthrough (blank mix, as read)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BlankMix_PassesEverythingThroughInSourceOrder()
    {
        var items = Decisions(10);
        var source = Source(items, ProblemStatsDocument.Empty, QuizMix.Empty, order: ProblemOrder.AsRead);

        var ids = await IdsAsync(source);

        Assert.Equal(Enumerable.Range(0, 10), ids);
        Assert.Null(source.LastComposition);
    }

    [Fact]
    public async Task BlankMix_DoesNotDedupe()
    {
        // Inert means inert: even duplicate ids stream through unchanged.
        var items = new[] { Decision(0), Decision(0), Decision(1) };
        var source = Source(items, ProblemStatsDocument.Empty, QuizMix.Empty, order: ProblemOrder.AsRead);

        Assert.Equal([0, 0, 1], await IdsAsync(source));
    }

    [Fact]
    public void Count_PassesThroughOnlyForBlankMix()
    {
        var items = Decisions(10);

        Assert.Equal(10, Source(items, ProblemStatsDocument.Empty, QuizMix.Empty).Count);
        Assert.Null(Source(
            items, ProblemStatsDocument.Empty,
            new QuizMix([Entry(QuizCategory.NeverSeen, 100)])).Count);
    }

    [Fact]
    public void Name_PassesThrough()
    {
        var source = Source(Decisions(1), ProblemStatsDocument.Empty, QuizMix.Empty);

        Assert.Equal("Test", source.Name);
    }

    // -----------------------------------------------------------------------
    //  Composition basics
    // -----------------------------------------------------------------------

    [Fact]
    public async Task EmptyInnerSource_YieldsNothing_WithHonestTelemetry()
    {
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)], quizLength: 5);
        var source = Source([], ProblemStatsDocument.Empty, mix);

        Assert.Empty(await IdsAsync(source));
        Assert.Equal(5, source.LastComposition!.TargetCount);
        Assert.Equal(0, source.LastComposition.DrawnCount);
        Assert.Equal(5, source.LastComposition.Entries[0].Requested);
        Assert.Equal(0, source.LastComposition.Entries[0].Drawn);
    }

    [Fact]
    public async Task EmptyStatsDocument_MakesEverythingNeverSeen()
    {
        // Falls out of the null-stats path — no special-casing to trip over.
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)]);
        var source = Source(Decisions(10), ProblemStatsDocument.Empty, mix, order: ProblemOrder.AsRead);

        Assert.Equal(Enumerable.Range(0, 10), await IdsAsync(source));
    }

    [Fact]
    public async Task SingleDecision_ComposesAOneItemQuiz()
    {
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)]);
        var source = Source(Decisions(1), ProblemStatsDocument.Empty, mix);

        Assert.Equal([0], await IdsAsync(source));
        Assert.Equal(1, source.LastComposition!.DrawnCount);
    }

    [Fact]
    public async Task ActiveMix_DedupesByDecisionId_FirstOccurrenceWins()
    {
        var items = new[] { Decision(0), Decision(0), Decision(1) };
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)]);
        var source = Source(items, ProblemStatsDocument.Empty, mix, order: ProblemOrder.AsRead);

        Assert.Equal([0, 1], await IdsAsync(source));
    }

    [Fact]
    public async Task UnmatchedDecisions_AreUnreachableWithoutEverythingElse()
    {
        // 0 is wrong; 1 is unseen; 2 was answered correctly. Only GotWrong is
        // selected, so 1 and 2 match no selected entry and are never drawn.
        var doc = Doc(SeenWrong(0), SeenCorrect(2));
        var mix = new QuizMix([Entry(QuizCategory.GotWrong, 100)]);
        var source = Source(Decisions(3), doc, mix, order: ProblemOrder.AsRead);

        Assert.Equal([0], await IdsAsync(source));
    }

    [Fact]
    public async Task EverythingElse_CollectsExactlyTheUnmatched()
    {
        // 0 is wrong (matched by GotWrong); 1 is unseen and 2 seen-correct —
        // matched by no non-residual entry, so both land in the residual pool.
        var doc = Doc(SeenWrong(0), SeenCorrect(2));
        var mix = new QuizMix(
            [Entry(QuizCategory.GotWrong, 50), Entry(QuizCategory.EverythingElse, 50)]);
        var source = Source(Decisions(3), doc, mix, order: ProblemOrder.AsRead);

        Assert.Equal([0, 1, 2], await IdsAsync(source));

        var report = source.LastComposition!;
        Assert.Equal(3, report.DrawnCount);
        Assert.Equal(1, report.Entries[0].PoolSize);   // GotWrong: {0}
        Assert.Equal(2, report.Entries[1].PoolSize);   // EverythingElse: {1, 2}
        Assert.Equal(1, report.Entries[0].Drawn);
        Assert.Equal(2, report.Entries[1].Drawn);
    }

    [Fact]
    public async Task OverlappingCategories_PresentADecisionAtMostOnce()
    {
        // Decision 0 is both wrong and stale — reachable via both entries,
        // drawn once (global dedupe).
        var doc = Doc(SeenWrong(0, daysAgo: 40), SeenCorrect(1, daysAgo: 40));
        var mix = new QuizMix(
            [Entry(QuizCategory.GotWrong, 50), Entry(QuizCategory.NotSeenInDays(30), 50)]);
        var source = Source(Decisions(2), doc, mix, order: ProblemOrder.AsRead);

        Assert.Equal([0, 1], await IdsAsync(source));
        Assert.Equal(2, source.LastComposition!.DrawnCount);
    }

    [Fact]
    public async Task OverlapContest_GoesToTheEarlierEntry()
    {
        // Same data, both entry orders: 0 is wrong AND stale, 1 only stale.
        // The contested 0 is counted by whichever entry is declared first —
        // entry order is contractual, not incidental.
        var doc = Doc(SeenWrong(0, daysAgo: 40), SeenCorrect(1, daysAgo: 40));
        var items = Decisions(2);

        var wrongFirst = Source(items, doc, new QuizMix(
            [Entry(QuizCategory.GotWrong, 50), Entry(QuizCategory.NotSeenInDays(30), 50)]), order: ProblemOrder.AsRead);
        await IdsAsync(wrongFirst);
        Assert.Equal(1, wrongFirst.LastComposition!.Entries[0].Drawn);   // GotWrong took 0
        Assert.Equal(1, wrongFirst.LastComposition.Entries[1].Drawn);    // stale entry got 1

        var staleFirst = Source(items, doc, new QuizMix(
            [Entry(QuizCategory.NotSeenInDays(30), 50), Entry(QuizCategory.GotWrong, 50)]), order: ProblemOrder.AsRead);
        await IdsAsync(staleFirst);
        Assert.Equal(2, staleFirst.LastComposition!.Entries[0].Drawn);   // stale took 0 AND absorbed the shortfall
        Assert.Equal(0, staleFirst.LastComposition.Entries[1].Drawn);    // GotWrong's only candidate was contested away
    }

    // -----------------------------------------------------------------------
    //  Apportionment, length, shortfall
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Apportionment_UsesLargestRemainder_TiesToEarlierEntry()
    {
        // 34/33/33 of 10 → floors 3/3/3, leftover 1 → largest remainder (34).
        // All three categories match every unseen decision, so pools are full
        // and the requested counts are served exactly.
        var mix = new QuizMix(
        [
            Entry(QuizCategory.NeverSeen, 34),
            Entry(QuizCategory.SeenFewerThan(1), 33),
            Entry(QuizCategory.NotSeenInDays(1), 33),
        ], quizLength: 10);
        var source = Source(Decisions(12), ProblemStatsDocument.Empty, mix, order: ProblemOrder.AsRead);

        var ids = await IdsAsync(source);

        Assert.Equal(Enumerable.Range(0, 10), ids);
        var report = source.LastComposition!;
        Assert.Equal([4, 3, 3], report.Entries.Select(e => e.Requested));
        Assert.Equal([4, 3, 3], report.Entries.Select(e => e.Drawn));
        Assert.Equal(report.TargetCount, report.Entries.Sum(e => e.Requested));
    }

    [Fact]
    public async Task QuizLength_CapsTheComposition()
    {
        var mix = new QuizMix(
            [Entry(QuizCategory.NeverSeen, 100)], quizLength: 4);
        var source = Source(Decisions(10), ProblemStatsDocument.Empty, mix, order: ProblemOrder.AsRead);

        Assert.Equal([0, 1, 2, 3], await IdsAsync(source));
    }

    [Fact]
    public async Task QuizLengthBeyondSupply_DrawsEverythingReachable_AndReportsIt()
    {
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)], quizLength: 10);
        var source = Source(Decisions(3), ProblemStatsDocument.Empty, mix);

        var ids = await IdsAsync(source);

        Assert.Equal(3, ids.Count);
        Assert.Equal(10, source.LastComposition!.TargetCount);
        Assert.Equal(3, source.LastComposition.DrawnCount);
        Assert.Equal(10, source.LastComposition.Entries[0].Requested);
        Assert.Equal(3, source.LastComposition.Entries[0].Drawn);
    }

    /// <summary>
    /// halheinrich/backgammon#12: the capped/capless split is the producer's
    /// to state. The load-bearing case is the third one — a capped mix asking
    /// for exactly the union count — where every count on the report matches
    /// the capless composition above it and only this flag tells them apart.
    /// </summary>
    [Fact]
    public async Task HasRequestedLength_RecordsWhereTheTargetCameFrom()
    {
        var capless = Source(
            Decisions(3), ProblemStatsDocument.Empty,
            new QuizMix([Entry(QuizCategory.NeverSeen, 100)]));
        await IdsAsync(capless);

        Assert.False(capless.LastComposition!.HasRequestedLength);
        Assert.Equal(3, capless.LastComposition.TargetCount);   // the reachable union

        var capped = Source(
            Decisions(3), ProblemStatsDocument.Empty,
            new QuizMix([Entry(QuizCategory.NeverSeen, 100)], quizLength: 2));
        await IdsAsync(capped);

        Assert.True(capped.LastComposition!.HasRequestedLength);
        Assert.Equal(2, capped.LastComposition.TargetCount);

        // Capped at exactly what the union supplies: counts identical to the
        // capless report, split visible only here.
        var cappedAtUnion = Source(
            Decisions(3), ProblemStatsDocument.Empty,
            new QuizMix([Entry(QuizCategory.NeverSeen, 100)], quizLength: 3));
        await IdsAsync(cappedAtUnion);

        Assert.True(cappedAtUnion.LastComposition!.HasRequestedLength);
        Assert.Equal(
            (capless.LastComposition.TargetCount, capless.LastComposition.DrawnCount),
            (cappedAtUnion.LastComposition.TargetCount, cappedAtUnion.LastComposition.DrawnCount));
    }

    [Fact]
    public async Task Shortfall_RedistributesToEntriesWithSupply()
    {
        // Two never-seen, eight wrong; asked for 5 + 5. The never-seen pool
        // dries up at 2, and the deficit of 3 tops up GotWrong.
        var doc = Doc(Enumerable.Range(2, 8).Select(n => SeenWrong(n)).ToArray());
        var mix = new QuizMix(
            [Entry(QuizCategory.NeverSeen, 50), Entry(QuizCategory.GotWrong, 50)],
            quizLength: 10);
        var source = Source(Decisions(10), doc, mix, order: ProblemOrder.AsRead);

        var ids = await IdsAsync(source);

        Assert.Equal(Enumerable.Range(0, 10), ids);
        var report = source.LastComposition!;
        Assert.Equal(10, report.DrawnCount);
        Assert.Equal(new MixCompositionEntry(QuizCategory.NeverSeen, 50, 2, 5, 2), report.Entries[0]);
        Assert.Equal(new MixCompositionEntry(QuizCategory.GotWrong, 50, 8, 5, 8), report.Entries[1]);
    }

    // -----------------------------------------------------------------------
    //  Shuffled, as read & determinism
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SameSeed_SameInputs_YieldIdenticalCompositions()
    {
        var items = Decisions(30);
        var doc = Doc(Enumerable.Range(0, 15).Select(n => SeenWrong(n)).ToArray());
        var mix = new QuizMix(
            [Entry(QuizCategory.GotWrong, 50), Entry(QuizCategory.EverythingElse, 50)],
            quizLength: 10);

        var a = await IdsAsync(Source(items, doc, mix, seed: 42));
        var b = await IdsAsync(Source(items, doc, mix, seed: 42));

        Assert.Equal(a, b);
    }

    [Fact]
    public async Task Restart_DrawsAfresh()
    {
        // Re-enumerating the same instance re-composes and re-shuffles — a
        // Restart is a new draw, not a replay (the shuffled-source precedent).
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)], quizLength: 10);
        var source = Source(Decisions(30), ProblemStatsDocument.Empty, mix);

        var first = await IdsAsync(source);
        var second = await IdsAsync(source);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task AsRead_IsFullyDeterministic_WithoutASeed()
    {
        // Source order drives both draws and presentation; the RNG is never
        // consumed, so even an unseeded instance is exactly reproducible.
        var doc = Doc(SeenCorrect(0), SeenCorrect(2), SeenCorrect(4));
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)]);
        var source = new MixedProblemSetSource(
            new InMemorySource("Test", Decisions(6)), ProblemOrder.AsRead, PlayRanking.Equity, mix, () => doc, Clock);

        Assert.Equal([1, 3, 5], await IdsAsync(source));
        Assert.Equal([1, 3, 5], await IdsAsync(source));
    }

    [Fact]
    public async Task Shuffled_ShufflesPresentation()
    {
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)]);
        var source = Source(Decisions(30), ProblemStatsDocument.Empty, mix, seed: 7);

        var ids = await IdsAsync(source);

        Assert.Equal(Enumerable.Range(0, 30), ids.Order());
        Assert.NotEqual(Enumerable.Range(0, 30), ids);
    }

    // -----------------------------------------------------------------------
    //  Provider seam
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ProviderIsResolvedFreshPerEnumeration()
    {
        // The consumer's document advances between passes (folds replace the
        // immutable document); a Restart composes against the current one.
        var items = Decisions(3);
        var doc = ProblemStatsDocument.Empty;
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)]);
        var source = new MixedProblemSetSource(
            new InMemorySource("Test", items), ProblemOrder.AsRead, PlayRanking.Equity, mix, () => doc, Clock, seed: 7);

        Assert.Equal([0, 1, 2], await IdsAsync(source));

        doc = Doc(SeenCorrect(0), SeenCorrect(1), SeenCorrect(2));

        Assert.Empty(await IdsAsync(source));
    }

    [Fact]
    public async Task ProviderReturningNull_FailsFast()
    {
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)]);
        var source = new MixedProblemSetSource(
            new InMemorySource("Test", Decisions(3)), ProblemOrder.Shuffled, PlayRanking.Equity, mix, () => null!, Clock, seed: 7);

        await Assert.ThrowsAsync<InvalidOperationException>(() => IdsAsync(source));
    }

    // -----------------------------------------------------------------------
    //  Telemetry availability & lifecycle
    // -----------------------------------------------------------------------

    [Fact]
    public async Task LastComposition_IsAvailableAtTheFirstItem()
    {
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)]);
        var source = Source(Decisions(5), ProblemStatsDocument.Empty, mix);

        Assert.Null(source.LastComposition);

        await foreach (var _ in source.EnumerateAsync())
        {
            Assert.NotNull(source.LastComposition);
            break;
        }
    }

    // -----------------------------------------------------------------------
    //  Classification by content identity (the halheinrich/backgammon#95
    //  mis-classification pin): the classifier judges the problem's full
    //  record via the drawn item's ProblemKey, wherever the copy came from.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Classification_JudgesACopyByTheProblemsRecord_NotItsOwnId()
    {
        // The lifetime record was earned quizzing SOME copy of problem 0; the
        // source holds a content-equal copy under a different DecisionId.
        // GotWrong must still claim it — under v1's id-keyed stats this copy
        // carried no record and fell out of the pool (the halheinrich/backgammon#95 defect).
        var doc = Doc(SeenWrong(0));
        var mix = new QuizMix([Entry(QuizCategory.GotWrong, 100)]);
        var source = Source([Copy(0, "other-file.xgp"), Decision(1)], doc, mix, order: ProblemOrder.AsRead);

        Assert.Equal([0], await IdsAsync(source));
    }

    [Fact]
    public async Task Classification_ACopyOfAProblemAnsweredCorrectly_IsNotGotWrong()
    {
        // The absent half of the pair above: a correct-only record excludes
        // every copy of the problem, whichever id is drawn.
        var doc = Doc(SeenCorrect(0));
        var mix = new QuizMix([Entry(QuizCategory.GotWrong, 100)]);
        var source = Source([Copy(0, "other-file.xgp"), Decision(1)], doc, mix, order: ProblemOrder.AsRead);

        Assert.Empty(await IdsAsync(source));
    }

    [Fact]
    public async Task Classification_SightingsReadTheProblemsFullRecord_AcrossCopies()
    {
        // One sighting lives on the problem's record; the drawn item is a
        // different-id copy. SeenFewerThan(2) still sees supply (1 < 2);
        // SeenFewerThan(1) sees none (1 sighting is not fewer than 1) — the
        // present/absent pair over the same fixture.
        var doc = Doc(SeenCorrect(0));
        var items = new[] { Copy(0, "other-file.xgp") };

        var underTwo = Source(items, doc, new QuizMix(
            [Entry(QuizCategory.SeenFewerThan(2), 100)]), order: ProblemOrder.AsRead);
        Assert.Equal([0], await IdsAsync(underTwo));

        var underOne = Source(items, doc, new QuizMix(
            [Entry(QuizCategory.SeenFewerThan(1), 100)]), order: ProblemOrder.AsRead);
        Assert.Empty(await IdsAsync(underOne));
    }

    [Fact]
    public async Task AverageLoss_JudgesACubeProblemByItsLossPerAnswer()
    {
        // Cube problem 5 was answered once and lost 0.30 over its two halves:
        // 0.15 per half, 0.30 per answer. At a 0.15 threshold it is drawn —
        // its loss per answer is over it, though its per-half average is not.
        var cube = TestRecords.Cube(id: Id(5), position: PositionOf(5));
        var doc = Doc(new ProblemStats(ProblemKey.From(cube), new ScoreSegment(2, 1, 0.30), Now));
        var mix = new QuizMix([Entry(QuizCategory.AvgEquityLossOver(0.15), 100)]);
        var source = Source([cube, Decision(2)], doc, mix, order: ProblemOrder.AsRead);

        Assert.Equal([5], await IdsAsync(source));
    }

    [Fact]
    public async Task ContentEqualCopies_AreDistinctRecordsHere_BothClassified()
    {
        // The enumeration dedupe is record identity (DecisionId), not content
        // identity: collapsing content-equal copies is the distinct layer's
        // job and the consumer's wiring choice. Both copies share one record
        // and both classify GotWrong.
        var doc = Doc(SeenWrong(0));
        var mix = new QuizMix([Entry(QuizCategory.GotWrong, 100)]);
        var source = Source([Decision(0), Copy(0, "other-file.xgp")], doc, mix, order: ProblemOrder.AsRead);

        Assert.Equal([0, 0], await IdsAsync(source));
    }

    // =======================================================================
    //  The order (SPEC-filtering.md §5 "The mix selects; the order presents",
    //  §7). The composer applies it exactly once and decides which
    //  application it gets: the plain key for a blank mix, the mixed key for
    //  an active one.
    // =======================================================================

    /// <summary>
    /// Decision <paramref name="n"/> (its own problem — see
    /// <see cref="Decision"/>), played as the candidate at
    /// <paramref name="userPlayIndex"/> (0 the best, error 0; 1 errs 0.1731;
    /// 2 errs 0.1813), or with no index as an unlisted play of
    /// <paramref name="unlistedPlayError"/>, or as no recorded play at all; at
    /// <paramref name="move"/> of an .xg match, or with no move number — a
    /// position file's — when none is given.
    /// </summary>
    private static BgDecisionData PlayOf(
        int n, int? userPlayIndex = 0, double? unlistedPlayError = null, int? move = null) =>
        TestRecords.CheckerPlay(
            id: move is int m ? new XgDecisionId($"d{n}.xg", Game: 1, MoveNumber: m, IsCube: false) : Id(n),
            position: PositionOf(n),
            decision: TestRecords.CheckerPlayData(userPlayIndex: userPlayIndex, unlistedPlayError: unlistedPlayError));

    /// <summary>A lifetime record of <paramref name="decision"/>'s problem: <paramref name="submitted"/> wrong, <paramref name="loss"/> lost.</summary>
    private static ProblemStats RecordOf(BgDecisionData decision, int submitted, double loss) =>
        new(ProblemKey.From(decision), new ScoreSegment(submitted, 0, loss), Now - TimeSpan.FromDays(1));

    /// <summary>A stats provider that counts its calls.</summary>
    private sealed class CountingProvider(ProblemStatsDocument document)
    {
        public int Calls { get; private set; }

        public ProblemStatsDocument Get()
        {
            Calls++;
            return document;
        }
    }

    private static readonly QuizMix Everything = new([Entry(QuizCategory.EverythingElse, 100)]);

    // -----------------------------------------------------------------------
    //  A blank mix disables composition, never the order
    // -----------------------------------------------------------------------

    /// <summary>
    /// Decisions over which every order differs from source order: errors,
    /// move numbers and a position file's numberless decision, mixed.
    /// </summary>
    private static IReadOnlyList<BgDecisionData> Varied() =>
    [
        PlayOf(1, userPlayIndex: 1, move: 7),
        PlayOf(2, userPlayIndex: 0, move: 2),
        PlayOf(3, userPlayIndex: null, unlistedPlayError: 0.3),
        PlayOf(4, userPlayIndex: 2, move: 5),
        PlayOf(5, userPlayIndex: 1, move: 1),
        PlayOf(6, userPlayIndex: 0, move: 3),
    ];

    public static TheoryData<ProblemOrder> EveryOrder => new(Enum.GetValues<ProblemOrder>());

    [Theory]
    [MemberData(nameof(EveryOrder))]
    public async Task BlankMix_PresentsInTheOrder_AsThePlainDecoratorDoes(ProblemOrder order)
    {
        var provider = new CountingProvider(Doc(RecordOf(Varied()[1], 1, 0.9)));
        var composer = new MixedProblemSetSource(
            new InMemorySource("Test", Varied()), order, PlayRanking.Equity, QuizMix.Empty, provider.Get, Clock, seed: 11);
        var plain = new OrderedProblemSetSource(
            new InMemorySource("Test", Varied()), order, PlayRanking.Equity, seed: 11);

        Assert.Equal(await IdsAsync(plain), await IdsAsync(composer));
        Assert.Null(composer.LastComposition);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task BlankMix_ByError_OrdersByXgsError_NeverTheStatsDocument()
    {
        // Decision 2 played the best move (XG's error 0) but its record has
        // lost 0.9 an answer. A blank mix reads XG's error, so it stays last —
        // and the provider is never called.
        var items = Varied();
        var provider = new CountingProvider(Doc(RecordOf(items[1], 1, 0.9)));
        var source = new MixedProblemSetSource(
            new InMemorySource("Test", items), ProblemOrder.ByErrorLargestFirst, PlayRanking.Equity,
            QuizMix.Empty, provider.Get, Clock);

        // 3: 0.3 unstated; 4: 0.1813; 1 and 5: 0.1731, in source order; 2 and 6: 0.
        Assert.Equal([3, 4, 1, 5, 2, 6], await IdsAsync(source));
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task BlankMix_Shuffled_Shuffles_WithoutCallingTheStatsProvider()
    {
        var provider = new CountingProvider(ProblemStatsDocument.Empty);
        var source = new MixedProblemSetSource(
            new InMemorySource("Test", Decisions(30)), ProblemOrder.Shuffled, PlayRanking.Equity,
            QuizMix.Empty, provider.Get, Clock, seed: 7);

        var ids = await IdsAsync(source);

        Assert.Equal(Enumerable.Range(0, 30), ids.Order());
        Assert.NotEqual(Enumerable.Range(0, 30), ids);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task BlankMix_NeedsNoStatsProvider()
    {
        // No stats document, so no provider: a blank mix still runs, in its order.
        var source = new MixedProblemSetSource(
            new InMemorySource("Test", Varied()), ProblemOrder.ByMoveNumberAscending, PlayRanking.Equity,
            QuizMix.Empty, statsProvider: null, Clock);

        Assert.Equal([5, 2, 6, 4, 1, 3], await IdsAsync(source));
        Assert.Equal(6, source.Count);
    }

    [Fact]
    public void ActiveMix_WithNoStatsProvider_IsAConsumerError()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new MixedProblemSetSource(
            new InMemorySource("Test", Decisions(3)), ProblemOrder.AsRead, PlayRanking.Equity,
            Everything, statsProvider: null, Clock));

        Assert.Equal("statsProvider", ex.ParamName);
    }

    // -----------------------------------------------------------------------
    //  By error with an active mix — the mixed key
    // -----------------------------------------------------------------------

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
        var source = Source(
            [played1, played2, played3, played4, played5, played6, cube7], doc, Everything,
            order: ProblemOrder.ByErrorLargestFirst);

        // A cube's two halves are summed: per half its 0.11 would fall below 3.
        // 5 and 6 tie at 0.0 and keep source order.
        Assert.Equal([2, 4, 7, 3, 1, 5, 6], await IdsAsync(source));
    }

    [Fact]
    public async Task ActiveMix_ByError_FillsEachRowFromTheFrontOfItsOrderedSet()
    {
        // Four wrong, losing 0.1, 0.4, 0.2 and 0.3 an answer; GotWrong may
        // take two. By error takes the two largest and presents them largest
        // first; as read takes the first two in source order.
        var items = new[] { PlayOf(1), PlayOf(2), PlayOf(3), PlayOf(4) };
        var doc = Doc(
            RecordOf(items[0], 1, 0.1), RecordOf(items[1], 1, 0.4),
            RecordOf(items[2], 1, 0.2), RecordOf(items[3], 1, 0.3));
        var mix = new QuizMix([Entry(QuizCategory.GotWrong, 100)], quizLength: 2);

        Assert.Equal([2, 4], await IdsAsync(Source(items, doc, mix, order: ProblemOrder.ByErrorLargestFirst)));
        Assert.Equal([1, 2], await IdsAsync(Source(items, doc, mix, order: ProblemOrder.AsRead)));
    }

    [Fact]
    public async Task ActiveMix_ByError_TheStatsProviderIsCalledOnce_ItsDocumentServesBothPasses()
    {
        // The key is read from the document the composition classified
        // against: one document per enumeration.
        var items = new[] { PlayOf(1), PlayOf(2) };
        var provider = new CountingProvider(Doc(RecordOf(items[0], 1, 0.1), RecordOf(items[1], 1, 0.2)));
        var source = new MixedProblemSetSource(
            new InMemorySource("Test", items), ProblemOrder.ByErrorLargestFirst, PlayRanking.Equity,
            new QuizMix([Entry(QuizCategory.GotWrong, 100)]), provider.Get, Clock);

        Assert.Equal([2, 1], await IdsAsync(source));
        Assert.Equal(1, provider.Calls);
    }

    // -----------------------------------------------------------------------
    //  By move number with an active mix — and the second pass
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ActiveMix_SortsTheWholeSelectionAgain_AcrossRows()
    {
        // Never seen at moves 9, 1, 5; got wrong at moves 2, 8, 3; two each.
        // Each row fills from the front of its set by move number (1, 5 and
        // 2, 3), and the selection is then sorted again as a whole — not
        // presented row by row.
        var neverSeen = new[] { PlayOf(1, move: 9), PlayOf(2, move: 1), PlayOf(3, move: 5) };
        var gotWrong = new[] { PlayOf(4, move: 2), PlayOf(5, move: 8), PlayOf(6, move: 3) };
        var doc = Doc(gotWrong.Select(d => RecordOf(d, 1, 0.1)).ToArray());
        var mix = new QuizMix(
            [Entry(QuizCategory.NeverSeen, 50), Entry(QuizCategory.GotWrong, 50)], quizLength: 4);

        var source = Source([.. neverSeen, .. gotWrong], doc, mix, order: ProblemOrder.ByMoveNumberAscending);

        Assert.Equal([2, 4, 6, 3], await IdsAsync(source));
    }

    [Fact]
    public async Task ActiveMix_ByMoveNumber_ADecisionWithNoMoveNumberGoesLast_InSourceOrder()
    {
        var items = new[] { PlayOf(1), PlayOf(2, move: 6), PlayOf(3), PlayOf(4, move: 2) };

        var source = Source(items, ProblemStatsDocument.Empty, Everything, order: ProblemOrder.ByMoveNumberAscending);

        Assert.Equal([4, 2, 1, 3], await IdsAsync(source));
    }

    // -----------------------------------------------------------------------
    //  Construction & cancellation
    // -----------------------------------------------------------------------

    [Fact]
    public void Constructor_RejectsNulls()
    {
        var inner = new InMemorySource("Test", Decisions(1));
        var active = new QuizMix([Entry(QuizCategory.NeverSeen, 100)]);
        static ProblemStatsDocument Stats() => ProblemStatsDocument.Empty;

        Assert.Throws<ArgumentNullException>(
            () => new MixedProblemSetSource(null!, ProblemOrder.AsRead, PlayRanking.Equity, QuizMix.Empty, Stats, Clock));
        Assert.Throws<ArgumentNullException>(
            () => new MixedProblemSetSource(inner, ProblemOrder.AsRead, PlayRanking.Equity, active, null, Clock));
        Assert.Throws<ArgumentNullException>(
            () => new MixedProblemSetSource(inner, ProblemOrder.AsRead, PlayRanking.Equity, null!, Stats, Clock));
        Assert.Throws<ArgumentNullException>(
            () => new MixedProblemSetSource(inner, ProblemOrder.AsRead, PlayRanking.Equity, QuizMix.Empty, Stats, null!));
    }

    [Fact]
    public void Constructor_RejectsAnUndefinedOrderOrRanking()
    {
        var inner = new InMemorySource("Test", Decisions(1));
        static ProblemStatsDocument Stats() => ProblemStatsDocument.Empty;

        Assert.Equal("order", Assert.Throws<ArgumentOutOfRangeException>(
            () => new MixedProblemSetSource(inner, (ProblemOrder)99, PlayRanking.Equity, QuizMix.Empty, Stats, Clock)).ParamName);
        Assert.Equal("ranking", Assert.Throws<ArgumentOutOfRangeException>(
            () => new MixedProblemSetSource(inner, ProblemOrder.AsRead, (PlayRanking)99, QuizMix.Empty, Stats, Clock, seed: 1)).ParamName);
    }

    [Fact]
    public async Task EnumerateAsync_HonoursCancellation()
    {
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)]);
        var source = Source(Decisions(10), ProblemStatsDocument.Empty, mix);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => IdsAsync(source, cts.Token));
    }
}
