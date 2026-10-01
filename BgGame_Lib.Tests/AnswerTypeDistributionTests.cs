namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;

public class AnswerTypeDistributionTests
{
    // A cube decision's truth is the producer's, derived from its two
    // equities (CubeDecisionData.BestAnswer, SPEC-scoring §3 as amended on
    // halheinrich/backgammon#326), never stated directly. The values below sit
    // off the boundaries so each truth is unambiguous — except Tie, which sits
    // ON the no-double-equals-the-cash tie of halheinrich/backgammon#293
    // deliberately, where the truth is the fourth answer though more than one
    // answer costs nothing. Fixtures_* pins that the fixtures really do
    // derive the intended truths, so a bucketing failure can never be
    // mistaken for a mis-built fixture.

    private static CubeDecision Cube(double noDoubleEquity, double doubleTakeEquity, PositionData? position = null) =>
        TestSubmissions.CubeRecord(noDoubleEquity, doubleTakeEquity, position);

    private static CheckerPlayDecision Play => TestRecords.CheckerPlay();
    private static CubeDecision NoDouble => Cube(0.20, 0.10);
    private static CubeDecision DoubleTake => Cube(0.30, 0.60);
    private static CubeDecision DoublePass => Cube(0.50, 1.20);
    private static CubeDecision NoDoublePass => Cube(1.30, 1.50);
    private static CubeDecision Tie => Cube(1.00, 1.20);

    /// <summary>
    /// The fourth answer's truth where gammons are not possible, so it reads
    /// No double / Pass rather than Too good: the bucket is the answer's
    /// meaning, whichever label it reads.
    /// </summary>
    private static CubeDecision NoDoublePassWithoutGammons => Cube(1.00, 1.20, TestSubmissions.NoGammons);

    /// <summary>
    /// Playing on beats being taken (N above the cash) yet the opponent would
    /// take: XG's "Too good to double/Take", which the 2026-09-02 amendment
    /// ruled No double / Take.
    /// </summary>
    private static CubeDecision PlayingOnBeatsBeingTaken => Cube(1.10, 0.90);

    /// <summary>
    /// Assert every bucket, plus that <c>Total</c> is their sum — the fold
    /// contract the consumer's matched count depends on.
    /// </summary>
    private static void AssertBuckets(
        AnswerTypeDistribution d,
        int checkerPlays, int noDouble, int doubleTake, int doublePass, int noDoublePass)
    {
        Assert.Equal(checkerPlays, d.CheckerPlays);
        Assert.Equal(noDouble, d.NoDouble);
        Assert.Equal(doubleTake, d.DoubleTake);
        Assert.Equal(doublePass, d.DoublePass);
        Assert.Equal(noDoublePass, d.NoDoublePass);
        Assert.Equal(checkerPlays + noDouble + doubleTake + doublePass + noDoublePass, d.Total);
    }

    private static AnswerTypeDistribution Fold(params BgDecisionData[] pool) =>
        pool.Aggregate(AnswerTypeDistribution.Empty, (acc, d) => acc.Add(d));

    // ---------------------------------------------------------------------
    //  Fixtures
    // ---------------------------------------------------------------------

    [Fact]
    public void Fixtures_DeriveTheFourTruths()
    {
        Assert.Equal(CubeAnswer.NoDouble, NoDouble.Decision.BestAnswer);
        Assert.Equal(CubeAnswer.DoubleTake, DoubleTake.Decision.BestAnswer);
        Assert.Equal(CubeAnswer.DoublePass, DoublePass.Decision.BestAnswer);
        Assert.Equal(CubeAnswer.NoDoublePass, NoDoublePass.Decision.BestAnswer);
    }

    [Fact]
    public void TieFixture_DerivesTheFourthAnswer_WhileMoreThanOneAnswerCostsNothing()
    {
        // At N exactly 1 with a pass, No double, Double / Pass and the fourth
        // answer all cost 0 (SPEC-scoring §3); the truth, the single
        // representative, is the fourth answer.
        Assert.Equal(CubeAnswer.NoDoublePass, Tie.Decision.BestAnswer);
        Assert.Equal(0.0, Tie.CostOf(CubeAnswer.NoDouble).Total);
        Assert.Equal(0.0, Tie.CostOf(CubeAnswer.DoublePass).Total);
        Assert.Equal(0.0, Tie.CostOf(CubeAnswer.NoDoublePass).Total);
    }

    [Fact]
    public void LabelFixtures_ReadTheFourthAnswerUnderBothItsLabels()
    {
        Assert.Equal(CubeClaim.TooGood, NoDoublePass.ClaimOf(NoDoublePass.Decision.BestAnswer));
        Assert.Equal(CubeAnswer.NoDoublePass, NoDoublePassWithoutGammons.Decision.BestAnswer);
        Assert.Equal(CubeClaim.NoDouble, NoDoublePassWithoutGammons.ClaimOf(CubeAnswer.NoDoublePass));
    }

    [Fact]
    public void PlayingOnBeatsBeingTakenFixture_DerivesNoDouble()
    {
        Assert.Equal(CubeAnswer.NoDouble, PlayingOnBeatsBeingTaken.Decision.BestAnswer);
    }

    // ---------------------------------------------------------------------
    //  Empty
    // ---------------------------------------------------------------------

    [Fact]
    public void Empty_IsZeroAcrossAllBuckets()
    {
        AssertBuckets(AnswerTypeDistribution.Empty, 0, 0, 0, 0, 0);
    }

