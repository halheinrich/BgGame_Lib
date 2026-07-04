namespace BgGame_Lib;

/// <summary>
/// Source of dice rolls for a match driver. The library itself contains no
/// randomness — all dice enter through this seam, and rolling is strictly a
/// driver/server-side concern: agents (<see cref="IPlayAgent"/> /
/// <see cref="ICubeAgent"/>) never roll and never see the source.
///
/// Implementations:
///   • <see cref="SeededDiceSource"/> — seeded pseudo-random rolls, reproducible per seed.
///   • <see cref="RecordedDiceSource"/> — replays a fixed pre-recorded sequence
///     (deterministic tests; duplicate-dice tournament pairings in a later arc).
///   • <see cref="VerifiableDiceSource"/> — audit-grade rolls derived from a
///     secret <see cref="DiceKey"/> via a public algorithm, for the
///     commit-and-reveal fairness scheme.
/// </summary>
public interface IDiceSource
{
    /// <summary>
    /// Produce the next pair of dice, each in 1–6.
    ///
    /// The pair is unordered as far as game rules are concerned; tuple order is
    /// meaningful only where a consumer assigns it a convention (the match
    /// runner's opening roll treats <c>Die1</c> as seat One's die and
    /// <c>Die2</c> as seat Two's die).
    /// </summary>
    (int Die1, int Die2) Roll();
}
