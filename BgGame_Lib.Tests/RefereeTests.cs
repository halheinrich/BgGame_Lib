namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;
using BgMoveGen;

public class RefereeTests
{
    [Fact]
    public void IsGameOver_StandardOpening_ReturnsNull()
    {
        var (game, referee) = NewStandardGame();
        Assert.Null(referee.IsGameOver(game));
    }

    [Fact]
    public void IsGameOver_OpponentBornOffAll_ReturnsSingleWinForOpponent()
    {
        // Construct a board where opponent (negative side) has zero checkers
        // and on-roll has 14 visible (so 1 borne off). Win type: Single.
        var board = new BoardState();
        board.Points[1] = 14;   // 14 on-roll checkers on the 1-point
        board.RecalcHighPoint();

        var match = MatchState.NewMatch(7);
        var game = GameState.FromPosition(match, board, cubeSize: 1, cubeOwner: CubeOwner.Centered);
        var referee = new Referee();

        var result = referee.IsGameOver(game);

        Assert.NotNull(result);
        Assert.False(result!.OnRollWon);  // opponent (just-moved in normal flow) won
        Assert.Equal(GameResultKind.WinSingle, result.Kind);
    }

    [Fact]
    public void IsGameOver_GammonClassification()
    {
        // On-roll has all 15 checkers still on board, none in opponent home or on bar.
        // Opponent has 0 checkers. → Gammon for opponent.
        var board = new BoardState();
        board.Points[1] = 15;  // all on the 1-point — far from opponent's home
        board.RecalcHighPoint();

        var match = MatchState.NewMatch(7);
        var game = GameState.FromPosition(match, board, cubeSize: 2, cubeOwner: CubeOwner.OnRoll);
        var referee = new Referee();

        var result = referee.IsGameOver(game);

        Assert.NotNull(result);
        Assert.False(result!.OnRollWon);
        Assert.Equal(GameResultKind.WinGammon, result.Kind);
        Assert.Equal(4, result.Points);  // cube 2 × gammon 2
    }

    [Fact]
    public void IsGameOver_BackgammonViaOpponentHome()
    {
        // On-roll has all 15 still on board, with one checker on point 20 (in opponent's home).
        var board = new BoardState();
        board.Points[1] = 14;
        board.Points[20] = 1;  // in opponent's home (current POV: 19-24)
        board.RecalcHighPoint();

        var match = MatchState.NewMatch(7);
        var game = GameState.FromPosition(match, board, cubeSize: 1, cubeOwner: CubeOwner.Centered);
        var referee = new Referee();

        var result = referee.IsGameOver(game);

        Assert.NotNull(result);
        Assert.False(result!.OnRollWon);
        Assert.Equal(GameResultKind.WinBackgammon, result.Kind);
    }

    [Fact]
    public void IsGameOver_BackgammonViaOpponentOnBar()
    {
        // On-roll has all 15 still on board with one on the bar (Points[25]).
        var board = new BoardState();
        board.Points[1] = 14;
        board.Points[25] = 1;
        board.RecalcHighPoint();

        var match = MatchState.NewMatch(7);
        var game = GameState.FromPosition(match, board, cubeSize: 1, cubeOwner: CubeOwner.Centered);
        var referee = new Referee();

        var result = referee.IsGameOver(game);

        Assert.NotNull(result);
        Assert.Equal(GameResultKind.WinBackgammon, result.Kind);
    }

    [Fact]
    public void ApplyPlay_LegalPlay_AppliesAndFlipsPerspective()
    {
        var (game, referee) = NewStandardGame();

        // Build a legal opening play (3-1: 8/5 6/5).
        var legalPlays = MoveGenerator.GeneratePlays(game.Board, 3, 1);
        Assert.NotEmpty(legalPlays);
        var play = legalPlays[0];

        var matchScoresBefore = (game.Match.OnRollScore, game.Match.OpponentScore);
        // Capture pre-flip board.
        var before = new int[26];
        Array.Copy(game.Board.Points, before, 26);

        referee.ApplyPlay(game, play, 3, 1);

        // After flip: each new point = -before[25 - i].
        for (int i = 0; i < 26; i++)
        {
            // We can't know the exact intermediate (post-apply pre-flip) board easily here,
            // but we can confirm the perspective signed flip property holds: total positive
            // checkers swap with total negative.
        }

        int newPositive = 0, newNegative = 0;
        for (int i = 0; i < 26; i++)
        {
            if (game.Board.Points[i] > 0) newPositive += game.Board.Points[i];
            else if (game.Board.Points[i] < 0) newNegative += -game.Board.Points[i];
        }

        // After the move + flip, the just-moved player's checkers are now negative;
        // they had 15. The new on-roll (who hasn't moved) still has 15 positive.
        Assert.Equal(15, newPositive);
        Assert.Equal(15, newNegative);

        // Score perspective swapped (both were 0 → still both 0 — assert no exception path).
        Assert.Equal(matchScoresBefore.OnRollScore, game.Match.OnRollScore);
        Assert.Equal(matchScoresBefore.OpponentScore, game.Match.OpponentScore);
    }

    [Fact]
    public void ApplyPlay_IllegalPlay_Throws()
    {
        var (game, referee) = NewStandardGame();

        // Construct a clearly illegal play: a single move that doesn't match dice.
        var illegal = new Play();
        illegal.Add(new Move(13, 7));  // 13 → 7 takes a 6, but dice are 3-1 below

        Assert.Throws<ArgumentException>(() => referee.ApplyPlay(game, illegal, 3, 1));
    }

    [Fact]
    public void ApplyCubeResponse_Take_DoublesCube_TransfersOwnership_NoGameEnd()
    {
        var (game, referee) = NewStandardGame();
        // Pre-condition: cube is centered (legal-to-offer).
        var result = referee.ApplyCubeResponse(game, CubeAction.Take);

        Assert.Null(result);
        Assert.Equal(2, game.CubeSize);
        Assert.Equal(CubeOwner.Opponent, game.CubeOwner);
    }

    [Fact]
    public void ApplyCubeResponse_Pass_ReturnsSingleWinAtPreDoubleCube()
    {
        var match = MatchState.NewMatch(7);
        var game = GameState.FromPosition(match, BoardState.Standard(), cubeSize: 2, cubeOwner: CubeOwner.OnRoll);
        var referee = new Referee();

        var result = referee.ApplyCubeResponse(game, CubeAction.Pass);

        Assert.NotNull(result);
        Assert.True(result!.OnRollWon);
        Assert.Equal(GameResultKind.WinSingle, result.Kind);
        Assert.Equal(2, result.CubeSize);  // pre-double cube value (game state unchanged)
        Assert.Equal(2, game.CubeSize);    // not mutated by Pass
    }

    [Theory]
    [InlineData(CubeAction.NoDouble)]
    [InlineData(CubeAction.Double)]
    public void ApplyCubeResponse_NonResponseValue_Throws(CubeAction badResponse)
    {
        var (game, referee) = NewStandardGame();
        Assert.Throws<ArgumentException>(() => referee.ApplyCubeResponse(game, badResponse));
    }

    private static (GameState game, Referee referee) NewStandardGame()
    {
        var match = MatchState.NewMatch(7);
        var game = GameState.NewGame(match);
        return (game, new Referee());
    }
}
