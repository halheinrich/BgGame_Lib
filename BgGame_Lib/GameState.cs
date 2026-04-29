namespace BgGame_Lib;

using BgDataTypes_Lib;
using BgMoveGen;

/// <summary>
/// Mutable game-level state: the live board plus cube state, aggregating the
/// surrounding <see cref="MatchState"/>.
///
/// Lifetime: one <see cref="GameState"/> per game within a match. When the game
/// ends, callers construct a fresh <see cref="GameState"/> against the same
/// <see cref="Match"/> for the next game.
///
/// On-roll-relative perspective: <see cref="Board"/> follows BgMoveGen's
/// convention (positive = on-roll's checkers); <see cref="CubeOwner"/> values
/// <see cref="BgDataTypes_Lib.CubeOwner.OnRoll"/> / <see cref="BgDataTypes_Lib.CubeOwner.Opponent"/>
/// likewise label the current on-roll perspective. Both flip together when the
/// turn passes — see <see cref="Referee.ApplyPlay"/>.
/// </summary>
public sealed class GameState
{
    /// <summary>The live mutable board. Mutated by BgMoveGen apply/undo and perspective flipping.</summary>
    public BoardState Board { get; }

    /// <summary>The match this game belongs to. Aggregated reference, not owned.</summary>
    public MatchState Match { get; }

    /// <summary>Cube size: 1, 2, 4, 8, ...</summary>
    public int CubeSize { get; private set; }

    /// <summary>Cube ownership in the current on-roll perspective.</summary>
    public CubeOwner CubeOwner { get; private set; }

    private GameState(BoardState board, MatchState match, int cubeSize, CubeOwner cubeOwner)
    {
        Board = board;
        Match = match;
        CubeSize = cubeSize;
        CubeOwner = cubeOwner;
    }

    /// <summary>
    /// Construct a fresh game: standard starting board, cube at 1, centered.
    /// </summary>
    public static GameState NewGame(MatchState match)
    {
        ArgumentNullException.ThrowIfNull(match);
        return new GameState(BoardState.Standard(), match, cubeSize: 1, cubeOwner: CubeOwner.Centered);
    }

    /// <summary>
    /// Construct from an arbitrary position — for fixtures, replay, mid-game resume.
    /// The <paramref name="board"/> is referenced (not copied); callers retain ownership.
    /// </summary>
    public static GameState FromPosition(
        MatchState match, BoardState board, int cubeSize, CubeOwner cubeOwner)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(board);
        if (cubeSize < 1)
            throw new ArgumentOutOfRangeException(nameof(cubeSize), cubeSize, "Cube size must be ≥ 1.");
        if (!IsPowerOfTwo(cubeSize))
            throw new ArgumentException("Cube size must be a power of two.", nameof(cubeSize));
        if (!Enum.IsDefined(cubeOwner))
            throw new ArgumentException("Unknown CubeOwner value.", nameof(cubeOwner));

        return new GameState(board, match, cubeSize, cubeOwner);
    }

    /// <summary>
    /// Double the cube and transfer ownership to the responder (the current
    /// opponent). Called by <see cref="Referee"/> on a Take response.
    /// </summary>
    /// <exception cref="InvalidOperationException">Cube is owned by the opponent (only the owner or a centered-cube holder may offer).</exception>
    public void DoubleCube()
    {
        if (CubeOwner == CubeOwner.Opponent)
            throw new InvalidOperationException(
                "Cannot double: the cube is currently owned by the opponent.");

        CubeSize *= 2;
        CubeOwner = CubeOwner.Opponent;
    }

    /// <summary>
    /// Flip board points, swap match scores, and flip cube ownership labels.
    /// Internal — invoked by <see cref="Referee.ApplyPlay"/> after move application
    /// to maintain the on-roll-relative invariant. Not part of the public surface.
    /// </summary>
    internal void SwapPerspective()
    {
        FlipBoardInPlace(Board);
        Match.SwapPerspective();
        CubeOwner = CubeOwner switch
        {
            CubeOwner.OnRoll => CubeOwner.Opponent,
            CubeOwner.Opponent => CubeOwner.OnRoll,
            CubeOwner.Centered => CubeOwner.Centered,
            _ => throw new InvalidOperationException($"Unhandled CubeOwner value: {CubeOwner}"),
        };
    }

    /// <summary>Take an immutable snapshot of the full game-and-match state.</summary>
    public GameSnapshot Snapshot()
    {
        var boardCopy = new int[26];
        Array.Copy(Board.Points, boardCopy, 26);
        return new GameSnapshot(boardCopy, CubeSize, CubeOwner, Match.Snapshot());
    }

    private static void FlipBoardInPlace(BoardState board)
    {
        // Negate every value, then reverse the 26-element array. The result has
        // [0] still meaning "opponent bar" (just relabeled — what was the on-roll
        // bar at index 25 is now at index 0, and its sign was flipped from positive
        // to negative, which is the new "opponent" perspective).
        for (int i = 0; i < 26; i++)
            board.Points[i] = -board.Points[i];

        for (int lo = 0, hi = 25; lo < hi; lo++, hi--)
            (board.Points[lo], board.Points[hi]) = (board.Points[hi], board.Points[lo]);

        board.RecalcHighPoint();
    }

    private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;
}
