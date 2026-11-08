namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;

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

    // ── The Crawford game is played once (halheinrich/backgammon#532) ──
    //
    // The leader stays one-away after the Crawford game until the match ends,
    // so a flag re-derived from the leader's score alone would come back on
    // every second post-Crawford game.

    private static readonly GameResult TrailerWinsSingle =
        new(GameResultKind.WinSingle, OnRollWon: false, CubeSize: 1);

    [Fact]
    public void AwardGame_EveryGameAfterTheCrawfordGame_IsPostCrawford()
    {
        // Live from 0–0: the leader reaches 6 with a doubled backgammon, the
        // Crawford game follows, then the trailer wins single games through
        // double match point. Only the 6–0 game is Crawford.
        var match = MatchState.NewMatch(7);
        match.AwardGame(new GameResult(GameResultKind.WinBackgammon, OnRollWon: true, CubeSize: 2));
        Assert.Equal((6, 0), (match.OnRollScore, match.OpponentScore));
        Assert.True(match.IsCrawford);

        for (int trailer = 1; trailer <= 6; trailer++)
        {
            match.AwardGame(TrailerWinsSingle);

            Assert.Equal((6, trailer), (match.OnRollScore, match.OpponentScore));
            Assert.False(match.IsCrawford, $"6–{trailer} is post-Crawford");
        }
    }

    [Fact]
    public void AwardGame_ResumedPostCrawford_StaysPostCrawford()
    {
        // A resumed state with the leader one-away and the flag clear is
        // post-Crawford: the Crawford game has been played.
        var match = MatchState.FromScores(matchLength: 7, onRollScore: 6, opponentScore: 1, isCrawford: false);

        match.AwardGame(TrailerWinsSingle);

        Assert.Equal((6, 2), (match.OnRollScore, match.OpponentScore));
        Assert.False(match.IsCrawford);
    }

    [Fact]
    public void AwardGame_PostCrawford_AfterSwapPerspective_StaysPostCrawford()
    {
        // The phase is read from both sides' scores, not from the on-roll label:
        // once the labels swap, the leader is the opponent and the trailer's win
        // is credited on roll.
        var match = MatchState.FromScores(matchLength: 7, onRollScore: 6, opponentScore: 0, isCrawford: true);
        match.AwardGame(TrailerWinsSingle);
        Assert.False(match.IsCrawford);

        match.SwapPerspective();
        Assert.Equal((1, 6), (match.OnRollScore, match.OpponentScore));
        match.AwardGame(new GameResult(GameResultKind.WinSingle, OnRollWon: true, CubeSize: 1));

        Assert.Equal((2, 6), (match.OnRollScore, match.OpponentScore));
        Assert.False(match.IsCrawford);
    }

    [Fact]
    public void AwardGame_DoubleMatchPointReachedPostCrawford_IsNotCrawford()
    {
        // Both sides one-away is double match point: provably post-Crawford.
        var match = MatchState.FromScores(matchLength: 7, onRollScore: 6, opponentScore: 5, isCrawford: false);

        match.AwardGame(TrailerWinsSingle);

        Assert.Equal((6, 6), (match.OnRollScore, match.OpponentScore));
        Assert.False(match.IsCrawford);
        Assert.False(match.IsMatchOver);
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

    // ── The away counts: what each side still needs (halheinrich/backgammon#533) ──

    [Theory]
    [InlineData(5, 0, 0)]
    [InlineData(7, 2, 2)]
    public void Snapshot_EquivalentScores_ReadTheSameAwayCounts(int matchLength, int onRoll, int opponent)
    {
        // 0–0 in a 5-point match and 2–2 in a 7-point match: 5-away each.
        var snap = MatchState.FromScores(matchLength, onRoll, opponent, isCrawford: false).Snapshot();

        Assert.Equal(5, snap.OnRollNeeds);
        Assert.Equal(5, snap.OpponentNeeds);
    }

    [Fact]
    public void Snapshot_UnequalScores_KeepTheirOrientation()
    {
        var snap = MatchState.FromScores(matchLength: 7, onRollScore: 3, opponentScore: 2, isCrawford: false).Snapshot();

        Assert.Equal(4, snap.OnRollNeeds);
        Assert.Equal(5, snap.OpponentNeeds);

        // The captured scores stay what they were.
        Assert.Equal((3, 2), (snap.OnRollScore, snap.OpponentScore));
    }

    [Fact]
    public void Snapshot_AwayCountsFollowTheTurnBoundaryFlip()
    {
        var match = MatchState.FromScores(matchLength: 7, onRollScore: 3, opponentScore: 2, isCrawford: false);
        match.SwapPerspective();

        var snap = match.Snapshot();

        Assert.Equal(5, snap.OnRollNeeds);
        Assert.Equal(4, snap.OpponentNeeds);
    }

    [Fact]
    public void Snapshot_MoneyGame_HasNoAwayCount()
    {
        var match = MatchState.FromScores(matchLength: 0, onRollScore: 4, opponentScore: 9, isCrawford: false);

        var snap = match.Snapshot();

        Assert.Null(snap.OnRollNeeds);
        Assert.Null(snap.OpponentNeeds);
        Assert.Equal((4, 9), (snap.OnRollScore, snap.OpponentScore));
    }

    [Fact]
    public void Snapshot_AwayCountsAreDerived_NotPartOfEquality()
    {
        // Two captures of one standing are one value; the away counts add no state.
        var first = new MatchSnapshot(7, 3, 2, IsCrawford: false);
        var second = MatchState.FromScores(7, 3, 2, isCrawford: false).Snapshot();

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
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

    [Theory]
    [InlineData(7, 4, 3)]   // neither side one-away: pre-Crawford
    [InlineData(7, 6, 6)]   // both one-away: double match point
    [InlineData(1, 0, 0)]   // 1-point match: both one-away from the start
    public void FromScores_CrawfordRefusal_IsTheMatchStandingRefusal(int matchLength, int onRoll, int opponent)
    {
        // The Crawford standing is BgDataTypes_Lib's rule; FromScores carries
        // its refusal rather than restating it.
        Assert.False(MatchStanding.TryCreate(
            matchLength - onRoll, matchLength - opponent, isCrawford: true, out _, out string? refusal));

        var ex = Assert.Throws<ArgumentException>(
            () => MatchState.FromScores(matchLength, onRoll, opponent, isCrawford: true));

        Assert.StartsWith(refusal, ex.Message, StringComparison.Ordinal);
        Assert.Equal("isCrawford", ex.ParamName);
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
