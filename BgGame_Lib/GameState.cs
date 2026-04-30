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
/// On-roll-relative perspective: <see cref="Board"/> follows BgDataTypes_Lib's
/// "Mop" convention (positive = on-roll's checkers); <see cref="CubeOwner"/> values
/// <see cref="BgDataTypes_Lib.CubeOwner.OnRoll"/> / <see cref="BgDataTypes_Lib.CubeOwner.Opponent"/>
/// likewise label the current on-roll perspective. Board, match scores, and
/// cube ownership all flip together at a turn boundary — see
/// <see cref="ApplyPlay(Play, int, int)"/>.
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
    /// Apply a chosen play and transition the turn boundary: the board is
    /// validated-and-applied (with perspective flip) via
    /// <see cref="MoveGenerator.ApplyPlay(BoardState, Play, int, int)"/>, then
    /// match score labels and cube-owner label flip to the new on-roll player.
    /// After this call, <see cref="Board"/>, <see cref="Match"/>, and
    /// <see cref="CubeOwner"/> are all expressed from the next mover's POV.
    ///
    /// <para>
    /// This is the unified turn-transition primitive. Callers reasoning in
    /// on-roll POV never need to flip explicitly. Bypassing this method (e.g.,
    /// invoking <see cref="BoardState.ApplyPlay"/> on <see cref="Board"/>
    /// directly) leaves the match scores and cube ownership unflipped — a
    /// half-flipped, internally inconsistent state.
    /// </para>
    ///
    /// <para>
    /// Throw-before-mutate: if <paramref name="play"/> is illegal for the
    /// current board and dice, <see cref="MoveGenerator.ApplyPlay(BoardState, Play, int, int)"/>
    /// throws and no field on this <see cref="GameState"/> is mutated.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentException">The play is not legal from the current state and dice.</exception>
    public void ApplyPlay(Play play, int die1, int die2)
    {
        MoveGenerator.ApplyPlay(Board, play, die1, die2);
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

    private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;
}
