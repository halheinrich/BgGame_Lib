namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;

public class ProblemStatsTests
{
    // Canonical key fixtures: the ratified wire grammar (board/score/cube[/dice]),
    // standard starting board. A cube key is a play key without the dice field —
    // the kind discriminant — so CubeKey and Key name the same position's two
    // distinct decision kinds.
    private const string Board = "0,-2,0,0,0,0,5,0,3,0,0,0,-5,5,0,0,0,-3,0,-5,0,0,0,0,2,0";

    private static readonly ProblemKey Key = ProblemKey.Parse($"{Board}/7a7/1c/31");
    private static readonly ProblemKey OtherKey = ProblemKey.Parse($"{Board}/5a3/2o/65");
    private static readonly ProblemKey CubeKey = ProblemKey.Parse($"{Board}/7a7/1c");

    private static readonly DateTimeOffset T1 = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

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
    public void FromPlay_Correct_OneSubmittedOneCorrectNoLoss()
    {
        var s = ProblemStats.From(Play(Key, equityLoss: 0.0, correct: true), T1);

        Assert.Equal(Key, s.Key);
        Assert.Equal(new ScoreSegment(Submitted: 1, Correct: 1, TotalEquityLoss: 0.0), s.Tally);
        Assert.Equal(0, s.Wrong);
        Assert.Equal(T1, s.LastQuizzed);
    }

    [Fact]
    public void FromPlay_Incorrect_OneSubmittedZeroCorrectWithLoss()
    {
        var s = ProblemStats.From(Play(Key, equityLoss: 0.042, correct: false), T1);

        Assert.Equal(1, s.Tally.Submitted);
        Assert.Equal(0, s.Tally.Correct);
        Assert.Equal(0.042, s.Tally.TotalEquityLoss, precision: 9);
        Assert.Equal(1, s.Wrong);
    }

    [Fact]
    public void FromCube_BothHalvesCorrect_CountsTwoCorrectDecisions()
    {
        var s = ProblemStats.From(
            Cube(CubeKey, doublerLoss: 0.0, doublerCorrect: true, takerLoss: 0.0, takerCorrect: true), T1);

        Assert.Equal(CubeKey, s.Key);
        Assert.Equal(new ScoreSegment(Submitted: 2, Correct: 2, TotalEquityLoss: 0.0), s.Tally);
    }

    [Theory]
    [InlineData(0.05, false, 0.00, true)]   // doubler half wrong
    [InlineData(0.00, true, 0.08, false)]   // taker half wrong
    public void FromCube_OneHalfWrong_CountsOneOfTwo_LossSumsBothHalves(
        double doublerLoss, bool doublerCorrect, double takerLoss, bool takerCorrect)
    {
        var s = ProblemStats.From(
            Cube(CubeKey, doublerLoss, doublerCorrect, takerLoss, takerCorrect), T1);

        Assert.Equal(2, s.Tally.Submitted);
        Assert.Equal(1, s.Tally.Correct);
        Assert.Equal(1, s.Wrong);
        Assert.Equal(doublerLoss + takerLoss, s.Tally.TotalEquityLoss, precision: 9);
    }

    [Fact]
    public void FromCube_BothHalvesWrong_CountsZeroOfTwo_LossSumsBothHalves()
    {
        var s = ProblemStats.From(
            Cube(CubeKey, doublerLoss: 0.05, doublerCorrect: false, takerLoss: 0.08, takerCorrect: false), T1);

        Assert.Equal(2, s.Tally.Submitted);
        Assert.Equal(0, s.Tally.Correct);
        Assert.Equal(2, s.Wrong);
        Assert.Equal(0.13, s.Tally.TotalEquityLoss, precision: 9);
    }

    [Fact]
    public void FromCube_WrongClaimOverTheRightAction_CountsOneOfTwoAtZeroDoublerLoss()
    {
        // The lifetime tally reads the same claim-vs-claim derivation the
        // session score does (SPEC-scoring §3; halheinrich/backgammon#86): a
        // no-double answer on a too-good position is one wrong half, and it
        // costs +0.000 because the board action behind both claims is the
        // same. Built through SubmittedCubeAction.From so the truth pair and
        // the loss come from the producer, not from this test.
        var tooGoodPass = new DecisionData
        {
            IsCube = true,
            NoDoubleEquity = 1.30,
            DoubleTakeEquity = 1.50,
        };
        var submission = SubmittedCubeAction.From(
            CubeKey, CubeClaimPair.NoDoublePass, tooGoodPass);

        Assert.Equal(CubeClaimPair.TooGoodPass, submission.BestDecision);

        var s = ProblemStats.From(submission, T1);

        Assert.Equal(2, s.Tally.Submitted);
        Assert.Equal(1, s.Tally.Correct);       // taker half only
        Assert.Equal(1, s.Wrong);
        Assert.Equal(0.0, s.Tally.TotalEquityLoss);
    }

