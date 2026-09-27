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
/// <param name="Games">
/// The completed games, in order — copied on construction into an immutable
/// list, so neither the caller's list nor a cast can change it afterwards.
/// </param>
public sealed record MatchResult(
    MatchSeat? Winner,
    int SeatOneScore,
    int SeatTwoScore,
    IReadOnlyList<GameRecord> Games)
{
    private readonly IReadOnlyList<GameRecord> _games = ImmutableCopy.Of(Games, nameof(Games));

    /// <summary>The completed games, in order; an immutable copy.</summary>
    /// <exception cref="ArgumentNullException">Thrown on init when the value is <see langword="null"/>.</exception>
    public IReadOnlyList<GameRecord> Games
    {
        get => _games;
        init => _games = ImmutableCopy.Of(value, nameof(Games));
    }
}
