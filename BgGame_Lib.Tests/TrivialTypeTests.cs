namespace BgGame_Lib.Tests;

using BgMoveGen;

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

        var a = new SubmittedPlay(play, MatchedCandidateIndex: 0, EquityLoss: 0.0, IsCorrect: true);
        var b = new SubmittedPlay(play, MatchedCandidateIndex: 0, EquityLoss: 0.0, IsCorrect: true);

        Assert.Equal(a, b);
    }
}
