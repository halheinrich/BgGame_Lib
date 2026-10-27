namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;

/// <summary>
/// Readings of XG's error for the by-error order's <c>recordedError</c>
/// (<see cref="ProblemSequence.BuildAsync"/>). Which half of a cube decision
/// a host reads is its filter layer's rule (SPEC-filtering.md §8); these are
/// the shapes the tests hand in.
/// </summary>
internal static class TestReadings
{
    /// <summary>
    /// What a host with no name facet passes: the doubler's error — for a
    /// checker play, the player's — under <paramref name="ranking"/>, as the
    /// filter view reads it; <see langword="null"/> where the view holds none.
    /// </summary>
    internal static Func<BgDecisionData, double?> DoublersError(PlayRanking ranking = PlayRanking.Equity) =>
        decision => decision.ViewFor(ranking).PlayerResult.TryGetError(out double error) ? error : null;

    /// <summary>
    /// The taker's error on a cube decision where a double was offered, under
    /// <see cref="PlayRanking.Equity"/> (a cube's halves do not depend on the
    /// ranking); <see langword="null"/> otherwise. A reading the doubler's half
    /// does not hold, standing in for a host whose name facet lists the taker.
    /// </summary>
    internal static double? TakersError(BgDecisionData decision) =>
        decision.ViewFor(PlayRanking.Equity).TakerResult is { } taker && taker.TryGetError(out double error)
            ? error
            : null;
}

/// <summary>
/// A reading that records every decision it is asked about, in order, and
/// answers with <paramref name="read"/>: how a test sees whether, how often
/// and for which decisions the order consulted the host.
/// </summary>
internal sealed class CountingReading(Func<BgDecisionData, double?> read)
{
    /// <summary>The decisions read, one entry per call.</summary>
    public List<DecisionId> Reads { get; } = [];

    /// <summary>The reading itself; pass this method group as <c>recordedError</c>.</summary>
    public double? Read(BgDecisionData decision)
    {
        Reads.Add(decision.Id);
        return read(decision);
    }
}
