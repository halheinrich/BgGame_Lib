namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// A source of problems (decisions) for any quiz / replay loop. Iterable of
/// <see cref="BgDecisionData"/> with a re-iterable contract.
///
/// Phase 1 ships against a server-disk implementation living in
/// BgQuiz_Blazor. Future implementations include uploaded files, deployed
/// problem-set bundles, and curated libraries — all plug in via this interface
/// without changes to the consuming quiz controller.
///
/// Re-iterability: each call to <see cref="EnumerateAsync"/> yields a fresh
/// enumeration from the start. Implementations that genuinely cannot replay
/// (e.g., a one-shot in-memory upload that has been consumed) document the
/// limitation and throw on second call.
///
/// Position tracking ("problem 5 of 20") is intentionally not on this surface;
/// it belongs to the consumer (quiz controller). Sources are streams.
/// </summary>
public interface IProblemSetSource
{
    /// <summary>Human-readable label — filename, deployed-set name, "Uploaded", etc.</summary>
    string Name { get; }

    /// <summary>
    /// Total number of decisions, if known up front. Null when streaming or unknown.
    /// When non-null, must equal the number of items <see cref="EnumerateAsync"/> yields.
    /// </summary>
    int? Count { get; }

    /// <summary>
    /// Yield the decisions in order. Each invocation starts a fresh enumeration.
    /// </summary>
    IAsyncEnumerable<BgDecisionData> EnumerateAsync(
        CancellationToken cancellationToken = default);
}
