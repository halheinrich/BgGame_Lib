namespace BgGame_Lib;

/// <summary>
/// Composition telemetry for one <see cref="MixedProblemSetSource"/>
/// enumeration: what the mix asked for and what the pools could actually
/// supply, overall and per entry. Exposed as
/// <see cref="MixedProblemSetSource.LastComposition"/> — assigned before the
/// first decision is yielded, so a consumer can render an honest shortfall
/// notice ("wanted 10 never-seen, only 4 exist") as soon as the quiz starts.
///
/// <para>
/// <see cref="DrawnCount"/> below <see cref="TargetCount"/> means the
/// requested quiz length exceeded the deduped union of the selected
/// categories' pools — the quiz is every reachable decision, and no amount of
/// redistribution could fill the rest. Per-entry shortfalls (an entry's pool
/// running dry, its share redistributed to the others) are visible on
/// <see cref="MixCompositionEntry"/> even when the overall count was met.
/// </para>
/// </summary>
/// <param name="TargetCount">
/// The composition target: the mix's <see cref="QuizMix.QuizLength"/>, or the
/// deduped union of the selected pools when no length was set.
/// </param>
/// <param name="DrawnCount">
/// How many decisions were actually drawn — the lesser of
/// <see cref="TargetCount"/> and the deduped union of the selected pools.
/// </param>
/// <param name="Entries">Per-entry telemetry, in the mix's declared entry order.</param>
public sealed record MixComposition(
    int TargetCount,
    int DrawnCount,
    IReadOnlyList<MixCompositionEntry> Entries);
