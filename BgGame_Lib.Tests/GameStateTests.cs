namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;
using BgMoveGen;

public class GameStateTests
{
    [Fact]
    public void NewGame_StandardBoard_CubeOneCentered()
    {
        var match = MatchState.NewMatch(7);
        var game = GameState.NewGame(match);

        Assert.Same(match, game.Match);
        Assert.Equal(1, game.CubeSize);
        Assert.Equal(CubeOwner.Centered, game.CubeOwner);
        Assert.Equal(15, SumPositive(game.Board));
        Assert.Equal(15, SumNegative(game.Board));
    }

    [Fact]
    public void FromPosition_RetainsBoardReference()
    {
        var match = MatchState.NewMatch(7);
        var board = BoardState.Standard();
        var game = GameState.FromPosition(match, board, cubeSize: 2, cubeOwner: CubeOwner.OnRoll);

        Assert.Same(board, game.Board);
        Assert.Equal(2, game.CubeSize);
        Assert.Equal(CubeOwner.OnRoll, game.CubeOwner);
    }

    [Fact]
    public void FromPosition_NonPowerOfTwoCube_Throws()
    {
        var match = MatchState.NewMatch(7);
        Assert.Throws<ArgumentException>(() =>
            GameState.FromPosition(match, BoardState.Standard(), cubeSize: 3, cubeOwner: CubeOwner.OnRoll));
    }

    [Fact]
    public void FromPosition_ZeroCube_Throws()
    {
        var match = MatchState.NewMatch(7);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GameState.FromPosition(match, BoardState.Standard(), cubeSize: 0, cubeOwner: CubeOwner.OnRoll));
    }

    [Fact]
    public void DoubleCube_FromCentered_DoublesAndOpponentOwns()
    {
        var match = MatchState.NewMatch(7);
        var game = GameState.NewGame(match);

        game.DoubleCube();

        Assert.Equal(2, game.CubeSize);
        Assert.Equal(CubeOwner.Opponent, game.CubeOwner);
    }

    [Fact]
    public void DoubleCube_FromOnRoll_TransfersToOpponent()
    {
        var match = MatchState.NewMatch(7);
        var game = GameState.FromPosition(match, BoardState.Standard(), cubeSize: 2, cubeOwner: CubeOwner.OnRoll);

        game.DoubleCube();

        Assert.Equal(4, game.CubeSize);
        Assert.Equal(CubeOwner.Opponent, game.CubeOwner);
    }

    [Fact]
    public void DoubleCube_WhenOpponentOwns_Throws()
    {
        // From the on-roll player's POV, the cube is opponent-owned, so on-roll
        // cannot double here.
        var match = MatchState.NewMatch(7);
        var game = GameState.FromPosition(match, BoardState.Standard(), cubeSize: 2, cubeOwner: CubeOwner.Opponent);

        Assert.Throws<InvalidOperationException>(() => game.DoubleCube());
    }

    [Fact]
    public void Snapshot_DefensiveCopiesBoard()
    {
        var match = MatchState.NewMatch(7);
        var game = GameState.NewGame(match);
        var snap = game.Snapshot();

        var beforeSum = 0;
        foreach (var v in snap.Board) beforeSum += v;

        // Mutate the live board.
        game.Board.Points[1] += 100;

        var afterSum = 0;
        foreach (var v in snap.Board) afterSum += v;

        Assert.Equal(beforeSum, afterSum);
    }

    [Fact]
    public void Snapshot_CapturesMatchAtSameInstant()
    {
        var match = MatchState.FromScores(matchLength: 7, onRollScore: 3, opponentScore: 2, isCrawford: false);
        var game = GameState.FromPosition(match, BoardState.Standard(), cubeSize: 4, cubeOwner: CubeOwner.OnRoll);

        var snap = game.Snapshot();

        Assert.Equal(4, snap.CubeSize);
        Assert.Equal(CubeOwner.OnRoll, snap.CubeOwner);
        Assert.Equal(3, snap.Match.OnRollScore);
        Assert.Equal(2, snap.Match.OpponentScore);

        // Subsequent match mutation does not retroactively change the snapshot.
        match.AwardGame(new GameResult(GameResultKind.WinSingle, OnRollWon: true, CubeSize: 1));
        Assert.Equal(3, snap.Match.OnRollScore);
    }

    [Fact]
    public void SwapPerspective_Internal_FlipsBoardPointsAndCube()
    {
        var match = MatchState.FromScores(matchLength: 7, onRollScore: 3, opponentScore: 1, isCrawford: false);
        var board = BoardState.Standard();
        var game = GameState.FromPosition(match, board, cubeSize: 2, cubeOwner: CubeOwner.OnRoll);

        // Capture original state.
        var originalPoints = new int[26];
        Array.Copy(board.Points, originalPoints, 26);

        game.SwapPerspective();

        // Each point i in the new state should equal -original[25 - i].
        for (int i = 0; i < 26; i++)
            Assert.Equal(-originalPoints[25 - i], board.Points[i]);

        // Cube ownership flipped, scores swapped.
        Assert.Equal(CubeOwner.Opponent, game.CubeOwner);
        Assert.Equal(1, match.OnRollScore);
        Assert.Equal(3, match.OpponentScore);
    }

    [Fact]
    public void SwapPerspective_Twice_RestoresOriginal()
    {
        var match = MatchState.NewMatch(7);
        var board = BoardState.Standard();
        var game = GameState.FromPosition(match, board, cubeSize: 2, cubeOwner: CubeOwner.OnRoll);

        var originalPoints = new int[26];
        Array.Copy(board.Points, originalPoints, 26);

        game.SwapPerspective();
        game.SwapPerspective();

        for (int i = 0; i < 26; i++)
            Assert.Equal(originalPoints[i], board.Points[i]);
        Assert.Equal(CubeOwner.OnRoll, game.CubeOwner);
    }

    [Fact]
    public void SwapPerspective_CenteredCubeStaysCentered()
    {
        var match = MatchState.NewMatch(7);
        var game = GameState.NewGame(match);

        game.SwapPerspective();

        Assert.Equal(CubeOwner.Centered, game.CubeOwner);
    }

    private static int SumPositive(BoardState b)
    {
        int s = 0;
        for (int i = 0; i < 26; i++) if (b.Points[i] > 0) s += b.Points[i];
        return s;
    }

    private static int SumNegative(BoardState b)
    {
        int s = 0;
        for (int i = 0; i < 26; i++) if (b.Points[i] < 0) s += -b.Points[i];
        return s;
    }
}
