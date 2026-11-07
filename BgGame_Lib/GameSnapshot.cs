namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// Immutable point-in-time capture of a <see cref="GameState"/>: the input of
/// every agent query (<see cref="IPlayAgent"/>, <see cref="ICubeAgent"/>) and
/// the state each <see cref="Transcript"/> entry records. The runner hands an
/// agent the same value it records for that decision.
///
/// <see cref="Board"/> is a <see cref="BoardPosition"/>: an immutable value, so
/// subsequent mutations of the live <see cref="GameState"/> do not affect
/// snapshots already taken, and two snapshots of the same position compare
/// equal by value (<see cref="BoardPosition"/> is the one definition of "the
/// same position"). A reader that needs a working board builds its own,
/// <c>new BoardState(snapshot.Board)</c>.
///
/// All fields are in the on-roll player's frame, whoever reads them; nothing
/// re-expresses a snapshot for the other side.
/// <see cref="Board"/> is in <see cref="BoardState"/>'s frame (the "Mop" layout):
/// 26 counts, [0] = opponent bar, [1..24] = points, [25] = on-roll bar;
/// positive = on-roll's checkers.
/// </summary>
/// <param name="Board">The position, in the on-roll player's frame, as a value.</param>
/// <param name="CubeSize">Cube size (1, 2, 4, ...).</param>
/// <param name="CubeOwner">Cube ownership, relative to the player on roll.</param>
/// <param name="Match">Snapshot of the match-level state at the same instant.</param>
public sealed record GameSnapshot(
    BoardPosition Board,
    int CubeSize,
    CubeOwner CubeOwner,
    MatchSnapshot Match);
