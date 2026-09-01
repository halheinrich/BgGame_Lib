namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;

public class AnswerTypeDistributionTests
{
    // A cube decision's best claim pair is derived from its two equities, not
    // stated directly (the producer's one derivation site — SPEC-scoring §3):
    //
    //   BestDoublerAction = Double  iff min(E_DT, 1) > E_ND
    //   BestDoublerClaim  = TooGood iff best action is NoDouble and E_ND > 1
    //   BestTakerAction   = Take    iff E_DT < 1
    //
    // The values below sit off the tie boundaries so each pair is unambiguous
    // — except Boundary, which sits ON the E_ND == 1 tie deliberately: the
    // ruled tie-breaks compose the incoherent (NoDouble, Pass) pair there,
    // and its classification is a ruled mapping this suite pins. Fixture_*
    // pins that the fixtures really do produce the intended pairs, so a
    // bucketing failure can never be mistaken for a mis-built fixture.

    private static DecisionData Cube(double noDoubleEquity, double doubleTakeEquity) => new()
    {
        IsCube = true,
        NoDoubleEquity = noDoubleEquity,
        DoubleTakeEquity = doubleTakeEquity,
    };

    private static DecisionData Play => new() { IsCube = false };
    private static DecisionData NoDoubleTake => Cube(0.20, 0.10);
    private static DecisionData DoubleTake => Cube(0.30, 0.60);
    private static DecisionData DoublePass => Cube(0.50, 1.20);
    private static DecisionData TooGoodPass => Cube(1.30, 1.50);
    private static DecisionData TooGoodTake => Cube(1.10, 0.90);
    private static DecisionData Boundary => Cube(1.00, 1.20);

    /// <summary>
    /// Assert every bucket, plus that <c>Total</c> is their sum — the fold
    /// contract the consumer's matched count depends on.
    /// </summary>
    private static void AssertBuckets(
        AnswerTypeDistribution d,
        int checkerPlays, int noDoubleTake, int doubleTake, int doublePass,
        int tooGoodPass, int tooGoodTake)
    {
        Assert.Equal(checkerPlays, d.CheckerPlays);
        Assert.Equal(noDoubleTake, d.NoDoubleTake);
        Assert.Equal(doubleTake, d.DoubleTake);
        Assert.Equal(doublePass, d.DoublePass);
        Assert.Equal(tooGoodPass, d.TooGoodPass);
        Assert.Equal(tooGoodTake, d.TooGoodTake);
        Assert.Equal(
            checkerPlays + noDoubleTake + doubleTake + doublePass
                + tooGoodPass + tooGoodTake,
            d.Total);
    }

    private static AnswerTypeDistribution Fold(params DecisionData[] pool) =>
        pool.Aggregate(AnswerTypeDistribution.Empty, (acc, d) => acc.Add(d));

    // ---------------------------------------------------------------------
    //  Fixtures
    // ---------------------------------------------------------------------

    [Fact]
    public void Fixtures_ProduceTheFiveIntendedVerdictPairs()
    {
        Assert.Equal(CubeClaimPair.NoDoubleTake, NoDoubleTake.BestClaimPair);
        Assert.Equal(CubeClaimPair.DoubleTake, DoubleTake.BestClaimPair);
        Assert.Equal(CubeClaimPair.DoublePass, DoublePass.BestClaimPair);
        Assert.Equal(CubeClaimPair.TooGoodPass, TooGoodPass.BestClaimPair);
        Assert.Equal(CubeClaimPair.TooGoodTake, TooGoodTake.BestClaimPair);
    }

    [Fact]
    public void BoundaryFixture_ComposesTheIncoherentPair()
    {
        // E_ND == 1 exactly with E_DT >= 1: both halves tie and the ruled
        // tie-breaks compose (NoDouble, Pass) — the one reachable route to
        // the sixth cell as derived truth (SPEC-scoring §3, amended
        // 2026-09-01 at leg 1's landing).
        Assert.Equal(CubeClaimPair.NoDoublePass, Boundary.BestClaimPair);
        Assert.True(Boundary.BestClaimPair.IsIncoherent);
    }

    // ---------------------------------------------------------------------
    //  Empty
    // ---------------------------------------------------------------------

    [Fact]
    public void Empty_IsZeroAcrossAllBuckets()
    {
        AssertBuckets(AnswerTypeDistribution.Empty, 0, 0, 0, 0, 0, 0);
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
        AssertBuckets(Fold(Play), 1, 0, 0, 0, 0, 0);
    }

