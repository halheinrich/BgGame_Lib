namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// Immutable point-in-time capture of a <see cref="GameState"/>. Used by
/// <see cref="Transcript"/> entries to record the state at each decision moment.
///
/// <see cref="Board"/> is a defensive copy — subsequent mutations of the live
/// <see cref="GameState"/> do not affect snapshots already taken.
///
/// All fields are on-roll-relative; the perspective at snapshot time is preserved.
/// Layout of <see cref="Board"/> matches BgMoveGen.BoardState.Points and
/// BgDataTypes_Lib's "Mop" convention: 26 elements, [0] = opponent bar,
/// [1..24] = points, [25] = on-roll bar; positive = on-roll's checkers.
/// </summary>
/// <param name="Board">26-element on-roll-relative point array (defensive copy).</param>
/// <param name="CubeSize">Cube size (1, 2, 4, ...).</param>
/// <param name="CubeOwner">Cube ownership in the current perspective.</param>
/// <param name="Match">Snapshot of the match-level state at the same instant.</param>
public sealed record GameSnapshot(
    IReadOnlyList<int> Board,
    int CubeSize,
    CubeOwner CubeOwner,
    MatchSnapshot Match);
