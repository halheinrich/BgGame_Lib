namespace BgGame_Lib;

/// <summary>
/// The frame-free context of a game about to begin, delivered by
/// <see cref="IMatchObserver.OnGameStarted"/> before the opening roll.
///
/// <para>
/// A game's opening frame is indeterminate until the opening roll names the
/// first mover, so no board snapshot can accompany this event. The facts here
/// are the ones that <em>are</em> settled at game construction and do not
/// depend on that frame: the two seats' entering scores and whether this is the
/// Crawford game. Scores are <b>seat-absolute</b> —
/// <see cref="SeatOneScore"/> / <see cref="SeatTwoScore"/> name the fixed seats,
/// not the on-roll / opponent labels of the frame-relative
/// <see cref="MatchState"/> — so a consumer never has to know or re-derive which
/// seat is about to move to fold them into a running match view.
/// </para>
/// </summary>
/// <param name="GameNumber">1-based game number within the match.</param>
/// <param name="SeatOneScore">Points seat One has entering this game (before any of its awards).</param>
/// <param name="SeatTwoScore">Points seat Two has entering this game (before any of its awards).</param>
/// <param name="IsCrawford">
/// True iff this is the Crawford game — the one game after a side reaches
/// <see cref="MatchState.MatchLength"/> − 1, during which the cube is suspended.
/// The same value the substrate computes for game construction (see
/// <see cref="MatchState.IsCrawford"/>); always false in money games and
/// 1-point matches.
/// </param>
public sealed record GameStartContext(
    int GameNumber,
    int SeatOneScore,
    int SeatTwoScore,
    bool IsCrawford);