    [Fact]
    public void Add_NoDoubleTakeCube_LandsInNoDoubleTake()
    {
        AssertBuckets(Fold(NoDoubleTake), 0, 1, 0, 0, 0, 0);
    }

    [Fact]
    public void Add_DoubleTakeCube_LandsInDoubleTake()
    {
        AssertBuckets(Fold(DoubleTake), 0, 0, 1, 0, 0, 0);
    }

    [Fact]
    public void Add_DoublePassCube_LandsInDoublePass()
    {
        AssertBuckets(Fold(DoublePass), 0, 0, 0, 1, 0, 0);
    }

    [Fact]
    public void Add_TooGoodPassCube_LandsInTooGoodPass()
    {
        AssertBuckets(Fold(TooGoodPass), 0, 0, 0, 0, 1, 0);
    }

    [Fact]
    public void Add_TooGoodTakeCube_LandsInTooGoodTake_NotNoDoubleTake()
    {
        // halheinrich/backgammon#86's motivating case: before the claim
        // vocabulary this position's action pair (NoDouble, Take) filed it
        // under NoDoubleTake. AssertBuckets pins every other bucket at 0, so
        // the old misfiling would fail here by name.
        AssertBuckets(Fold(TooGoodTake), 0, 0, 0, 0, 0, 1);
    }

    [Fact]
    public void Add_BoundaryIncoherentPair_LandsInTooGoodPass()
    {
        // "(No Double, Pass) is just too good" (SPEC-scoring §3, ruled
        // 2026-09-01): the boundary-composed incoherent best pair houses in
        // the Too Good bucket, pass side — the too-good posture's degenerate
        // point, not a sixth bucket, and never a dropped or throwing cell.
        AssertBuckets(Fold(Boundary), 0, 0, 0, 0, 1, 0);
    }

    [Fact]
    public void AllFiveCubeVerdicts_AreDistinguished()
    {
        AssertBuckets(
            Fold(NoDoubleTake, DoubleTake, DoublePass, TooGoodPass, TooGoodTake),
            0, 1, 1, 1, 1, 1);
    }

    // ---------------------------------------------------------------------
    //  Fold contract — one bucket per Add, Total == decisions folded
    // ---------------------------------------------------------------------

    [Fact]
    public void CubeDecision_CountsOnce_NotOncePerHalf()
    {
        // Deliberately unlike QuizScore / ProblemStats, where a cube position
        // folds as two scored decisions. Pool membership counts rows.
        Assert.Equal(1, Fold(DoubleTake).Total);
    }

    [Fact]
    public void Total_EqualsTheNumberOfAddsPerformed()
    {
        DecisionData[] pool =
        [
            Play, NoDoubleTake, DoubleTake, DoublePass, TooGoodPass, TooGoodTake,
            Play, DoublePass, DoublePass, Boundary,
        ];

        var d = Fold(pool);

        Assert.Equal(pool.Length, d.Total);
        AssertBuckets(d, 2, 1, 1, 3, 2, 1);
    }

    [Fact]
    public void Add_LeavesTheReceiverUnchanged()
    {
        var start = Fold(Play);

        var next = start.Add(TooGoodPass);

        AssertBuckets(start, 1, 0, 0, 0, 0, 0);
        AssertBuckets(next, 1, 0, 0, 0, 1, 0);
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
        var a = new AnswerTypeDistribution(1, 2, 3, 4, 5, 6);
        var b = new AnswerTypeDistribution(10, 20, 30, 40, 50, 60);

        AssertBuckets(a + b, 11, 22, 33, 44, 55, 66);
    }

    [Fact]
    public void OperatorPlus_EmptyIsTheIdentity()
    {
        var d = new AnswerTypeDistribution(3, 1, 4, 1, 5, 9);

        Assert.Equal(d, d + AnswerTypeDistribution.Empty);
        Assert.Equal(d, AnswerTypeDistribution.Empty + d);
    }

    [Fact]
    public void OperatorPlus_MatchesSequentialAdds()
    {
        var sequential = Fold(
            Play, Play, NoDoubleTake, DoubleTake, DoublePass, TooGoodPass, TooGoodTake);

        var a = Fold(Play, NoDoubleTake, DoubleTake);
        var b = Fold(Play, DoublePass, TooGoodPass, TooGoodTake);

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
