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

    private static BgDecisionData Decision(int n) => new()
    {
        Id = Id(n),
        Position = new PositionData
        {
            Mop = new int[26],
            OnRollNeeds = n,
            CubeOwner = CubeOwner.Centered,
        },
        Decision = new DecisionData(),
        Descriptive = new DescriptiveData(),
        Outcome = new PlayOutcomeData(),
    };

    private static IReadOnlyList<BgDecisionData> Decisions(int count) =>
        Enumerable.Range(0, count).Select(Decision).ToList();

    private static DecisionStats SeenCorrect(int n, int daysAgo = 1) =>
        new(Id(n), new ScoreSegment(1, 1, 0.0), Now - TimeSpan.FromDays(daysAgo));

    private static DecisionStats SeenWrong(int n, int daysAgo = 1) =>
        new(Id(n), new ScoreSegment(1, 0, 0.1), Now - TimeSpan.FromDays(daysAgo));

    private static DecisionStatsDocument Doc(params DecisionStats[] stats) =>
        DecisionStatsDocument.FromStats(stats);

    private static QuizMixEntry Entry(QuizCategory category, int percent) =>
        new(category, percent);

    private static MixedProblemSetSource Source(
        IReadOnlyList<BgDecisionData> items,
        DecisionStatsDocument doc,
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
        var source = Source(items, DecisionStatsDocument.Empty, QuizMix.Empty);

        var ids = await IdsAsync(source);

        Assert.Equal(Enumerable.Range(0, 10), ids);
        Assert.Null(source.LastComposition);
    }

    [Fact]
    public async Task BlankMix_DoesNotDedupe()
    {
        // Inert means inert: even duplicate ids stream through unchanged.
        var items = new[] { Decision(0), Decision(0), Decision(1) };
        var source = Source(items, DecisionStatsDocument.Empty, QuizMix.Empty);

        Assert.Equal([0, 0, 1], await IdsAsync(source));
    }

    [Fact]
    public void Count_PassesThroughOnlyForBlankMix()
    {
        var items = Decisions(10);

        Assert.Equal(10, Source(items, DecisionStatsDocument.Empty, QuizMix.Empty).Count);
        Assert.Null(Source(
            items, DecisionStatsDocument.Empty,
            new QuizMix([Entry(QuizCategory.NeverSeen, 100)])).Count);
    }

    [Fact]
    public void Name_PassesThrough()
    {
        var source = Source(Decisions(1), DecisionStatsDocument.Empty, QuizMix.Empty);

        Assert.Equal("Test", source.Name);
    }

    // -----------------------------------------------------------------------
    //  Composition basics
    // -----------------------------------------------------------------------

    [Fact]
    public async Task EmptyInnerSource_YieldsNothing_WithHonestTelemetry()
    {
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)], quizLength: 5);
        var source = Source([], DecisionStatsDocument.Empty, mix);

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
        var source = Source(Decisions(10), DecisionStatsDocument.Empty, mix);

        Assert.Equal(Enumerable.Range(0, 10), await IdsAsync(source));
    }

    [Fact]
    public async Task SingleDecision_ComposesAOneItemQuiz()
    {
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)]);
        var source = Source(Decisions(1), DecisionStatsDocument.Empty, mix);

        Assert.Equal([0], await IdsAsync(source));
        Assert.Equal(1, source.LastComposition!.DrawnCount);
    }

    [Fact]
    public async Task ActiveMix_DedupesByDecisionId_FirstOccurrenceWins()
    {
        var items = new[] { Decision(0), Decision(0), Decision(1) };
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)], randomOrder: false);
        var source = Source(items, DecisionStatsDocument.Empty, mix);

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
        var source = Source(Decisions(12), DecisionStatsDocument.Empty, mix);

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
        var source = Source(Decisions(10), DecisionStatsDocument.Empty, mix);

        Assert.Equal([0, 1, 2, 3], await IdsAsync(source));
    }

    [Fact]
    public async Task QuizLengthBeyondSupply_DrawsEverythingReachable_AndReportsIt()
    {
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)], quizLength: 10);
        var source = Source(Decisions(3), DecisionStatsDocument.Empty, mix);

        var ids = await IdsAsync(source);

        Assert.Equal(3, ids.Count);
        Assert.Equal(10, source.LastComposition!.TargetCount);
        Assert.Equal(3, source.LastComposition.DrawnCount);
        Assert.Equal(10, source.LastComposition.Entries[0].Requested);
        Assert.Equal(3, source.LastComposition.Entries[0].Drawn);
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
        var source = Source(Decisions(30), DecisionStatsDocument.Empty, mix);

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
        var source = Source(Decisions(30), DecisionStatsDocument.Empty, mix, seed: 7);

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
        var doc = DecisionStatsDocument.Empty;
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
        var source = Source(Decisions(5), DecisionStatsDocument.Empty, mix);

        Assert.Null(source.LastComposition);

        await foreach (var _ in source.EnumerateAsync())
        {
            Assert.NotNull(source.LastComposition);
            break;
        }
    }

    // -----------------------------------------------------------------------
    //  Construction & cancellation
    // -----------------------------------------------------------------------

    [Fact]
    public void Constructor_RejectsNulls()
    {
        var inner = new InMemorySource("Test", Decisions(1));
        static DecisionStatsDocument Stats() => DecisionStatsDocument.Empty;

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
        var source = Source(Decisions(10), DecisionStatsDocument.Empty, mix);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => IdsAsync(source, cts.Token));
    }
}
