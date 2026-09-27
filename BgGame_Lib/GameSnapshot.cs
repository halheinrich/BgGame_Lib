namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// Immutable point-in-time capture of a <see cref="GameState"/>. Used by
/// <see cref="Transcript"/> entries to record the state at each decision moment.
///
/// <see cref="Board"/> is a <see cref="BoardPosition"/>: an immutable value, so
/// subsequent mutations of the live <see cref="GameState"/> do not affect
/// snapshots already taken, and two snapshots of the same position compare
/// equal by value (<see cref="BoardPosition"/> is the one definition of "the
/// same position").
///
/// All fields are on-roll-relative; the perspective at snapshot time is preserved.
/// <see cref="Board"/> is in <see cref="BoardState"/>'s frame (the "Mop" layout):
/// 26 counts, [0] = opponent bar, [1..24] = points, [25] = on-roll bar;
/// positive = on-roll's checkers.
/// </summary>
/// <param name="Board">The on-roll-relative position, as a value.</param>
/// <param name="CubeSize">Cube size (1, 2, 4, ...).</param>
/// <param name="CubeOwner">Cube ownership in the current perspective.</param>
/// <param name="Match">Snapshot of the match-level state at the same instant.</param>
public sealed record GameSnapshot(
    BoardPosition Board,
    int CubeSize,
    CubeOwner CubeOwner,
    MatchSnapshot Match);
