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

    // Per-half correctness is derived from the answer/truth pair rather than
    // stated, so this helper composes pairs that realize the requested
    // verdicts: the truth is always (Double, Take) and each half of the
    // answer either matches it or is bent away from it. What this suite
    // asserts — how a cube submission accumulates — is unchanged by that.
    private static SubmittedCubeAction Cube(
        ProblemKey? key, double doublerLoss, bool doublerCorrect, double takerLoss, bool takerCorrect) =>
        new(key,
            new CubeClaimPair(
                doublerCorrect ? CubeClaim.Double : CubeClaim.NoDouble,
                takerCorrect ? CubeAction.Take : CubeAction.Pass),
            CubeClaimPair.DoubleTake,
            DoublerEquityLoss: doublerLoss, TakerEquityLoss: takerLoss);

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

    // =====================================================================
    //  Merge — the document algebra behind the v4 fold
    //  (SPEC-stats-identity.md §3, amended 2026-09-02; halheinrich/backgammon#187)
    // =====================================================================
    //
    // Per key: shared keys merge via ProblemStats.Merge (tallies summed field
    // by field, LastQuizzed the later), one-sided keys pass through. The
    // equity losses below are dyadic rationals (0.125, 0.25, 0.5) so the
    // sums are exact and the associativity pin is a genuine equality, not a
    // rounding coincidence.

    private static readonly ProblemKey KeyC = ProblemKey.Parse($"{Board}/3a1/1c/42");

    private static ProblemStatsDocument Doc(params ProblemStats[] stats) =>
        ProblemStatsDocument.FromStats(stats);

    private static void AssertSameRecords(ProblemStatsDocument expected, ProblemStatsDocument actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var (key, record) in expected.Problems)
            Assert.Equal(record, Assert.Contains(key, actual.Problems));
    }

    [Fact]
    public void Merge_DisjointKeys_CarriesBothSidesThroughUnchanged()
    {
        var a = new ProblemStats(KeyA, new ScoreSegment(3, 2, 0.125), T1);
        var b = new ProblemStats(KeyB, new ScoreSegment(2, 1, 0.25), T2);

        var merged = Doc(a).Merge(Doc(b));

        AssertSameRecords(Doc(a, b), merged);
    }

    [Fact]
    public void Merge_SharedKey_SumsTalliesAndTakesTheLaterLastQuizzed()
    {
        var mine = new ProblemStats(KeyA, new ScoreSegment(3, 2, 0.125), T1);
        var theirs = new ProblemStats(KeyA, new ScoreSegment(2, 0, 0.25), T2);

        var merged = Doc(mine).Merge(Doc(theirs));

        Assert.Equal(1, merged.Count);
        Assert.Equal(
            new ProblemStats(KeyA, new ScoreSegment(5, 2, 0.375), T2),
            merged.Problems[KeyA]);
    }

    [Fact]
    public void Merge_SharedAndDisjointKeysTogether()
    {
        var left = Doc(
            new ProblemStats(KeyA, new ScoreSegment(1, 1, 0.0), T1),
            new ProblemStats(KeyB, new ScoreSegment(2, 1, 0.5), T2));
        var right = Doc(
            new ProblemStats(KeyB, new ScoreSegment(4, 3, 0.25), T1),
            new ProblemStats(KeyC, new ScoreSegment(1, 0, 0.125), T1));

        var merged = left.Merge(right);

        AssertSameRecords(
            Doc(
                new ProblemStats(KeyA, new ScoreSegment(1, 1, 0.0), T1),
                new ProblemStats(KeyB, new ScoreSegment(6, 4, 0.75), T2),
                new ProblemStats(KeyC, new ScoreSegment(1, 0, 0.125), T1)),
            merged);
    }

    [Fact]
    public void Merge_EmptyIsTheIdentity_OnBothSides()
    {
        var doc = Doc(
            new ProblemStats(KeyA, new ScoreSegment(3, 2, 0.125), T1),
            new ProblemStats(KeyB, new ScoreSegment(2, 1, 0.25), T2));

        AssertSameRecords(doc, doc.Merge(ProblemStatsDocument.Empty));
        AssertSameRecords(doc, ProblemStatsDocument.Empty.Merge(doc));
        Assert.Equal(0, ProblemStatsDocument.Empty.Merge(ProblemStatsDocument.Empty).Count);
    }

    [Fact]
    public void Merge_IsCommutative()
    {
        var left = Doc(
            new ProblemStats(KeyA, new ScoreSegment(1, 1, 0.0), T1),
            new ProblemStats(KeyB, new ScoreSegment(2, 1, 0.5), T2));
        var right = Doc(
            new ProblemStats(KeyB, new ScoreSegment(4, 3, 0.25), T1),
            new ProblemStats(KeyC, new ScoreSegment(1, 0, 0.125), T1));

        AssertSameRecords(left.Merge(right), right.Merge(left));
    }

    [Fact]
    public void Merge_IsAssociative()
    {
        var a = Doc(new ProblemStats(KeyA, new ScoreSegment(1, 1, 0.125), T1));
        var b = Doc(
            new ProblemStats(KeyA, new ScoreSegment(2, 0, 0.25), T2),
            new ProblemStats(KeyB, new ScoreSegment(1, 1, 0.0), T1));
        var c = Doc(
            new ProblemStats(KeyA, new ScoreSegment(4, 4, 0.5), T1),
            new ProblemStats(KeyC, new ScoreSegment(3, 1, 0.125), T2));

        AssertSameRecords(a.Merge(b).Merge(c), a.Merge(b.Merge(c)));
    }

    [Fact]
    public void Merge_LeavesBothInputsUnchanged()
    {
        var mine = Doc(new ProblemStats(KeyA, new ScoreSegment(3, 2, 0.125), T1));
        var theirs = Doc(new ProblemStats(KeyA, new ScoreSegment(2, 0, 0.25), T2));

        var merged = mine.Merge(theirs);

        Assert.NotSame(mine, merged);
        Assert.NotSame(theirs, merged);
        Assert.Equal(new ScoreSegment(3, 2, 0.125), mine.Problems[KeyA].Tally);
        Assert.Equal(T1, mine.Problems[KeyA].LastQuizzed);
        Assert.Equal(new ScoreSegment(2, 0, 0.25), theirs.Problems[KeyA].Tally);
        Assert.Equal(new ScoreSegment(5, 2, 0.375), merged.Problems[KeyA].Tally);
    }

    [Fact]
    public void Merge_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ProblemStatsDocument.Empty.Merge(null!));
    }
}
