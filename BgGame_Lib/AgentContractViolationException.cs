namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>Which agent-contract rule a participant broke.</summary>
public enum AgentContractViolationKind
{
    /// <summary><see cref="IPlayAgent.ChoosePlayAsync"/> returned a play that is not legal for the state and dice.</summary>
    IllegalPlay = 1,

    /// <summary><see cref="ICubeAgent.ChooseOfferAsync"/> returned something other than <see cref="CubeAction.NoDouble"/> or <see cref="CubeAction.Double"/>.</summary>
    IllegalCubeOffer = 2,

    /// <summary><see cref="ICubeAgent.ChooseResponseAsync"/> returned something other than <see cref="CubeAction.Take"/> or <see cref="CubeAction.Pass"/>.</summary>
    IllegalCubeResponse = 3,
}

/// <summary>
/// Thrown by <see cref="MatchRunner"/> when an agent returns a value outside
/// its contract: an illegal play, or a cube action from the wrong half of the
/// <see cref="CubeAction"/> enum for the decision being asked.
///
/// The runner is deliberately policy-free about consequences. Converting a
/// violation into a forfeit — and deciding what a forfeit awards or persists —
/// is tournament-layer logic: a tournament server catches this exception and
/// applies its own rules. <see cref="Seat"/> and <see cref="Kind"/> classify
/// the offender and the broken rule; <see cref="OffendingPlay"/> /
/// <see cref="OffendingCubeAction"/> carry the structured offending value for
/// any policy layer that wants to inspect it beyond the message text (which
/// already embeds the same detail — see the factory methods). Which of these a
/// server persists is its own choice: the current BgTournament server forfeits
/// on <see cref="Seat"/> and records the human-readable <see cref="Exception.Message"/>.
/// The substrate state is not mutated by the offending decision
/// (throw-before-mutate holds throughout), so the match is abandoned
/// mid-decision, not corrupted.
/// </summary>
public sealed class AgentContractViolationException : Exception
{
    /// <summary>The seat whose agent violated its contract.</summary>
    public MatchSeat Seat { get; }

    /// <summary>Which contract rule was broken.</summary>
    public AgentContractViolationKind Kind { get; }

    /// <summary>The play the agent returned; null unless <see cref="Kind"/> is <see cref="AgentContractViolationKind.IllegalPlay"/>.</summary>
    public Play? OffendingPlay { get; private init; }

    /// <summary>The cube action the agent returned; null unless <see cref="Kind"/> is a cube violation.</summary>
    public CubeAction? OffendingCubeAction { get; private init; }

    /// <summary>Construct with an explicit message (adapters wrapping remote engines may need free-form detail).</summary>
    public AgentContractViolationException(
        AgentContractViolationKind kind, MatchSeat seat, string message)
        : base(message)
    {
        Kind = kind;
        Seat = seat;
    }

    /// <summary>Construct with an explicit message and the underlying cause.</summary>
    public AgentContractViolationException(
        AgentContractViolationKind kind, MatchSeat seat, string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = kind;
        Seat = seat;
    }

    /// <summary>
    /// An <see cref="IPlayAgent"/> returned a play the substrate rejected.
    /// <paramref name="cause"/> is the validation exception from
    /// <see cref="GameState.ApplyPlay(Play, int, int)"/>.
    /// </summary>
    public static AgentContractViolationException ForPlay(
        MatchSeat seat, Play offendingPlay, int die1, int die2, Exception cause) =>
        new(AgentContractViolationKind.IllegalPlay, seat,
            $"Seat {seat}'s play agent returned an illegal play {FormatPlay(offendingPlay)} for dice ({die1}, {die2}).",
            cause)
        { OffendingPlay = offendingPlay };

    /// <summary>An <see cref="ICubeAgent"/> offer call returned a response-side (or undefined) value.</summary>
    public static AgentContractViolationException ForCubeOffer(
        MatchSeat seat, CubeAction offendingAction) =>
        new(AgentContractViolationKind.IllegalCubeOffer, seat,
            $"Seat {seat}'s cube agent returned {offendingAction} from ChooseOfferAsync; legal values are NoDouble and Double.")
        { OffendingCubeAction = offendingAction };

    /// <summary>An <see cref="ICubeAgent"/> response call returned an offer-side (or undefined) value.</summary>
    public static AgentContractViolationException ForCubeResponse(
        MatchSeat seat, CubeAction offendingAction) =>
        new(AgentContractViolationKind.IllegalCubeResponse, seat,
            $"Seat {seat}'s cube agent returned {offendingAction} from ChooseResponseAsync; legal values are Take and Pass.")
        { OffendingCubeAction = offendingAction };

    private static string FormatPlay(Play play)
    {
        if (play.Count == 0) return "(no moves)";
        var parts = new string[play.Count];
        for (int i = 0; i < play.Count; i++)
        {
            var move = play[i];
            parts[i] = move.ToPt < 0 ? $"{move.FrPt}/{-move.ToPt}*" : $"{move.FrPt}/{move.ToPt}";
        }
        return string.Join(" ", parts);
    }
}
