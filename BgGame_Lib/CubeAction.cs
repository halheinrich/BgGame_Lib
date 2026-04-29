namespace BgGame_Lib;

/// <summary>
/// Cube-decision values produced by <see cref="ICubeAgent"/>.
///
/// The enum is intentionally a single flat set covering both decision moments.
/// Each method on <see cref="ICubeAgent"/> narrows the legal subset:
///   • <see cref="ICubeAgent.ChooseOfferAsync"/> returns <see cref="NoDouble"/> or <see cref="Double"/>.
///   • <see cref="ICubeAgent.ChooseResponseAsync"/> returns <see cref="Take"/> or <see cref="Pass"/>.
///
/// Tournament-rule values (Beaver, Raccoon, Resign) are deferred to a Phase 2+
/// extension. When they arrive they will join this enum and the corresponding
/// agent methods will widen their legal subsets.
/// </summary>
public enum CubeAction
{
    /// <summary>Offer side: do not double.</summary>
    NoDouble,

    /// <summary>Offer side: offer a double to the opponent.</summary>
    Double,

    /// <summary>Response side: accept the double; cube size doubles and ownership transfers.</summary>
    Take,

    /// <summary>Response side: decline the double; the offerer wins the pre-double cube value.</summary>
    Pass,
}
