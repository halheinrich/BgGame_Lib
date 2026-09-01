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

    // Canonical key fixtures for the submission records (standard starting
    // board; the same position under two away-scores gives two distinct keys).
    private const string Board = "0,-2,0,0,0,0,5,0,3,0,0,0,-5,5,0,0,0,-3,0,-5,0,0,0,0,2,0";
    private static readonly ProblemKey KeyA = ProblemKey.Parse($"{Board}/7a7/1c/31");
    private static readonly ProblemKey KeyB = ProblemKey.Parse($"{Board}/5a3/1c/31");

    [Fact]
    public void SubmittedPlay_RecordEqualityHoldsByValue()
    {
        Play play = [new(13, 7)];

        var a = new SubmittedPlay(KeyA, play, MatchedCandidateIndex: 0, EquityLoss: 0.0, IsCorrect: true);
        var b = new SubmittedPlay(KeyA, play, MatchedCandidateIndex: 0, EquityLoss: 0.0, IsCorrect: true);

        Assert.Equal(a, b);
    }

    [Fact]
    public void SubmittedPlay_ProblemKeyParticipatesInEquality()
    {
        Play play = [new(13, 7)];

        var a = new SubmittedPlay(KeyA, play, MatchedCandidateIndex: 0, EquityLoss: 0.0, IsCorrect: true);
        var b = new SubmittedPlay(KeyB, play, MatchedCandidateIndex: 0, EquityLoss: 0.0, IsCorrect: true);

        Assert.NotEqual(a, b);
        // The no-key rung is a value too: null-key submissions compare equal
        // to each other and unequal to any keyed one.
        Assert.Equal(a with { ProblemKey = null }, b with { ProblemKey = null });
        Assert.NotEqual(a, a with { ProblemKey = null });
    }

    [Fact]
    public void SubmittedCubeAction_RecordEqualityHoldsByValue()
    {
        var decision = new CubeDecisionPair(CubeAction.Double, CubeAction.Take);
        var cubeKey = ProblemKey.Parse($"{Board}/7a7/1c");

        var a = new SubmittedCubeAction(cubeKey, decision,
            DoublerEquityLoss: 0.0, TakerEquityLoss: 0.08, DoublerCorrect: true, TakerCorrect: false);
        var b = new SubmittedCubeAction(cubeKey, decision,
            DoublerEquityLoss: 0.0, TakerEquityLoss: 0.08, DoublerCorrect: true, TakerCorrect: false);

        Assert.Equal(a, b);
    }

    [Fact]
    public void SubmittedCubeAction_ProblemKeyParticipatesInEquality()
    {
        var decision = new CubeDecisionPair(CubeAction.Double, CubeAction.Take);

        var a = new SubmittedCubeAction(ProblemKey.Parse($"{Board}/7a7/1c"), decision,
            DoublerEquityLoss: 0.0, TakerEquityLoss: 0.08, DoublerCorrect: true, TakerCorrect: false);
        var b = new SubmittedCubeAction(ProblemKey.Parse($"{Board}/5a3/1c"), decision,
            DoublerEquityLoss: 0.0, TakerEquityLoss: 0.08, DoublerCorrect: true, TakerCorrect: false);

        Assert.NotEqual(a, b);
    }
}
