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

    [Theory]
    [InlineData(7, 7, 0)]   // on-roll at the length
    [InlineData(7, 9, 0)]   // on-roll over the length
    [InlineData(7, 0, 7)]   // opponent at the length
    [InlineData(7, 0, 9)]   // opponent over the length
    public void FromScores_ScoreAtOrOverMatchLength_Throws(int matchLength, int onRoll, int opponent)
    {
        // A score at/over the length is a finished match, not a resumable
        // mid-match state.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MatchState.FromScores(matchLength, onRoll, opponent, isCrawford: false));
    }

    [Fact]
    public void FromScores_BothScoresAtLengthMinusOne_Allowed()
    {
        // Boundary: double match point is a legal resumable state.
        var match = MatchState.FromScores(matchLength: 7, onRollScore: 6, opponentScore: 6, isCrawford: false);

        Assert.False(match.IsMatchOver);
    }

    [Fact]
    public void FromScores_MoneyGame_ScoresUnbounded()
    {
        // MatchLength 0 has no finish line; accumulated money scores are free.
        var match = MatchState.FromScores(matchLength: 0, onRollScore: 25, opponentScore: 40, isCrawford: false);

        Assert.Equal(25, match.OnRollScore);
        Assert.Equal(40, match.OpponentScore);
        Assert.False(match.IsMatchOver);
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

    [Theory]
    [InlineData(0, true)]   // money game: cube in play
    [InlineData(1, false)]  // 1-point match: no cube, ever
    [InlineData(2, true)]
    [InlineData(7, true)]
    public void HasCube_FalseOnlyForOnePointMatch(int matchLength, bool expected)
    {
        Assert.Equal(expected, MatchState.NewMatch(matchLength).HasCube);
    }

    [Fact]
    public void NewMatch_OnePoint_CubelessAndNeverCrawford()
    {
        // A 1-point match has no cube to suspend, so no Crawford game exists —
        // the flag stays false for the match's whole (cubeless) life.
        var match = MatchState.NewMatch(1);

        Assert.False(match.HasCube);
        Assert.False(match.IsCrawford);
    }

    [Fact]
    public void FromScores_CrawfordInOnePointMatch_Throws()
    {
        // External conventions that mark 1-point-match games "Crawford" must be
        // normalized by the caller; the substrate fails fast on the combination.
        Assert.Throws<ArgumentException>(
            () => MatchState.FromScores(matchLength: 1, onRollScore: 0, opponentScore: 0, isCrawford: true));
    }

    [Fact]
    public void FromScores_CrawfordWithNeitherSideOneAway_Throws()
    {
        // Neither score is matchLength − 1 (6): a Crawford game can only be the one
        // right after the leader first reaches one-away, so this is pre-Crawford.
        Assert.Throws<ArgumentException>(
            () => MatchState.FromScores(matchLength: 7, onRollScore: 4, opponentScore: 3, isCrawford: true));
    }

    [Fact]
    public void FromScores_CrawfordWithBothSidesOneAway_Throws()
    {
        // Both scores at matchLength − 1 (6) is double match point — provably
        // post-Crawford, never a Crawford game.
        Assert.Throws<ArgumentException>(
            () => MatchState.FromScores(matchLength: 7, onRollScore: 6, opponentScore: 6, isCrawford: true));
    }

    [Fact]
    public void FromScores_CrawfordWithLeaderOneAway_Constructs()
    {
        // Guard boundary: leader exactly at matchLength − 1 (6), trailer strictly
        // below — the one valid Crawford position — constructs cleanly.
        var match = MatchState.FromScores(matchLength: 7, onRollScore: 6, opponentScore: 3, isCrawford: true);

        Assert.True(match.IsCrawford);
        Assert.Equal(6, match.OnRollScore);
        Assert.Equal(3, match.OpponentScore);
    }

    [Fact]
    public void AwardGame_OnePointMatch_EndsWithoutEnteringCrawford()
    {
        var match = MatchState.NewMatch(1);

        match.AwardGame(new GameResult(GameResultKind.WinSingle, OnRollWon: true, CubeSize: 1));

        Assert.True(match.IsMatchOver);
        Assert.False(match.IsCrawford);
    }
}
