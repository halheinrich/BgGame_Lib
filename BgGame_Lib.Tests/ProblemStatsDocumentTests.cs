namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;

public class ProblemStatsDocumentTests
{
    // Canonical key fixtures (the ratified wire grammar over the standard
    // starting board): KeyA a checker play, KeyB a cube decision.
    private const string Board = "0,-2,0,0,0,0,5,0,3,0,0,0,-5,5,0,0,0,-3,0,-5,0,0,0,0,2,0";

    private static readonly ProblemKey KeyA = ProblemKey.Parse($"{Board}/7a7/1c/31");
    private static readonly ProblemKey KeyB = ProblemKey.Parse($"{Board}/5a3/2o");

    private static readonly DateTimeOffset T1 = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Deterministic <see cref="TimeProvider"/>: returns whatever
    /// <see cref="Now"/> is set to — the fold-time seam under test.
    /// </summary>
    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static SubmittedPlay Play(ProblemKey? key, double equityLoss, bool correct) =>
        new(key, UserPlay: [], MatchedCandidateIndex: correct ? 0 : 1,
            EquityLoss: equityLoss, IsCorrect: correct);

    private static SubmittedCubeAction Cube(
        ProblemKey? key, double doublerLoss, bool doublerCorrect, double takerLoss, bool takerCorrect) =>
        new(key, new CubeDecisionPair(CubeAction.Double, CubeAction.Take),
            DoublerEquityLoss: doublerLoss, TakerEquityLoss: takerLoss,
            DoublerCorrect: doublerCorrect, TakerCorrect: takerCorrect);

    [Fact]
    public void Empty_HasNoProblems()
    {
        Assert.Equal(0, ProblemStatsDocument.Empty.Count);
        Assert.Empty(ProblemStatsDocument.Empty.Problems);
    }

    [Fact]
    public void Plus_UnseenProblem_CreatesItsLifetimeRecord()
    {
        var doc = ProblemStatsDocument.Empty
            .Plus(Play(KeyA, equityLoss: 0.0, correct: true), new TestClock(T1));

        Assert.Equal(1, doc.Count);
        var stats = Assert.Contains(KeyA, doc.Problems);
        Assert.Equal(new ScoreSegment(Submitted: 1, Correct: 1, TotalEquityLoss: 0.0), stats.Tally);
        Assert.Equal(T1, stats.LastQuizzed);
    }

    [Fact]
    public void Plus_RepeatedFoldsForOneProblem_AccumulateAndTrackLatestTime()
    {
        // Content keying is the point of the v2 reshape: the two submissions
        // may come from copies in different files — same key, one record.
        var clock = new TestClock(T1);
        var doc = ProblemStatsDocument.Empty
            .Plus(Play(KeyA, equityLoss: 0.05, correct: false), clock);

        clock.Now = T2;
        doc = doc.Plus(Play(KeyA, equityLoss: 0.0, correct: true), clock);

        Assert.Equal(1, doc.Count);
        var stats = doc.Problems[KeyA];
        Assert.Equal(2, stats.Tally.Submitted);
        Assert.Equal(1, stats.Tally.Correct);
        Assert.Equal(0.05, stats.Tally.TotalEquityLoss, precision: 9);
        Assert.Equal(T2, stats.LastQuizzed);
    }

    [Fact]
    public void Plus_DistinctProblems_TrackIndependently()
    {
        var clock = new TestClock(T1);
        var doc = ProblemStatsDocument.Empty
            .Plus(Play(KeyA, equityLoss: 0.03, correct: false), clock)
            .Plus(Cube(KeyB, doublerLoss: 0.0, doublerCorrect: true, takerLoss: 0.0, takerCorrect: true), clock);

        Assert.Equal(2, doc.Count);
        Assert.Equal(0, doc.Problems[KeyA].Tally.Correct);
        Assert.Equal(2, doc.Problems[KeyB].Tally.Correct);
    }