    [Fact]
    public void EmptyPool_FoldsToEmpty()
    {
        var d = Fold();

        Assert.Equal(AnswerTypeDistribution.Empty, d);
        Assert.Equal(0, d.Total);
    }

    // ---------------------------------------------------------------------
    //  Classification — one decision of each kind, by its truth
    // ---------------------------------------------------------------------

    [Fact]
    public void Add_CheckerPlay_LandsInCheckerPlays()
    {
        AssertBuckets(Fold(Play), 1, 0, 0, 0, 0);
    }

    [Fact]
    public void Add_NoDoubleTruth_LandsInNoDouble()
    {
        AssertBuckets(Fold(NoDouble), 0, 1, 0, 0, 0);
    }

    [Fact]
    public void Add_DoubleTakeTruth_LandsInDoubleTake()
    {
        AssertBuckets(Fold(DoubleTake), 0, 0, 1, 0, 0);
    }

    [Fact]
    public void Add_DoublePassTruth_LandsInDoublePass()
    {
        AssertBuckets(Fold(DoublePass), 0, 0, 0, 1, 0);
    }

    [Fact]
    public void Add_TheFourthAnswersTruth_LandsInNoDoublePass_UnderEitherLabel()
    {
        // The bucket is named by the answer's meaning, "don't double, they'd
        // pass": Too good where gammons are possible, No double / Pass where
        // they are not, one bucket.
        AssertBuckets(Fold(NoDoublePass), 0, 0, 0, 0, 1);
        AssertBuckets(Fold(NoDoublePassWithoutGammons), 0, 0, 0, 0, 1);
    }

    [Fact]
    public void Add_TheTie_LandsInTheFourthAnswer_Once()
    {
        // The halheinrich/backgammon#293 tie is classified by its truth, the
        // fourth answer ("(No Double, Pass) is just too good"), and counts
        // once, though three answers are correct there.
        AssertBuckets(Fold(Tie), 0, 0, 0, 0, 1);
    }

    [Fact]
    public void Add_PlayingOnBeatsBeingTaken_LandsInNoDouble()
    {
        // By the 2026-09-02 ruling the fourth answer requires the pass, so a
        // position the opponent would take is No double whatever the
        // no-double equity.
        AssertBuckets(Fold(PlayingOnBeatsBeingTaken), 0, 1, 0, 0, 0);
    }

    [Fact]
    public void AllFourTruths_AreDistinguished()
    {
        AssertBuckets(Fold(NoDouble, DoubleTake, DoublePass, NoDoublePass), 0, 1, 1, 1, 1);
    }

    // ---------------------------------------------------------------------
    //  Fold contract — one bucket per Add, Total == decisions folded
    // ---------------------------------------------------------------------

    [Fact]
    public void ACubeDecision_CountsOnce_HoweverItsAnswersAreScored()
    {
        // Deliberately unlike QuizScore and ProblemStats, where a cube answer
        // is up to three decisions or two. Pool membership counts rows.
        Assert.Equal(1, Fold(DoubleTake).Total);
        Assert.Equal(1, Fold(Tie).Total);
    }

    [Fact]
    public void Total_EqualsTheNumberOfAddsPerformed()
    {
        BgDecisionData[] pool =
        [
            Play, NoDouble, DoubleTake, DoublePass, NoDoublePass, PlayingOnBeatsBeingTaken,
            Play, DoublePass, DoublePass, Tie, NoDoublePassWithoutGammons,
        ];

        var d = Fold(pool);

        Assert.Equal(pool.Length, d.Total);
        AssertBuckets(d, 2, 2, 1, 3, 3);
    }

    [Fact]
    public void Add_LeavesTheReceiverUnchanged()
    {
        var start = Fold(Play);

        var next = start.Add(NoDoublePass);

        AssertBuckets(start, 1, 0, 0, 0, 0);
        AssertBuckets(next, 1, 0, 0, 0, 1);
    }

    [Fact]
    public void Add_NullDecision_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AnswerTypeDistribution.Empty.Add(null!));
    }

    // ---------------------------------------------------------------------
    //  Combination
    // ---------------------------------------------------------------------

    [Fact]
    public void OperatorPlus_AddsBucketWise()
    {
        var a = new AnswerTypeDistribution(1, 2, 3, 4, 5);
        var b = new AnswerTypeDistribution(10, 20, 30, 40, 50);

        AssertBuckets(a + b, 11, 22, 33, 44, 55);
    }

    [Fact]
    public void OperatorPlus_EmptyIsTheIdentity()
    {
        var d = new AnswerTypeDistribution(3, 1, 4, 1, 5);

        Assert.Equal(d, d + AnswerTypeDistribution.Empty);
        Assert.Equal(d, AnswerTypeDistribution.Empty + d);
    }

    [Fact]
    public void OperatorPlus_MatchesSequentialAdds()
    {
        var sequential = Fold(
            Play, Play, NoDouble, DoubleTake, DoublePass, NoDoublePass, PlayingOnBeatsBeingTaken);

        var a = Fold(Play, NoDouble, DoubleTake);
        var b = Fold(Play, DoublePass, NoDoublePass, PlayingOnBeatsBeingTaken);

        Assert.Equal(sequential, a + b);
    }

    [Fact]
    public void OperatorPlus_NullLeft_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => null! + AnswerTypeDistribution.Empty);
    }

    [Fact]
    public void OperatorPlus_NullRight_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AnswerTypeDistribution.Empty + null!);
    }
}
