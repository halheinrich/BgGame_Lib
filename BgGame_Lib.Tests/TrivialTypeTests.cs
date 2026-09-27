namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;

public class TrivialTypeTests
{
    [Fact]
    public void CubeAction_HasFourMembers()
    {
        var values = Enum.GetValues<CubeAction>();
        Assert.Equal(4, values.Length);
        Assert.Contains(CubeAction.NoDouble, values);
        Assert.Contains(CubeAction.Double, values);
        Assert.Contains(CubeAction.Take, values);
        Assert.Contains(CubeAction.Pass, values);
    }

    [Theory]
    [InlineData(GameResultKind.WinSingle, 1)]
    [InlineData(GameResultKind.WinGammon, 2)]
    [InlineData(GameResultKind.WinBackgammon, 3)]
    public void GameResult_PointsScalesByCubeSize(GameResultKind kind, int multiplier)
    {
        var resultUnit = new GameResult(kind, OnRollWon: true, CubeSize: 1);
        var resultDoubled = new GameResult(kind, OnRollWon: true, CubeSize: 8);

        Assert.Equal(multiplier, resultUnit.Points);
        Assert.Equal(multiplier * 8, resultDoubled.Points);
    }

    // Canonical key fixtures for the cube submission record (standard
    // starting board; the same position under two away-scores gives two
    // distinct keys). SubmittedPlay's equality is ruled and pinned with its
    // scoring, in PlaySubmissionTests.
    private const string Board = "0,-2,0,0,0,0,5,0,3,0,0,0,-5,5,0,0,0,-3,0,-5,0,0,0,0,2,0";

    // The answer is (Double, Pass) against a (Double, Take) truth — doubler
    // half right, taker half wrong. Correctness is derived from those two
    // pairs, so it is not a constructor argument here.
    private static readonly CubeClaimPair Answer = new(CubeClaim.Double, CubeAction.Pass);

    [Fact]
    public void SubmittedCubeAction_RecordEqualityHoldsByValue()
    {
        var cubeKey = ProblemKey.Parse($"{Board}/7a7/1c");

        var a = new SubmittedCubeAction(cubeKey, Answer, CubeClaimPair.DoubleTake,
            DoublerEquityLoss: 0.0, TakerEquityLoss: 0.08);
        var b = new SubmittedCubeAction(cubeKey, Answer, CubeClaimPair.DoubleTake,
            DoublerEquityLoss: 0.0, TakerEquityLoss: 0.08);

        Assert.Equal(a, b);
    }

    [Fact]
    public void SubmittedCubeAction_ProblemKeyParticipatesInEquality()
    {
        var a = new SubmittedCubeAction(ProblemKey.Parse($"{Board}/7a7/1c"), Answer,
            CubeClaimPair.DoubleTake, DoublerEquityLoss: 0.0, TakerEquityLoss: 0.08);
        var b = new SubmittedCubeAction(ProblemKey.Parse($"{Board}/5a3/1c"), Answer,
            CubeClaimPair.DoubleTake, DoublerEquityLoss: 0.0, TakerEquityLoss: 0.08);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void SubmittedCubeAction_BestDecisionParticipatesInEquality()
    {
        // The truth pair is part of the record's value: the same answer
        // scored against a different position is a different submission, and
        // the derived per-half verdicts differ with it.
        var cubeKey = ProblemKey.Parse($"{Board}/7a7/1c");

        var a = new SubmittedCubeAction(cubeKey, Answer, CubeClaimPair.DoubleTake,
            DoublerEquityLoss: 0.0, TakerEquityLoss: 0.08);
        var b = new SubmittedCubeAction(cubeKey, Answer, CubeClaimPair.DoublePass,
            DoublerEquityLoss: 0.0, TakerEquityLoss: 0.08);

        Assert.NotEqual(a, b);
        Assert.False(a.TakerCorrect);
        Assert.True(b.TakerCorrect);
    }
}
