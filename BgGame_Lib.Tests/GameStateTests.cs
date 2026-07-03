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
    public void DoubleCube_WouldOverflow_ThrowsAndLeavesStateUnchanged()
    {
        // Doubling 2^30 gives 2^31, one past int.MaxValue; the checked
        // multiply must fail fast, not wrap negative.
        var match = MatchState.NewMatch(7);
        var game = GameState.FromPosition(match, BoardState.Standard(), cubeSize: 1 << 30, cubeOwner: CubeOwner.OnRoll);

        Assert.Throws<OverflowException>(() => game.DoubleCube());

        Assert.Equal(1 << 30, game.CubeSize);
        Assert.Equal(CubeOwner.OnRoll, game.CubeOwner);
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
    public void ApplyPlay_LegalPlay_FlipsBoardScoresAndCube()
    {
        var match = MatchState.FromScores(matchLength: 7, onRollScore: 3, opponentScore: 1, isCrawford: false);
        var game = GameState.FromPosition(match, BoardState.Standard(), cubeSize: 2, cubeOwner: CubeOwner.OnRoll);

        // Pick any legal opening play for 3-1.
        var legalPlays = MoveGenerator.GeneratePlays(game.Board, 3, 1);
        Assert.NotEmpty(legalPlays);
        var play = legalPlays[0];

        game.ApplyPlay(play, 3, 1);

        // After apply + flip: previous on-roll's checkers are now negative.
        int newPositive = 0, newNegative = 0;
        for (int i = 0; i < 26; i++)
        {
            if (game.Board.Points[i] > 0) newPositive += game.Board.Points[i];
            else if (game.Board.Points[i] < 0) newNegative += -game.Board.Points[i];
        }
        Assert.Equal(15, newPositive);
        Assert.Equal(15, newNegative);

        // Match score labels swapped.
        Assert.Equal(1, match.OnRollScore);
        Assert.Equal(3, match.OpponentScore);

        // Cube-owner label flipped.
        Assert.Equal(CubeOwner.Opponent, game.CubeOwner);
    }

    [Fact]
    public void ApplyPlay_CenteredCubeStaysCentered()
    {
        var match = MatchState.NewMatch(7);
        var game = GameState.NewGame(match);

        var legalPlays = MoveGenerator.GeneratePlays(game.Board, 3, 1);
        game.ApplyPlay(legalPlays[0], 3, 1);

        Assert.Equal(CubeOwner.Centered, game.CubeOwner);
    }

    [Fact]
    public void ApplyPlay_TwoLegalPlays_RestoresInitialPerspectiveLabels()
    {
        // Two ApplyPlays with empty (no-checker) plays — but the board does have
        // checkers, so an empty Play would be illegal. Use legal forced-pass-shape:
        // pick two compatible legal plays whose composition cancels match/cube
        // labels back to the start. Match/cube label flip is independent of which
        // legal play is chosen — exercise it with any legal pair.
        var match = MatchState.FromScores(matchLength: 7, onRollScore: 3, opponentScore: 1, isCrawford: false);
        var game = GameState.FromPosition(match, BoardState.Standard(), cubeSize: 2, cubeOwner: CubeOwner.OnRoll);

        var first = MoveGenerator.GeneratePlays(game.Board, 3, 1)[0];
        game.ApplyPlay(first, 3, 1);

        // After one flip, on-roll = original opponent. Pick any legal play for them.
        var second = MoveGenerator.GeneratePlays(game.Board, 4, 2)[0];
        game.ApplyPlay(second, 4, 2);

        // Match score labels back to original orientation.
        Assert.Equal(3, match.OnRollScore);
        Assert.Equal(1, match.OpponentScore);
        // Cube ownership back to original.
        Assert.Equal(CubeOwner.OnRoll, game.CubeOwner);
    }

    [Fact]
    public void ApplyPlay_IllegalPlay_Throws_LeavesStateUnchanged()
    {
        var match = MatchState.FromScores(matchLength: 7, onRollScore: 3, opponentScore: 1, isCrawford: false);
        var game = GameState.FromPosition(match, BoardState.Standard(), cubeSize: 2, cubeOwner: CubeOwner.OnRoll);

        var pointsBefore = new int[26];
        Array.Copy(game.Board.Points, pointsBefore, 26);
        var onRollBefore = match.OnRollScore;
        var opponentBefore = match.OpponentScore;
        var cubeOwnerBefore = game.CubeOwner;

        // 13→7 takes a 6, but dice are 3-1 — illegal.
        var illegal = new Play();
        illegal.Add(new Move(13, 7));

        Assert.Throws<ArgumentException>(() => game.ApplyPlay(illegal, 3, 1));

        // Throw-before-mutate: every field is unchanged.
        for (int i = 0; i < 26; i++)
            Assert.Equal(pointsBefore[i], game.Board.Points[i]);
        Assert.Equal(onRollBefore, match.OnRollScore);
        Assert.Equal(opponentBefore, match.OpponentScore);
        Assert.Equal(cubeOwnerBefore, game.CubeOwner);
    }

    [Fact]
    public void ApplyPlay_Dance_EmptyPlayIsLegal_FlipsPerspectiveOnly()
    {
        // On-roll dances: one checker on the bar, both entry points blocked.
        // GeneratePlays yields exactly the empty play, and ApplyPlay accepts it
        // as a normal turn transition (zero moves + full perspective flip).
        var mop = new int[26];
        mop[25] = 1;    // on-roll checker on the bar
        mop[6] = 14;    // rest of on-roll's checkers
        mop[22] = -2;   // blocks bar entry with a 3 (25 − 3)
        mop[24] = -2;   // blocks bar entry with a 1 (25 − 1)
        mop[1] = -11;   // rest of opponent's checkers
        var board = BoardState.FromMop(mop);
        var match = MatchState.FromScores(matchLength: 7, onRollScore: 3, opponentScore: 1, isCrawford: false);
        var game = GameState.FromPosition(match, board, cubeSize: 2, cubeOwner: CubeOwner.OnRoll);

        var legal = MoveGenerator.GeneratePlays(game.Board, 3, 1);
        var dance = Assert.Single(legal);
        Assert.Equal(0, dance.Count);

        game.ApplyPlay(dance, 3, 1);

        // Full turn transition: scores and cube-owner labels flipped...
        Assert.Equal(1, match.OnRollScore);
        Assert.Equal(3, match.OpponentScore);
        Assert.Equal(CubeOwner.Opponent, game.CubeOwner);
        // ...and the board flipped with no checkers moved: the dancer's bar
        // checker now reads as the opponent's bar ([25] → [0], negated).
        Assert.Equal(-1, game.Board.Points[0]);
        Assert.Equal(-14, game.Board.Points[19]);
    }

    // ── Cube legality: substrate-enforced (CanDouble + DoubleCube guards) ──

    [Theory]
    [InlineData(CubeOwner.Centered, true)]
    [InlineData(CubeOwner.OnRoll, true)]
    [InlineData(CubeOwner.Opponent, false)]
    public void CanDouble_ReflectsCubeOwnership(CubeOwner owner, bool expected)
    {
        var match = MatchState.NewMatch(7);
        var game = GameState.FromPosition(match, BoardState.Standard(), cubeSize: 2, cubeOwner: owner);

        Assert.Equal(expected, game.CanDouble);
    }

    [Fact]
    public void CanDouble_MoneyGame_True()
    {
        var match = MatchState.NewMatch(0);
        var game = GameState.NewGame(match);

        Assert.True(game.CanDouble);
    }

    [Fact]
    public void CanDouble_CrawfordGame_False()
    {
        var match = MatchState.FromScores(matchLength: 5, onRollScore: 4, opponentScore: 2, isCrawford: true);
        var game = GameState.NewGame(match);

        Assert.False(game.CanDouble);
    }

    [Fact]
    public void DoubleCube_CrawfordGame_Throws()
    {
        var match = MatchState.FromScores(matchLength: 5, onRollScore: 4, opponentScore: 2, isCrawford: true);
        var game = GameState.NewGame(match);

        var ex = Assert.Throws<InvalidOperationException>(() => game.DoubleCube());
        Assert.Contains("Crawford", ex.Message);
    }

    [Fact]
    public void CanDouble_OnePointMatch_False()
    {
        // No cube in a 1-point match — regardless of nominal ownership.
        var match = MatchState.NewMatch(1);
        var game = GameState.NewGame(match);

        Assert.False(game.CanDouble);
    }

    [Fact]
    public void DoubleCube_OnePointMatch_Throws()
    {
        var match = MatchState.NewMatch(1);
        var game = GameState.NewGame(match);

        var ex = Assert.Throws<InvalidOperationException>(() => game.DoubleCube());
        Assert.Contains("1-point match", ex.Message);
    }

    [Fact]
    public void OnePointMatch_SnapshotReadsAsCubeless()
    {
        // No special casing anywhere: the cube simply never moves off 1/centered,
        // and the snapshot reports exactly that.
        var match = MatchState.NewMatch(1);
        var game = GameState.NewGame(match);
        var snap = game.Snapshot();

        Assert.Equal(1, snap.CubeSize);
        Assert.Equal(CubeOwner.Centered, snap.CubeOwner);
        Assert.False(snap.Match.IsCrawford);
    }

    [Fact]
    public void DoubleCube_PostCrawford_Allowed()
    {
        // Crawford game played (flag cleared); doubling resumes.
        var match = MatchState.FromScores(matchLength: 5, onRollScore: 4, opponentScore: 2, isCrawford: false);
        var game = GameState.NewGame(match);

        game.DoubleCube();

        Assert.Equal(2, game.CubeSize);
        Assert.Equal(CubeOwner.Opponent, game.CubeOwner);
    }

    // ── OpponentView: detached responder-frame query view ─────────

    /// <summary>
    /// Deliberately flip-asymmetric position (unlike the standard start, which
    /// is flip-symmetric and would let a no-op pass the board assertions).
    /// </summary>
    private static BoardState AsymmetricBoard()
    {
        var mop = new int[26];
        mop[24] = 2; mop[13] = 4; mop[8] = 4; mop[6] = 5;      // on-roll: 15
        mop[19] = -5; mop[17] = -3; mop[12] = -5; mop[1] = -2; // opponent: 15
        return BoardState.FromMop(mop);
    }

    [Fact]
    public void OpponentView_FlipsBoard_SwapsScores_PreservesLengthCrawfordAndCubeSize()
    {
        var match = MatchState.FromScores(matchLength: 7, onRollScore: 3, opponentScore: 1, isCrawford: false);
        var game = GameState.FromPosition(match, AsymmetricBoard(), cubeSize: 4, cubeOwner: CubeOwner.OnRoll);

        var view = game.OpponentView();

        for (int i = 0; i < 26; i++)
            Assert.Equal(-game.Board.Points[25 - i], view.Board.Points[i]);
        Assert.Equal(1, view.Match.OnRollScore);
        Assert.Equal(3, view.Match.OpponentScore);
        Assert.Equal(7, view.Match.MatchLength);
        Assert.False(view.Match.IsCrawford);
        Assert.Equal(4, view.CubeSize);
    }

    [Theory]
    [InlineData(CubeOwner.Centered, CubeOwner.Centered)]
    [InlineData(CubeOwner.OnRoll, CubeOwner.Opponent)]
    [InlineData(CubeOwner.Opponent, CubeOwner.OnRoll)]
    public void OpponentView_MirrorsCubeOwner(CubeOwner owner, CubeOwner expected)
    {
        var match = MatchState.NewMatch(7);
        var game = GameState.FromPosition(match, AsymmetricBoard(), cubeSize: 2, cubeOwner: owner);

        Assert.Equal(expected, game.OpponentView().CubeOwner);
    }

    [Fact]
    public void OpponentView_PreservesCrawford()
    {
        var match = MatchState.FromScores(matchLength: 5, onRollScore: 4, opponentScore: 2, isCrawford: true);
        var game = GameState.NewGame(match);

        var view = game.OpponentView();

        Assert.True(view.Match.IsCrawford);
        Assert.Equal(2, view.Match.OnRollScore);
        Assert.Equal(4, view.Match.OpponentScore);
    }

    [Fact]
    public void OpponentView_IsDetached_MutationsDoNotCrossOver()
    {
        var match = MatchState.FromScores(matchLength: 7, onRollScore: 3, opponentScore: 1, isCrawford: false);
        var game = GameState.FromPosition(match, AsymmetricBoard(), cubeSize: 1, cubeOwner: CubeOwner.Centered);

        var view = game.OpponentView();
        Assert.NotSame(game.Board, view.Board);
        Assert.NotSame(game.Match, view.Match);

        // Mutating the view leaves the live state untouched...
        view.DoubleCube();
        view.Board.Points[6] -= 1;
        Assert.Equal(1, game.CubeSize);
        Assert.Equal(CubeOwner.Centered, game.CubeOwner);
        Assert.Equal(5, game.Board.Points[6]);

        // ...and the view does not track subsequent live mutations.
        game.Board.Points[24] -= 1;
        Assert.Equal(-2, view.Board.Points[1]);
    }

    [Fact]
    public void OpponentView_IsAnInvolution()
    {
        var match = MatchState.FromScores(matchLength: 7, onRollScore: 3, opponentScore: 1, isCrawford: false);
        var game = GameState.FromPosition(match, AsymmetricBoard(), cubeSize: 2, cubeOwner: CubeOwner.OnRoll);

        var back = game.OpponentView().OpponentView();

        for (int i = 0; i < 26; i++)
            Assert.Equal(game.Board.Points[i], back.Board.Points[i]);
        Assert.Equal(game.Match.OnRollScore, back.Match.OnRollScore);
        Assert.Equal(game.Match.OpponentScore, back.Match.OpponentScore);
        Assert.Equal(game.CubeSize, back.CubeSize);
        Assert.Equal(game.CubeOwner, back.CubeOwner);
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
