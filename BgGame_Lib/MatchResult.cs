namespace BgGame_Lib;

/// <summary>
/// Outcome of a <see cref="MatchRunner.RunMatchAsync"/> run: final seat-keyed
/// scores plus the per-game records in play order.
/// </summary>
/// <param name="Winner">
/// The seat that reached the match length, or null when the run ended without
/// a match winner (a money session, or a <c>maxGames</c> cap reached first).
/// </param>
/// <param name="SeatOneScore">Total points scored by seat One.</param>
/// <param name="SeatTwoScore">Total points scored by seat Two.</param>
/// <param name="Games">The completed games, in order.</param>
public sealed record MatchResult(
    MatchSeat? Winner,
    int SeatOneScore,
    int SeatTwoScore,
    IReadOnlyList<GameRecord> Games);
