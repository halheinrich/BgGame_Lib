namespace BgGame_Lib;

/// <summary>
/// Immutable point-in-time capture of a <see cref="MatchState"/>. Used by
/// <see cref="GameSnapshot"/> and through it by <see cref="Transcript"/>.
/// All fields are relative to the player on roll.
///
/// <para>The captured fields are the scores so far. In match play a score also
/// reads as what each side still needs to win, its away count
/// (<see cref="OnRollNeeds"/>, <see cref="OpponentNeeds"/>), derived from the
/// length and the scores and never stored: 0–0 in a 5-point match and 2–2 in a
/// 7-point match both read 5-away each. A money snapshot
/// (<see cref="MatchLength"/> 0) has no match to win, so it has no away count.</para>
/// </summary>
/// <param name="MatchLength">Match length in points; 0 = money game.</param>
/// <param name="OnRollScore">Points scored by the on-roll player.</param>
/// <param name="OpponentScore">Points scored by the opponent.</param>
/// <param name="IsCrawford">True iff the current game is the Crawford game.</param>
public sealed record MatchSnapshot(
    int MatchLength,
    int OnRollScore,
    int OpponentScore,
    bool IsCrawford)
{
    /// <summary>
    /// The points the on-roll player still needs to win the match,
    /// <see cref="MatchLength"/> − <see cref="OnRollScore"/>; null in a money
    /// game, which has no match to win.
    /// </summary>
    public int? OnRollNeeds => NeedsOf(OnRollScore);

    /// <summary>
    /// The points the opponent of the on-roll player still needs to win the
    /// match, <see cref="MatchLength"/> − <see cref="OpponentScore"/>; null in
    /// a money game, which has no match to win.
    /// </summary>
    public int? OpponentNeeds => NeedsOf(OpponentScore);

    private int? NeedsOf(int score) => MatchLength > 0 ? MatchLength - score : null;
}
