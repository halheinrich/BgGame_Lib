namespace BgGame_Lib.Tests;

using System.Runtime.CompilerServices;
using BgDataTypes_Lib;

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
    /// must be real and key-derivable — a standard starting board with
    /// stamped dice, distinguished (and read back by <c>IdsAsync</c>) via the
    /// away-scores pair <c>n</c>-away/<c>n</c>-away (<c>0a0</c> = money).
    ///
    /// <para>
    /// <c>n == 0</c> is therefore a <b>money</b> fixture, and money is the one
    /// score the key grammar spells the Jacoby rule for — an unstamped money
    /// record has no key at all (the no-key rung), so this fixture must say
    /// which rule it means. It means Jacoby on; the value is arbitrary here,
    /// the stamp is not. Off money the fact is meaningless, so match fixtures
    /// stay unstamped rather than carrying noise.
    /// </para>
    /// </summary>
    private static BgDecisionData Decision(int n) => new()
    {
        Id = Id(n),
        Position = new PositionData
        {
            Mop = [0, -2, 0, 0, 0, 0, 5, 0, 3, 0, 0, 0, -5, 5, 0, 0, 0, -3, 0, -5, 0, 0, 0, 0, 2, 0],
            OnRollNeeds = n,
            OpponentNeeds = n,
            CubeOwner = CubeOwner.Centered,
            IsJacoby = n == 0 ? true : null,
        },
        Decision = new DecisionData { Dice = [3, 1] },
        Descriptive = new DescriptiveData(),
        Outcome = new PlayOutcomeData(),
    };

    private static IReadOnlyList<BgDecisionData> Decisions(int count) =>
        Enumerable.Range(0, count).Select(Decision).ToList();

    /// <summary>
    /// A content-equal copy of decision <paramref name="n"/> under a
    /// different file-relative identity — same <see cref="ProblemKey"/>,
    /// different <see cref="DecisionId"/>.
    /// </summary>
    private static BgDecisionData Copy(int n, string file) =>
        new()
        {
            Id = new XgpDecisionId(file),
            Position = Decision(n).Position,
            Decision = Decision(n).Decision,
            Descriptive = new DescriptiveData(),
            Outcome = new PlayOutcomeData(),
        };

    /// <summary>
    /// A decision with real position facts but unstamped dice: no derivable
    /// <see cref="ProblemKey"/> — the no-key rung.
    /// </summary>
    private static BgDecisionData NoKeyDecision(int n) =>
        new()
        {
            Id = Id(n),
            Position = Decision(n).Position,
            Decision = new DecisionData(),   // dice default to {0,0}: underivable
            Descriptive = new DescriptiveData(),
            Outcome = new PlayOutcomeData(),
        };

    /// <summary>Decision <paramref name="n"/>'s content key, via the one blessed factory.</summary>
    private static ProblemKey Key(int n)
    {
        Assert.True(ProblemKey.TryDerive(Decision(n), out var key));
        return key!;
    }

    private static ProblemStats SeenCorrect(int n, int daysAgo = 1) =>
        new(Key(n), new ScoreSegment(1, 1, 0.0), Now - TimeSpan.FromDays(daysAgo));

    private static ProblemStats SeenWrong(int n, int daysAgo = 1) =>
        new(Key(n), new ScoreSegment(1, 0, 0.1), Now - TimeSpan.FromDays(daysAgo));

    private static ProblemStatsDocument Doc(params ProblemStats[] stats) =>
        ProblemStatsDocument.FromStats(stats);

    private static QuizMixEntry Entry(QuizCategory category, int percent) =>
        new(category, percent);

    private static MixedProblemSetSource Source(
        IReadOnlyList<BgDecisionData> items,
        ProblemStatsDocument doc,
        QuizMix mix,
        int seed = 7) =>
        new(new InMemorySource("Test", items), () => doc, mix, Clock, seed);

    private static async Task<List<int>> IdsAsync(
        IProblemSetSource source, CancellationToken ct = default)
    {
        var ids = new List<int>();
        await foreach (var item in source.EnumerateAsync(ct))
            ids.Add(item.Position.OnRollNeeds);
        return ids;
    }

    // -----------------------------------------------------------------------
    //  Passthrough (blank mix)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BlankMix_PassesEverythingThroughInSourceOrder()
    {
        var items = Decisions(10);
        var source = Source(items, ProblemStatsDocument.Empty, QuizMix.Empty);

        var ids = await IdsAsync(source);

        Assert.Equal(Enumerable.Range(0, 10), ids);
        Assert.Null(source.LastComposition);
    }

    [Fact]
    public async Task BlankMix_DoesNotDedupe()
    {
        // Inert means inert: even duplicate ids stream through unchanged.
        var items = new[] { Decision(0), Decision(0), Decision(1) };
        var source = Source(items, ProblemStatsDocument.Empty, QuizMix.Empty);

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
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)], randomOrder: false);
        var source = Source(Decisions(10), ProblemStatsDocument.Empty, mix);

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
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)], randomOrder: false);
        var source = Source(items, ProblemStatsDocument.Empty, mix);

        Assert.Equal([0, 1], await IdsAsync(source));
    }

    [Fact]
    public async Task UnmatchedDecisions_AreUnreachableWithoutEverythingElse()
    {
        // 0 is wrong; 1 is unseen; 2 was answered correctly. Only GotWrong is
        // selected, so 1 and 2 match no selected entry and are never drawn.
        var doc = Doc(SeenWrong(0), SeenCorrect(2));
        var mix = new QuizMix([Entry(QuizCategory.GotWrong, 100)], randomOrder: false);
        var source = Source(Decisions(3), doc, mix);

        Assert.Equal([0], await IdsAsync(source));
    }

    [Fact]
    public async Task EverythingElse_CollectsExactlyTheUnmatched()
    {
        // 0 is wrong (matched by GotWrong); 1 is unseen and 2 seen-correct —
        // matched by no non-residual entry, so both land in the residual pool.
        var doc = Doc(SeenWrong(0), SeenCorrect(2));
        var mix = new QuizMix(
            [Entry(QuizCategory.GotWrong, 50), Entry(QuizCategory.EverythingElse, 50)],
            randomOrder: false);
        var source = Source(Decisions(3), doc, mix);

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
            [Entry(QuizCategory.GotWrong, 50), Entry(QuizCategory.NotSeenInDays(30), 50)],
            randomOrder: false);
        var source = Source(Decisions(2), doc, mix);

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
            [Entry(QuizCategory.GotWrong, 50), Entry(QuizCategory.NotSeenInDays(30), 50)],
            randomOrder: false));
        await IdsAsync(wrongFirst);
        Assert.Equal(1, wrongFirst.LastComposition!.Entries[0].Drawn);   // GotWrong took 0
        Assert.Equal(1, wrongFirst.LastComposition.Entries[1].Drawn);    // stale entry got 1

        var staleFirst = Source(items, doc, new QuizMix(
            [Entry(QuizCategory.NotSeenInDays(30), 50), Entry(QuizCategory.GotWrong, 50)],
            randomOrder: false));
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
        ], quizLength: 10, randomOrder: false);
        var source = Source(Decisions(12), ProblemStatsDocument.Empty, mix);

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
            [Entry(QuizCategory.NeverSeen, 100)], quizLength: 4, randomOrder: false);
        var source = Source(Decisions(10), ProblemStatsDocument.Empty, mix);

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
            quizLength: 10, randomOrder: false);
        var source = Source(Decisions(10), doc, mix);

        var ids = await IdsAsync(source);

        Assert.Equal(Enumerable.Range(0, 10), ids);
        var report = source.LastComposition!;
        Assert.Equal(10, report.DrawnCount);
        Assert.Equal(new MixCompositionEntry(QuizCategory.NeverSeen, 50, 2, 5, 2), report.Entries[0]);
        Assert.Equal(new MixCompositionEntry(QuizCategory.GotWrong, 50, 8, 5, 8), report.Entries[1]);
    }

    // -----------------------------------------------------------------------
    //  Random toggle & determinism
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
    public async Task RandomOrderOff_IsFullyDeterministic_WithoutASeed()
    {
        // Source order drives both draws and presentation; the RNG is never
        // consumed, so even an unseeded instance is exactly reproducible.
        var doc = Doc(SeenCorrect(0), SeenCorrect(2), SeenCorrect(4));
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)], randomOrder: false);
        var source = new MixedProblemSetSource(
            new InMemorySource("Test", Decisions(6)), () => doc, mix, Clock);

        Assert.Equal([1, 3, 5], await IdsAsync(source));
        Assert.Equal([1, 3, 5], await IdsAsync(source));
    }

    [Fact]
    public async Task RandomOrderOn_ShufflesPresentation()
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
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)], randomOrder: false);
        var source = new MixedProblemSetSource(
            new InMemorySource("Test", items), () => doc, mix, Clock, seed: 7);

        Assert.Equal([0, 1, 2], await IdsAsync(source));

        doc = Doc(SeenCorrect(0), SeenCorrect(1), SeenCorrect(2));

        Assert.Empty(await IdsAsync(source));
    }

    [Fact]
    public async Task ProviderReturningNull_FailsFast()
    {
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)]);
        var source = new MixedProblemSetSource(
            new InMemorySource("Test", Decisions(3)), () => null!, mix, Clock, seed: 7);

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
        // carried no record and fell out of the pool (the #95 defect).
        var doc = Doc(SeenWrong(0));
        var mix = new QuizMix([Entry(QuizCategory.GotWrong, 100)], randomOrder: false);
        var source = Source([Copy(0, "other-file.xgp"), Decision(1)], doc, mix);

        Assert.Equal([0], await IdsAsync(source));
    }

    [Fact]
    public async Task Classification_ACopyOfAProblemAnsweredCorrectly_IsNotGotWrong()
    {
        // The absent half of the pair above: a correct-only record excludes
        // every copy of the problem, whichever id is drawn.
        var doc = Doc(SeenCorrect(0));
        var mix = new QuizMix([Entry(QuizCategory.GotWrong, 100)], randomOrder: false);
        var source = Source([Copy(0, "other-file.xgp"), Decision(1)], doc, mix);

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
            [Entry(QuizCategory.SeenFewerThan(2), 100)], randomOrder: false));
        Assert.Equal([0], await IdsAsync(underTwo));

        var underOne = Source(items, doc, new QuizMix(
            [Entry(QuizCategory.SeenFewerThan(1), 100)], randomOrder: false));
        Assert.Empty(await IdsAsync(underOne));
    }

    [Fact]
    public async Task ContentEqualCopies_AreDistinctRecordsHere_BothClassified()
    {
        // The enumeration dedupe is record identity (DecisionId), not content
        // identity: collapsing content-equal copies is the distinct layer's
        // job and the consumer's wiring choice. Both copies share one record
        // and both classify GotWrong.
        var doc = Doc(SeenWrong(0));
        var mix = new QuizMix([Entry(QuizCategory.GotWrong, 100)], randomOrder: false);
        var source = Source([Decision(0), Copy(0, "other-file.xgp")], doc, mix);

        Assert.Equal([0, 0], await IdsAsync(source));
    }

    [Fact]
    public async Task NoKeyDecision_ClassifiesAsNeverSeen()
    {
        // The no-key rung: an underivable key means no record can exist, so
        // the decision is never-seen to the classifier — reachable through
        // NeverSeen, invisible to record-dependent categories like GotWrong.
        var items = new[] { NoKeyDecision(0), Decision(1) };
        var doc = Doc(SeenWrong(1));

        var neverSeen = Source(items, doc, new QuizMix(
            [Entry(QuizCategory.NeverSeen, 100)], randomOrder: false));
        Assert.Equal([0], await IdsAsync(neverSeen));

        var gotWrong = Source(items, doc, new QuizMix(
            [Entry(QuizCategory.GotWrong, 100)], randomOrder: false));
        Assert.Equal([1], await IdsAsync(gotWrong));
    }

    // -----------------------------------------------------------------------
    //  Construction & cancellation
    // -----------------------------------------------------------------------

    [Fact]
    public void Constructor_RejectsNulls()
    {
        var inner = new InMemorySource("Test", Decisions(1));
        static ProblemStatsDocument Stats() => ProblemStatsDocument.Empty;

        Assert.Throws<ArgumentNullException>(
            () => new MixedProblemSetSource(null!, Stats, QuizMix.Empty, Clock));
        Assert.Throws<ArgumentNullException>(
            () => new MixedProblemSetSource(inner, null!, QuizMix.Empty, Clock));
        Assert.Throws<ArgumentNullException>(
            () => new MixedProblemSetSource(inner, Stats, null!, Clock));
        Assert.Throws<ArgumentNullException>(
            () => new MixedProblemSetSource(inner, Stats, QuizMix.Empty, null!));
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