    [Fact]
    public void PlusCube_FoldsHalvesAsTwoDecisionsInOneRecord()
    {
        var doc = ProblemStatsDocument.Empty.Plus(
            Cube(KeyB, doublerLoss: 0.04, doublerCorrect: false, takerLoss: 0.06, takerCorrect: true),
            new TestClock(T1));

        Assert.Equal(1, doc.Count);               // still one record per problem
        var stats = doc.Problems[KeyB];
        Assert.Equal(2, stats.Tally.Submitted);   // doubler half + taker half
        Assert.Equal(1, stats.Tally.Correct);     // half-right reads 1-of-2
        Assert.Equal(0.10, stats.Tally.TotalEquityLoss, precision: 9);
    }

    [Fact]
    public void Plus_ReturnsNewDocument_OriginalUnchanged()
    {
        var clock = new TestClock(T1);
        var original = ProblemStatsDocument.Empty
            .Plus(Play(KeyA, equityLoss: 0.0, correct: true), clock);
        var updated = original.Plus(Play(KeyA, equityLoss: 0.02, correct: false), clock);

        Assert.NotSame(original, updated);
        Assert.Equal(1, original.Problems[KeyA].Tally.Submitted);
        Assert.Equal(2, updated.Problems[KeyA].Tally.Submitted);
    }

    [Fact]
    public void Plus_NoKeyPlay_IsNotRecorded()
    {
        // The no-key rung (SPEC-stats-identity.md §2): underivable key means
        // the submission scores the session but never the lifetime record —
        // and this document-level skip is the single filter.
        var clock = new TestClock(T1);
        var doc = ProblemStatsDocument.Empty
            .Plus(Play(KeyA, equityLoss: 0.0, correct: true), clock);

        var after = doc.Plus(Play(null, equityLoss: 0.02, correct: false), clock);

        Assert.Same(doc, after);
        Assert.Equal(1, after.Count);
        Assert.Equal(1, after.Problems[KeyA].Tally.Submitted);
    }

    [Fact]
    public void Plus_NoKeyCube_IsNotRecorded()
    {
        var clock = new TestClock(T1);
        var doc = ProblemStatsDocument.Empty;

        var after = doc.Plus(
            Cube(null, doublerLoss: 0.0, doublerCorrect: true, takerLoss: 0.0, takerCorrect: true), clock);

        Assert.Same(doc, after);
        Assert.Equal(0, after.Count);
    }

    [Fact]
    public void Plus_NullArguments_Throw()
    {
        var doc = ProblemStatsDocument.Empty;
        var clock = new TestClock(T1);

        Assert.Throws<ArgumentNullException>(() => doc.Plus((SubmittedPlay)null!, clock));
        Assert.Throws<ArgumentNullException>(() => doc.Plus((SubmittedCubeAction)null!, clock));
        Assert.Throws<ArgumentNullException>(() => doc.Plus(Play(KeyA, 0.0, correct: true), null!));
        Assert.Throws<ArgumentNullException>(() => doc.Plus(
            Cube(KeyB, 0.0, doublerCorrect: true, 0.0, takerCorrect: true), null!));
    }

    [Fact]
    public void FromStats_BuildsKeyedDocument()
    {
        var a = new ProblemStats(KeyA, new ScoreSegment(3, 2, 0.125), T1);
        var b = new ProblemStats(KeyB, new ScoreSegment(2, 1, 0.08), T2);

        var doc = ProblemStatsDocument.FromStats([a, b]);

        Assert.Equal(2, doc.Count);
        Assert.Equal(a, doc.Problems[KeyA]);
        Assert.Equal(b, doc.Problems[KeyB]);
    }

    [Fact]
    public void FromStats_DuplicateKey_Throws()
    {
        var a = new ProblemStats(KeyA, new ScoreSegment(3, 2, 0.125), T1);
        var alsoA = new ProblemStats(KeyA, new ScoreSegment(1, 1, 0.0), T2);

        Assert.Throws<ArgumentException>(() => ProblemStatsDocument.FromStats([a, alsoA]));
    }

    [Fact]
    public void FromStats_NullCollectionOrElement_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ProblemStatsDocument.FromStats(null!));
        Assert.Throws<ArgumentException>(() => ProblemStatsDocument.FromStats([null!]));
    }
}
