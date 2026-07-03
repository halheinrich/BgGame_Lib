namespace BgGame_Lib;

using BgDataTypes_Lib;

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
/// Perspective contract: the queried player always sees its own frame. Both
/// methods receive a <see cref="GameState"/> in which the deciding agent is
/// the on-roll-labeled player — positive board values,
/// <see cref="MatchState.OnRollScore"/>, and <see cref="CubeOwner.OnRoll"/>
/// are its own. One frame rule everywhere (wire transports included) means an
/// implementation never reasons about "which side am I this call". Drivers
/// querying the non-on-roll player construct the compliant state with
/// <see cref="GameState.OpponentView"/>.
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
    ///
    /// <para>
    /// <paramref name="state"/> is in the <em>responder's</em> frame: the
    /// responder sees itself as the on-roll-labeled player, and the pending
    /// offer appears as the pre-double cube with
    /// <see cref="CubeOwner.Centered"/> or <see cref="CubeOwner.Opponent"/>
    /// (the offerer can only double a cube it has access to). Drivers pass a
    /// detached <see cref="GameState.OpponentView"/> of the live (offerer-frame)
    /// state; the view is a query snapshot — the driver applies the returned
    /// response to the live state, never to the view.
    /// </para>
    /// </summary>
    ValueTask<CubeAction> ChooseResponseAsync(
        GameState state,
        CancellationToken cancellationToken = default);
}
