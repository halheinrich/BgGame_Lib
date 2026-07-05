namespace BgGame_Lib.Tests;

using System.Globalization;
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

    [Fact]
    public void SubmittedPlay_RecordEqualityHoldsByValue()
    {
        var play = new Play();
        play.Add(new Move(13, 7));
        var id = DecisionId.Parse("problem.xgp", CultureInfo.InvariantCulture);

        var a = new SubmittedPlay(id, play, MatchedCandidateIndex: 0, EquityLoss: 0.0, IsCorrect: true);
        var b = new SubmittedPlay(id, play, MatchedCandidateIndex: 0, EquityLoss: 0.0, IsCorrect: true);

        Assert.Equal(a, b);
    }

    [Fact]
    public void SubmittedPlay_DecisionIdParticipatesInEquality()
    {
        var play = new Play();
        play.Add(new Move(13, 7));

        var a = new SubmittedPlay(DecisionId.Parse("problem-a.xgp", CultureInfo.InvariantCulture),
            play, MatchedCandidateIndex: 0, EquityLoss: 0.0, IsCorrect: true);
        var b = new SubmittedPlay(DecisionId.Parse("problem-b.xgp", CultureInfo.InvariantCulture),
            play, MatchedCandidateIndex: 0, EquityLoss: 0.0, IsCorrect: true);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void SubmittedCubeAction_RecordEqualityHoldsByValue()
    {
        var decision = new CubeDecisionPair(CubeAction.Double, CubeAction.Take);
        var id = DecisionId.Parse("problem.xgp", CultureInfo.InvariantCulture);

        var a = new SubmittedCubeAction(id, decision,
            DoublerEquityLoss: 0.0, TakerEquityLoss: 0.08, DoublerCorrect: true, TakerCorrect: false);
        var b = new SubmittedCubeAction(id, decision,
            DoublerEquityLoss: 0.0, TakerEquityLoss: 0.08, DoublerCorrect: true, TakerCorrect: false);

        Assert.Equal(a, b);
    }

    [Fact]
    public void SubmittedCubeAction_DecisionIdParticipatesInEquality()
    {
        var decision = new CubeDecisionPair(CubeAction.Double, CubeAction.Take);

        var a = new SubmittedCubeAction(DecisionId.Parse("problem-a.xgp", CultureInfo.InvariantCulture),
            decision, DoublerEquityLoss: 0.0, TakerEquityLoss: 0.08, DoublerCorrect: true, TakerCorrect: false);
        var b = new SubmittedCubeAction(DecisionId.Parse("problem-b.xgp", CultureInfo.InvariantCulture),
            decision, DoublerEquityLoss: 0.0, TakerEquityLoss: 0.08, DoublerCorrect: true, TakerCorrect: false);

        Assert.NotEqual(a, b);
    }
}
