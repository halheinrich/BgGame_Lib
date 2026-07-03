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
/// <see cref="ApplyPlay(Play, int, int)"/>. To <em>query</em> the position from
/// the other player's frame without advancing anything, take a detached
/// <see cref="OpponentView"/>; there is no in-place flip surface.
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
    /// True iff the on-roll player may legally offer a double right now:
    /// the match has a cube at all (<see cref="MatchState.HasCube"/> — false in
    /// a 1-point match), the current game is not the Crawford game, and the
    /// cube is centered or owned by the on-roll player.
    ///
    /// This is the queryable form of the <see cref="DoubleCube"/> guard —
    /// drivers gate the pre-roll cube window on it instead of catching the
    /// exception. Turn-sequencing legality (e.g., no double before a game's
    /// opening roll) is a driver concern, not encoded here.
    /// </summary>
    public bool CanDouble => DoubleRefusalReason is null;

    /// <summary>
    /// Single source for the cube-legality rule and its diagnostic messages:
    /// null when doubling is legal, otherwise the reason it is not.
    /// <see cref="CanDouble"/> and <see cref="DoubleCube"/> both read it.
    /// </summary>
    private string? DoubleRefusalReason =>
        !Match.HasCube ? "a 1-point match has no doubling cube"
        : Match.IsCrawford ? "doubling is prohibited during the Crawford game"
        : CubeOwner == CubeOwner.Opponent ? "the cube is currently owned by the opponent"
        : null;

    /// <summary>
    /// Double the cube and transfer ownership to the responder (the current
    /// opponent). Called by <see cref="Referee"/> on a Take response.
    ///
    /// Legality is substrate-enforced (see <see cref="CanDouble"/>): callers
    /// do not need match context to avoid an illegal double — the state
    /// itself refuses.
    /// </summary>
    /// <exception cref="InvalidOperationException">Doubling is illegal here: the match has no cube (1-point match), the current game is the Crawford game, or the cube is owned by the opponent.</exception>
    public void DoubleCube()
    {
        if (DoubleRefusalReason is string reason)
            throw new InvalidOperationException($"Cannot double: {reason}.");

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
        CubeOwner = MirrorOwner(CubeOwner);
    }

    /// <summary>
    /// Return a new <see cref="GameState"/> re-expressing this position from the
    /// current opponent's frame: the board is a <see cref="BoardState.FlippedCopy"/>,
    /// the score labels swap (its <see cref="MatchState.OnRollScore"/> is this
    /// state's <see cref="MatchState.OpponentScore"/>), the cube owner mirrors
    /// (<see cref="BgDataTypes_Lib.CubeOwner.OnRoll"/> ↔
    /// <see cref="BgDataTypes_Lib.CubeOwner.Opponent"/>, centered stays), and
    /// <see cref="CubeSize"/>, <see cref="MatchState.MatchLength"/>, and
    /// <see cref="MatchState.IsCrawford"/> carry over unchanged. The transform is
    /// an involution: <c>OpponentView().OpponentView()</c> reproduces the position.
    ///
    /// <para>
    /// This is a detached <em>query view</em>, not a fork of the live game: it
    /// owns a fresh board and a fresh <see cref="MatchState"/>, so nothing done
    /// to it affects this state (and vice versa), and it does not track
    /// subsequent live mutations. Use it to let the non-on-roll player evaluate
    /// a position in its own frame — the state handed to
    /// <see cref="ICubeAgent.ChooseResponseAsync"/> — without advancing anything.
    /// To advance past a turn boundary, use <see cref="ApplyPlay(Play, int, int)"/>
    /// on the live state; do not play on from a view.
    /// </para>
    /// </summary>
    public GameState OpponentView() =>
        new(
            Board.FlippedCopy(),
            MatchState.FromScores(
                Match.MatchLength, Match.OpponentScore, Match.OnRollScore, Match.IsCrawford),
            CubeSize,
            MirrorOwner(CubeOwner));

    /// <summary>
    /// The cube-owner half of a perspective change, single-sourced for the
    /// turn-boundary flip (<see cref="ApplyPlay(Play, int, int)"/>) and the
    /// detached query view (<see cref="OpponentView"/>).
    /// </summary>
    private static CubeOwner MirrorOwner(CubeOwner owner) => owner switch
    {
        CubeOwner.OnRoll => CubeOwner.Opponent,
        CubeOwner.Opponent => CubeOwner.OnRoll,
        CubeOwner.Centered => CubeOwner.Centered,
        _ => throw new InvalidOperationException($"Unhandled CubeOwner value: {owner}"),
    };

    /// <summary>Take an immutable snapshot of the full game-and-match state.</summary>
    public GameSnapshot Snapshot()
    {
        var boardCopy = new int[26];
        Array.Copy(Board.Points, boardCopy, 26);
        return new GameSnapshot(boardCopy, CubeSize, CubeOwner, Match.Snapshot());
    }

    private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;
}