    [Fact]
    public void Plus_RepeatedFolds_AccumulateAndAdvanceLastQuizzed()
    {
        var s = ProblemStats.From(Play(Key, equityLoss: 0.025, correct: false), T1)
            .Plus(Play(Key, equityLoss: 0.0, correct: true), T2);

        Assert.Equal(2, s.Tally.Submitted);
        Assert.Equal(1, s.Tally.Correct);
        Assert.Equal(0.025, s.Tally.TotalEquityLoss, precision: 9);
        Assert.Equal(1, s.Wrong);
        Assert.Equal(T2, s.LastQuizzed);
    }

    [Fact]
    public void Plus_ReturnsNewInstance_OriginalUnchanged()
    {
        var original = ProblemStats.From(Play(Key, equityLoss: 0.0, correct: true), T1);
        var updated = original.Plus(Play(Key, equityLoss: 0.05, correct: false), T2);

        Assert.NotSame(original, updated);
        Assert.Equal(1, original.Tally.Submitted);
        Assert.Equal(T1, original.LastQuizzed);
        Assert.Equal(2, updated.Tally.Submitted);
    }

    [Fact]
    public void PlusPlay_MismatchedKey_Throws()
    {
        var s = ProblemStats.From(Play(Key, equityLoss: 0.0, correct: true), T1);

        Assert.Throws<ArgumentException>(
            () => s.Plus(Play(OtherKey, equityLoss: 0.0, correct: true), T2));
    }

    [Fact]
    public void PlusCube_IntoPlayKeyRecord_Throws_KindsNeverMix()
    {
        // The same position's play key and cube key are distinct by grammar
        // (the dice field), so a mixed-kind fold is a key mismatch — kind
        // purity is structural, which keeps per-kind tally derivations
        // (Sightings' divide-by-two for cubes) exact.
        var s = ProblemStats.From(Play(Key, equityLoss: 0.0, correct: true), T1);

        Assert.Throws<ArgumentException>(() => s.Plus(
            Cube(CubeKey, doublerLoss: 0.0, doublerCorrect: true, takerLoss: 0.0, takerCorrect: true), T2));
    }

    [Fact]
    public void FromAndPlus_NoKeySubmissions_Throw()
    {
        // The no-key rung is filtered one level up (ProblemStatsDocument.Plus
        // returns the document unchanged); at this level a keyless submission
        // is a caller bug.
        var s = ProblemStats.From(Play(Key, equityLoss: 0.0, correct: true), T1);

        Assert.Throws<ArgumentException>(
            () => ProblemStats.From(Play(null, equityLoss: 0.0, correct: true), T1));
        Assert.Throws<ArgumentException>(
            () => ProblemStats.From(
                Cube(null, doublerLoss: 0.0, doublerCorrect: true, takerLoss: 0.0, takerCorrect: true), T1));
        Assert.Throws<ArgumentException>(
            () => s.Plus(Play(null, equityLoss: 0.0, correct: true), T2));
        Assert.Throws<ArgumentException>(
            () => s.Plus(
                Cube(null, doublerLoss: 0.0, doublerCorrect: true, takerLoss: 0.0, takerCorrect: true), T2));
    }

    [Fact]
    public void FromAndPlus_NullSubmissions_Throw()
    {
        var s = ProblemStats.From(Play(Key, equityLoss: 0.0, correct: true), T1);

        Assert.Throws<ArgumentNullException>(() => ProblemStats.From((SubmittedPlay)null!, T1));
        Assert.Throws<ArgumentNullException>(() => ProblemStats.From((SubmittedCubeAction)null!, T1));
        Assert.Throws<ArgumentNullException>(() => s.Plus((SubmittedPlay)null!, T2));
        Assert.Throws<ArgumentNullException>(() => s.Plus((SubmittedCubeAction)null!, T2));
    }

    [Fact]
    public void RecordEquality_HoldsByValue()
    {
        var a = new ProblemStats(Key, new ScoreSegment(3, 2, 0.125), T1);
        var b = new ProblemStats(Key, new ScoreSegment(3, 2, 0.125), T1);

        Assert.Equal(a, b);
        Assert.NotEqual(a, a with { LastQuizzed = T2 });
        Assert.NotEqual(a, a with { Key = OtherKey });
    }
}
