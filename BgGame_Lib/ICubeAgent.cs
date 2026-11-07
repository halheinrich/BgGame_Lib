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
/// Perspective contract: every query carries an immutable
/// <see cref="GameSnapshot"/> in the on-roll player's frame, and nothing is
/// re-expressed for the deciding side. The query tells the side, the data does
/// not: <see cref="ChooseOfferAsync"/> is asked of the on-roll player, whose
/// checkers are the positive counts, whose score is
/// <see cref="MatchSnapshot.OnRollScore"/> and whose cube is
/// <see cref="CubeOwner.OnRoll"/>; <see cref="ChooseResponseAsync"/> is asked
/// of the opponent of the player on roll, the negative side (see there).
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
    /// <param name="state">The decision state, in the on-roll player's frame (the agent's own).</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    ValueTask<CubeAction> ChooseOfferAsync(
        GameSnapshot state,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decide how to respond to a double offer.
    /// Legal return values: <see cref="CubeAction.Take"/>, <see cref="CubeAction.Pass"/>.
    ///
    /// <para>
    /// <paramref name="state"/> is the offer's own snapshot, unchanged: in the
    /// on-roll player's frame, the offerer's. The deciding agent is the
    /// opponent of the player on roll — its checkers are the negative counts,
    /// its score is <see cref="MatchSnapshot.OpponentScore"/> and what it still
    /// needs is <see cref="MatchSnapshot.OpponentNeeds"/>. The pending double
    /// shows as the pre-double cube, at its pre-double size, owned by
    /// <see cref="CubeOwner.Centered"/> (a first double) or
    /// <see cref="CubeOwner.OnRoll"/> (a redouble), never
    /// <see cref="CubeOwner.Opponent"/>: the offerer can only double a cube it
    /// has access to. If the agent takes, the cube becomes its own at twice
    /// that size.
    /// </para>
    /// </summary>
    /// <param name="state">The offer's decision state, in the on-roll (offerer's) frame; the agent is the negative side.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    ValueTask<CubeAction> ChooseResponseAsync(
        GameSnapshot state,
        CancellationToken cancellationToken = default);
}
