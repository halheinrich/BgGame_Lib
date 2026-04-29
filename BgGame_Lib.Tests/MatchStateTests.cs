namespace BgGame_Lib.Tests;

public class MatchStateTests
{
    [Fact]
    public void NewMatch_ZeroScores_NotCrawford_NotOver()
    {
        var match = MatchState.NewMatch(7);

        Assert.Equal(7, match.MatchLength);
        Assert.Equal(0, match.OnRollScore);
        Assert.Equal(0, match.OpponentScore);
        Assert.False(match.IsCrawford);
        Assert.False(match.IsMatchOver);
    }

    [Fact]
    public void NewMatch_NegativeLength_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MatchState.NewMatch(-1));
    }

    [Fact]
    public void FromScores_SetsAllFields()
    {
        var match = MatchState.FromScores(matchLength: 11, onRollScore: 4, opponentScore: 9, isCrawford: false);

        Assert.Equal(11, match.MatchLength);
        Assert.Equal(4, match.OnRollScore);
        Assert.Equal(9, match.OpponentScore);
        Assert.False(match.IsCrawford);
    }

    [Fact]
    public void FromScores_CrawfordInMoneyGame_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => MatchState.FromScores(matchLength: 0, onRollScore: 0, opponentScore: 0, isCrawford: true));
    }

    [Fact]
    public void AwardGame_OnRollWin_AddsToOnRollScore()
    {
        var match = MatchState.NewMatch(7);
        var result = new GameResult(GameResultKind.WinGammon, OnRollWon: true, CubeSize: 1);

        match.AwardGame(result);

        Assert.Equal(2, match.OnRollScore);
        Assert.Equal(0, match.OpponentScore);
    }

    [Fact]
    public void AwardGame_OpponentWin_AddsToOpponentScore()
    {
        var match = MatchState.NewMatch(7);
        var result = new GameResult(GameResultKind.WinSingle, OnRollWon: false, CubeSize: 4);

        match.AwardGame(result);

        Assert.Equal(0, match.OnRollScore);
        Assert.Equal(4, match.OpponentScore);
    }

    [Fact]
    public void AwardGame_CrossesIntoCrawford()
    {
        // 7-point match, score 4–3. On-roll wins 2 points (gammon at 1-cube) → 6–3.
        // Leader at MatchLength − 1 = 6. Next game is Crawford.
        var match = MatchState.FromScores(matchLength: 7, onRollScore: 4, opponentScore: 3, isCrawford: false);
        match.AwardGame(new GameResult(GameResultKind.WinGammon, OnRollWon: true, CubeSize: 1));

        Assert.Equal(6, match.OnRollScore);
        Assert.True(match.IsCrawford);
    }

    [Fact]
    public void AwardGame_CrawfordGame_Played_TransitionsToPostCrawford()
    {
        // Crawford game ends; next game is post-Crawford (IsCrawford = false).
        var match = MatchState.FromScores(matchLength: 7, onRollScore: 6, opponentScore: 3, isCrawford: true);
        match.AwardGame(new GameResult(GameResultKind.WinSingle, OnRollWon: false, CubeSize: 1));

        Assert.False(match.IsCrawford);
        Assert.Equal(6, match.OnRollScore);
        Assert.Equal(4, match.OpponentScore);
    }

    [Fact]
    public void AwardGame_MatchEndingWin_ClearsCrawford_FlagsMatchOver()
    {
        var match = MatchState.FromScores(matchLength: 7, onRollScore: 5, opponentScore: 4, isCrawford: false);
        match.AwardGame(new GameResult(GameResultKind.WinGammon, OnRollWon: true, CubeSize: 2));

        Assert.True(match.IsMatchOver);
        Assert.False(match.IsCrawford);
        Assert.True(match.OnRollScore >= match.MatchLength);
    }

    [Fact]
    public void AwardGame_MoneyGame_NoCrawfordTransition()
    {
        var match = MatchState.NewMatch(0);
        match.AwardGame(new GameResult(GameResultKind.WinSingle, OnRollWon: true, CubeSize: 8));

        Assert.False(match.IsCrawford);
        Assert.False(match.IsMatchOver);
        Assert.Equal(8, match.OnRollScore);
    }

    [Fact]
    public void SwapPerspective_Internal_SwapsScoreLabels()
    {
        var match = MatchState.FromScores(matchLength: 7, onRollScore: 5, opponentScore: 2, isCrawford: false);

        match.SwapPerspective();

        Assert.Equal(2, match.OnRollScore);
        Assert.Equal(5, match.OpponentScore);
    }

    [Fact]
    public void Snapshot_CapturesAllFields()
    {
        var match = MatchState.FromScores(matchLength: 7, onRollScore: 6, opponentScore: 5, isCrawford: true);
        var snap = match.Snapshot();

        Assert.Equal(7, snap.MatchLength);
        Assert.Equal(6, snap.OnRollScore);
        Assert.Equal(5, snap.OpponentScore);
        Assert.True(snap.IsCrawford);
    }
}
