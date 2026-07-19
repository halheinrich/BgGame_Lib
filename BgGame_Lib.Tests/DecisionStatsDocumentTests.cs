namespace BgGame_Lib.Tests;

using System.Globalization;
using BgDataTypes_Lib;

public class DecisionStatsDocumentTests
{
    private static readonly DecisionId IdA =
        DecisionId.Parse("problem-a.xgp", CultureInfo.InvariantCulture);
    private static readonly DecisionId IdB =
        DecisionId.Parse("match.xg:g2:m7:cube", CultureInfo.InvariantCulture);

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

    private static SubmittedPlay Play(DecisionId id, double equityLoss, bool correct) =>
        new(id, new Play(), MatchedCandidateIndex: correct ? 0 : 1,
            EquityLoss: equityLoss, IsCorrect: correct);

    private static SubmittedCubeAction Cube(
        DecisionId id, double doublerLoss, bool doublerCorrect, double takerLoss, bool takerCorrect) =>
        new(id, new CubeDecisionPair(CubeAction.Double, CubeAction.Take),
            DoublerEquityLoss: doublerLoss, TakerEquityLoss: takerLoss,
            DoublerCorrect: doublerCorrect, TakerCorrect: takerCorrect);

    [Fact]
    public void Empty_HasNoDecisions()
    {
        Assert.Equal(0, DecisionStatsDocument.Empty.Count);
        Assert.Empty(DecisionStatsDocument.Empty.Decisions);
    }

    [Fact]
    public void Plus_UnseenDecision_CreatesItsLifetimeRecord()
    {
        var doc = DecisionStatsDocument.Empty
            .Plus(Play(IdA, equityLoss: 0.0, correct: true), new TestClock(T1));

        Assert.Equal(1, doc.Count);
        var stats = Assert.Contains(IdA, doc.Decisions);
        Assert.Equal(new ScoreSegment(Submitted: 1, Correct: 1, TotalEquityLoss: 0.0), stats.Tally);
        Assert.Equal(T1, stats.LastQuizzed);
    }

    [Fact]
    public void Plus_RepeatedFoldsForOneDecision_AccumulateAndTrackLatestTime()
    {
        var clock = new TestClock(T1);
        var doc = DecisionStatsDocument.Empty
            .Plus(Play(IdA, equityLoss: 0.05, correct: false), clock);

        clock.Now = T2;
        doc = doc.Plus(Play(IdA, equityLoss: 0.0, correct: true), clock);

        Assert.Equal(1, doc.Count);
        var stats = doc.Decisions[IdA];
        Assert.Equal(2, stats.Tally.Submitted);
        Assert.Equal(1, stats.Tally.Correct);
        Assert.Equal(0.05, stats.Tally.TotalEquityLoss, precision: 9);
        Assert.Equal(T2, stats.LastQuizzed);
    }

    [Fact]
    public void Plus_DistinctDecisions_TrackIndependently()
    {
        var clock = new TestClock(T1);
        var doc = DecisionStatsDocument.Empty
            .Plus(Play(IdA, equityLoss: 0.03, correct: false), clock)
            .Plus(Cube(IdB, doublerLoss: 0.0, doublerCorrect: true, takerLoss: 0.0, takerCorrect: true), clock);

        Assert.Equal(2, doc.Count);
        Assert.Equal(0, doc.Decisions[IdA].Tally.Correct);
        Assert.Equal(2, doc.Decisions[IdB].Tally.Correct);
    }

    [Fact]
    public void PlusCube_FoldsHalvesAsTwoDecisionsInOneRecord()
    {
        var doc = DecisionStatsDocument.Empty.Plus(
            Cube(IdB, doublerLoss: 0.04, doublerCorrect: false, takerLoss: 0.06, takerCorrect: true),
            new TestClock(T1));

        Assert.Equal(1, doc.Count);               // still one record per decision
        var stats = doc.Decisions[IdB];
        Assert.Equal(2, stats.Tally.Submitted);   // doubler half + taker half
        Assert.Equal(1, stats.Tally.Correct);     // half-right reads 1-of-2
        Assert.Equal(0.10, stats.Tally.TotalEquityLoss, precision: 9);
    }

    [Fact]
    public void Plus_ReturnsNewDocument_OriginalUnchanged()
    {
        var clock = new TestClock(T1);
        var original = DecisionStatsDocument.Empty
            .Plus(Play(IdA, equityLoss: 0.0, correct: true), clock);
        var updated = original.Plus(Play(IdA, equityLoss: 0.02, correct: false), clock);

        Assert.NotSame(original, updated);
        Assert.Equal(1, original.Decisions[IdA].Tally.Submitted);
        Assert.Equal(2, updated.Decisions[IdA].Tally.Submitted);
    }

    [Fact]
    public void Plus_NullArguments_Throw()
    {
        var doc = DecisionStatsDocument.Empty;
        var clock = new TestClock(T1);

        Assert.Throws<ArgumentNullException>(() => doc.Plus((SubmittedPlay)null!, clock));
        Assert.Throws<ArgumentNullException>(() => doc.Plus((SubmittedCubeAction)null!, clock));
        Assert.Throws<ArgumentNullException>(() => doc.Plus(Play(IdA, 0.0, correct: true), null!));
        Assert.Throws<ArgumentNullException>(() => doc.Plus(
            Cube(IdB, 0.0, doublerCorrect: true, 0.0, takerCorrect: true), null!));
    }

    [Fact]
    public void FromStats_BuildsKeyedDocument()
    {
        var a = new DecisionStats(IdA, new ScoreSegment(3, 2, 0.125), T1);
        var b = new DecisionStats(IdB, new ScoreSegment(1, 0, 0.08), T2);

        var doc = DecisionStatsDocument.FromStats([a, b]);

        Assert.Equal(2, doc.Count);
        Assert.Equal(a, doc.Decisions[IdA]);
        Assert.Equal(b, doc.Decisions[IdB]);
    }

    [Fact]
    public void FromStats_DuplicateDecisionId_Throws()
    {
        var a = new DecisionStats(IdA, new ScoreSegment(3, 2, 0.125), T1);
        var alsoA = new DecisionStats(IdA, new ScoreSegment(1, 1, 0.0), T2);

        Assert.Throws<ArgumentException>(() => DecisionStatsDocument.FromStats([a, alsoA]));
    }

    [Fact]
    public void FromStats_NullCollectionOrElement_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => DecisionStatsDocument.FromStats(null!));
        Assert.Throws<ArgumentException>(() => DecisionStatsDocument.FromStats([null!]));
    }
}
