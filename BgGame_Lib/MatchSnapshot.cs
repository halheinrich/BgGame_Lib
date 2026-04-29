namespace BgGame_Lib;

/// <summary>
/// Immutable point-in-time capture of a <see cref="MatchState"/>. Used by
/// <see cref="GameSnapshot"/> and through it by <see cref="Transcript"/>.
/// All fields are on-roll-relative; perspective at snapshot time is preserved.
/// </summary>
/// <param name="MatchLength">Match length in points; 0 = money game.</param>
/// <param name="OnRollScore">Points scored by the on-roll player.</param>
/// <param name="OpponentScore">Points scored by the opponent.</param>
/// <param name="IsCrawford">True iff the current game is the Crawford game.</param>
public sealed record MatchSnapshot(
    int MatchLength,
    int OnRollScore,
    int OpponentScore,
    bool IsCrawford);
