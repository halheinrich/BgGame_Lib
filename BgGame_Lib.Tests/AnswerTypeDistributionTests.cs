namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;

public class AnswerTypeDistributionTests
{
    // A cube decision's best pair is derived from its two equities, not stated
    // directly:
    //
    //   BestDoublerAction = Double iff min(E_DT, 1) > E_ND
    //   BestTakerAction   = Take   iff E_DT < 1
    //
    // The values below sit off the tie boundaries so each pair is unambiguous;
    // they match BgDataTypes_Lib's own cube-scoring tests. Fixture_* pins that
    // they really do produce the four intended pairs, so a bucketing failure
    // can never be mistaken for a mis-built fixture.

    private static DecisionData Cube(double noDoubleEquity, double doubleTakeEquity) => new()
    {
        IsCube = true,
        NoDoubleEquity = noDoubleEquity,
        DoubleTakeEquity = doubleTakeEquity,
    };

    private static DecisionData Play => new() { IsCube = false };
    private static DecisionData NoDoubleTake => Cube(0.20, 0.10);
    private static DecisionData TooGood => Cube(1.30, 1.50);
    private static DecisionData DoubleTake => Cube(0.30, 0.60);
    private static DecisionData DoublePass => Cube(0.50, 1.20);

    private static CubeDecisionPair BestPair(DecisionData d) =>
        new(d.BestDoublerAction, d.BestTakerAction);

    /// <summary>
    /// Assert every bucket, plus that <c>Total</c> is their sum — the fold
    /// contract the consumer's matched count depends on.
    /// </summary>
    private static void AssertBuckets(
        AnswerTypeDistribution d,
        int checkerPlays, int noDoubleTake, int tooGood, int doubleTake, int doublePass)
    {
        Assert.Equal(checkerPlays, d.CheckerPlays);
        Assert.Equal(noDoubleTake, d.NoDoubleTake);
        Assert.Equal(tooGood, d.TooGood);
        Assert.Equal(doubleTake, d.DoubleTake);
        Assert.Equal(doublePass, d.DoublePass);
        Assert.Equal(
            checkerPlays + noDoubleTake + tooGood + doubleTake + doublePass,
            d.Total);
    }

    private static AnswerTypeDistribution Fold(params DecisionData[] pool) =>
        pool.Aggregate(AnswerTypeDistribution.Empty, (acc, d) => acc.Add(d));

    // ---------------------------------------------------------------------
    //  Fixtures
    // ---------------------------------------------------------------------

    [Fact]
    public void Fixtures_ProduceTheFourIntendedBestPairs()
    {
        Assert.Equal(CubeDecisionPair.NoDoubleTake, BestPair(NoDoubleTake));
        Assert.Equal(CubeDecisionPair.TooGood, BestPair(TooGood));
        Assert.Equal(CubeDecisionPair.DoubleTake, BestPair(DoubleTake));
        Assert.Equal(CubeDecisionPair.DoublePass, BestPair(DoublePass));
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
    //  Classification — one decision of each kind
    // ---------------------------------------------------------------------

    [Fact]
    public void Add_CheckerPlay_LandsInCheckerPlays()
    {
        AssertBuckets(Fold(Play), 1, 0, 0, 0, 0);
    }

    [Fact]
    public void Add_NoDoubleTakeCube_LandsInNoDoubleTake()
    {
        AssertBuckets(Fold(NoDoubleTake), 0, 1, 0, 0, 0);
    }

    [Fact]
    public void Add_TooGoodCube_LandsInTooGood()
    {
        AssertBuckets(Fold(TooGood), 0, 0, 1, 0, 0);
    }

    [Fact]
    public void Add_DoubleTakeCube_LandsInDoubleTake()
    {
        AssertBuckets(Fold(DoubleTake), 0, 0, 0, 1, 0);
    }

    [Fact]
    public void Add_DoublePassCube_LandsInDoublePass()
    {
        AssertBuckets(Fold(DoublePass), 0, 0, 0, 0, 1);
    }

    [Fact]
    public void AllFourCubePairs_AreDistinguished()
    {
        AssertBuckets(Fold(NoDoubleTake, TooGood, DoubleTake, DoublePass), 0, 1, 1, 1, 1);
    }

    // ---------------------------------------------------------------------
    //  Fold contract — one bucket per Add, Total == decisions folded
    // ---------------------------------------------------------------------

    [Fact]
    public void CubeDecision_CountsOnce_NotOncePerHalf()
    {
        // Deliberately unlike QuizScore / DecisionStats, where a cube position
        // folds as two scored decisions. Pool membership counts rows.
        Assert.Equal(1, Fold(DoubleTake).Total);
    }

    [Fact]
    public void Total_EqualsTheNumberOfAddsPerformed()
    {
        DecisionData[] pool =
        [
            Play, NoDoubleTake, TooGood, DoubleTake, DoublePass,
            Play, DoublePass, DoublePass,
        ];

        var d = Fold(pool);

        Assert.Equal(pool.Length, d.Total);
        AssertBuckets(d, 2, 1, 1, 1, 3);
    }

    [Fact]
    public void Add_LeavesTheReceiverUnchanged()
    {
        var start = Fold(Play);

        var next = start.Add(TooGood);

        AssertBuckets(start, 1, 0, 0, 0, 0);
        AssertBuckets(next, 1, 0, 1, 0, 0);
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
        var sequential = Fold(Play, Play, NoDoubleTake, TooGood, DoubleTake, DoublePass);

        var a = Fold(Play, NoDoubleTake, DoubleTake);
        var b = Fold(Play, TooGood, DoublePass);

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
