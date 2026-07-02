namespace BgGame_Lib;

/// <summary>
/// One entrant in a match: the pair of decision agents that together answer
/// every query the match loop can pose (plays, cube offers, cube responses).
///
/// A participant is one entity — a bot typically implements both interfaces
/// on a single object (see <see cref="From{TAgent}"/>); a network server
/// adapts one remote engine into one participant. The same participant
/// instance may occupy both seats of a match (mirror match / self-play):
/// results are keyed by <see cref="MatchSeat"/>, never by participant
/// identity.
/// </summary>
public sealed record MatchParticipant
{
    /// <summary>Decision-maker for play turns.</summary>
    public IPlayAgent PlayAgent { get; }

    /// <summary>Decision-maker for cube offers and responses.</summary>
    public ICubeAgent CubeAgent { get; }

    /// <summary>Bundle a play agent and a cube agent into one participant.</summary>
    /// <exception cref="ArgumentNullException">Either agent is null.</exception>
    public MatchParticipant(IPlayAgent playAgent, ICubeAgent cubeAgent)
    {
        ArgumentNullException.ThrowIfNull(playAgent);
        ArgumentNullException.ThrowIfNull(cubeAgent);
        PlayAgent = playAgent;
        CubeAgent = cubeAgent;
    }

    /// <summary>
    /// Bundle a single object implementing both agent interfaces — the typical
    /// bot shape.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="agent"/> is null.</exception>
    public static MatchParticipant From<TAgent>(TAgent agent)
        where TAgent : IPlayAgent, ICubeAgent
    {
        ArgumentNullException.ThrowIfNull(agent);
        return new MatchParticipant(agent, agent);
    }
}
