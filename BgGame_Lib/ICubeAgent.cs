namespace BgGame_Lib;

/// <summary>
/// Decision-maker for cube turns. Two methods, one per decision moment.
///
/// At the start of an on-roll player's turn, before rolling, the offer side
/// runs (<see cref="ChooseOfferAsync"/>). If the offer is <see cref="CubeAction.Double"/>,
/// the response side runs on the opponent's agent (<see cref="ChooseResponseAsync"/>).
///
/// Two methods (rather than one with a context parameter) narrow the legal
/// return-set per call:
///   • <see cref="ChooseOfferAsync"/> returns <see cref="CubeAction.NoDouble"/> or <see cref="CubeAction.Double"/>.
///   • <see cref="ChooseResponseAsync"/> returns <see cref="CubeAction.Take"/> or <see cref="CubeAction.Pass"/>.
///
/// Phase 1 quiz mode does not exercise cube agents — the Referee for quiz
/// problems is a passive scorer, not a driver. <see cref="ICubeAgent"/> ships
/// in BgGame_Lib so Phase 2+ modes (user-vs-user, user-vs-bot, bot-vs-bot)
/// plug in implementations without retrofitting.
/// </summary>
public interface ICubeAgent
{
    /// <summary>
    /// Decide whether to offer a double at the start of the on-roll player's turn.
    /// Legal return values: <see cref="CubeAction.NoDouble"/>, <see cref="CubeAction.Double"/>.
    /// </summary>
    ValueTask<CubeAction> ChooseOfferAsync(
        GameState state,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decide how to respond to a double offer.
    /// Legal return values: <see cref="CubeAction.Take"/>, <see cref="CubeAction.Pass"/>.
    /// </summary>
    ValueTask<CubeAction> ChooseResponseAsync(
        GameState state,
        CancellationToken cancellationToken = default);
}
