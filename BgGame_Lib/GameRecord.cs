namespace BgGame_Lib;

/// <summary>
/// One completed game within a match, as recorded by <see cref="MatchRunner"/>.
///
/// <see cref="GameResult.OnRollWon"/> inside <see cref="Result"/> is relative
/// to the perspective the game ended in; <see cref="Winner"/> is the absolute
/// seat attribution consumers should use.
/// </summary>
/// <param name="Winner">The seat that won the game.</param>
/// <param name="Result">Win kind and points (cube × win-type multiplier).</param>
/// <param name="Transcript">Full decision-by-decision record of the game.</param>
public sealed record GameRecord(MatchSeat Winner, GameResult Result, Transcript Transcript);
