namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// One entry in a <see cref="Transcript"/> — an immutable record of a single
/// decision moment plus the state at which it occurred.
///
/// <para>Three concrete subtypes:
///   • <see cref="PlayTranscriptEntry"/> — a play decision with dice and chosen Play.
///   • <see cref="CubeTranscriptEntry"/> — a cube offer or response.
///   • <see cref="GameEndedTranscriptEntry"/> — the terminating event of a game.</para>
///
/// <para>Pattern-match consumers use <c>is X</c> to reach the payload — the standard
/// C# discriminated-union pattern. A single entry record with nullable fields
/// would be worse because legal-payload-by-discriminator would have to be
/// enforced at runtime.</para>
///
/// <para><b>Seat identity.</b> <paramref name="OnRollSeat"/> is the seat whose
/// perspective <paramref name="State"/> is expressed in — the one fact that
/// makes every perspective-relative snapshot field (board positives,
/// <see cref="MatchSnapshot.OnRollScore"/>, <see cref="GameSnapshot.CubeOwner"/>)
/// absolutely interpretable. Snapshots themselves stay on-roll-frame; nothing
/// about the frame conventions changes. Attribution is derived from it by the
/// subtypes that own the rule: a play's mover is <paramref name="OnRollSeat"/>
/// itself, a cube entry's actor is <see cref="CubeTranscriptEntry.ActingSeat"/>,
/// a finished game's winner is <see cref="GameEndedTranscriptEntry.Winner"/>.</para>
/// </summary>
/// <param name="State">Immutable snapshot of the state the entry records, in <paramref name="OnRollSeat"/>'s frame.</param>
/// <param name="OnRollSeat">The seat whose perspective <paramref name="State"/> is expressed in.</param>
public abstract record TranscriptEntry(GameSnapshot State, MatchSeat OnRollSeat);

/// <summary>
/// A play decision: the dice rolled and the play chosen.
/// <see cref="TranscriptEntry.OnRollSeat"/> is the mover — the snapshot is
/// taken in the decision frame, before the apply flips perspective.
/// </summary>
/// <param name="State">The state the play decision was made in (mover's frame).</param>
/// <param name="OnRollSeat">The seat that rolled and played — the mover.</param>
/// <param name="Die1">First die of the roll.</param>
/// <param name="Die2">Second die of the roll.</param>
/// <param name="ChosenPlay">The play applied; empty for a dance turn.</param>
public sealed record PlayTranscriptEntry(
    GameSnapshot State,
    MatchSeat OnRollSeat,
    int Die1,
    int Die2,
    Play ChosenPlay) : TranscriptEntry(State, OnRollSeat);

/// <summary>
/// A cube decision: an offer side action or a response side action.
///
/// <para>Both the offer entry and its response entry are snapshotted against
/// the live state, which stays in the <em>offerer's</em> frame across the
/// responder query — so <see cref="TranscriptEntry.OnRollSeat"/> is the
/// offerer for both and does not flip between them. The decision-maker is
/// <see cref="ActingSeat"/>.</para>
/// </summary>
/// <param name="State">The live state at the decision (offerer's frame for offers and responses alike).</param>
/// <param name="OnRollSeat">The seat whose perspective <paramref name="State"/> is in — the offerer.</param>
/// <param name="Action">The cube action taken.</param>
public sealed record CubeTranscriptEntry(
    GameSnapshot State,
    MatchSeat OnRollSeat,
    CubeAction Action) : TranscriptEntry(State, OnRollSeat)
{
    /// <summary>
    /// The seat that made this cube decision. Offer-side actions
    /// (<see cref="CubeAction.NoDouble"/> / <see cref="CubeAction.Double"/>)
    /// belong to the on-roll offerer; response-side actions
    /// (<see cref="CubeAction.Take"/> / <see cref="CubeAction.Pass"/>) belong
    /// to the other seat — the single home of the response-attribution rule.
    /// </summary>
    public MatchSeat ActingSeat =>
        Action is CubeAction.Take or CubeAction.Pass ? OnRollSeat.Other() : OnRollSeat;
}

/// <summary>
/// The terminating event of a game — bear-off completion or cube pass.
/// <see cref="TranscriptEntry.OnRollSeat"/> is wherever the final turn
/// transition left the live frame (the last apply's flip for a borne-off
/// game; the offerer's frame for a passed double).
/// </summary>
/// <param name="State">The terminal position, in <paramref name="OnRollSeat"/>'s frame.</param>
/// <param name="OnRollSeat">The seat whose perspective <paramref name="State"/> is expressed in.</param>
/// <param name="Result">Win kind, perspective-relative winner flag, and cube value.</param>
public sealed record GameEndedTranscriptEntry(
    GameSnapshot State,
    MatchSeat OnRollSeat,
    GameResult Result) : TranscriptEntry(State, OnRollSeat)
{
    /// <summary>
    /// The seat that won the game — <see cref="GameResult.OnRollWon"/>
    /// resolved against <see cref="TranscriptEntry.OnRollSeat"/>. The single
    /// home of win attribution; <see cref="GameRecord.Winner"/> is computed
    /// from it.
    /// </summary>
    public MatchSeat Winner => Result.OnRollWon ? OnRollSeat : OnRollSeat.Other();
}
