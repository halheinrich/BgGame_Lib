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
///
/// <para>
/// <b>The capped/capless split is recorded, not inferred.</b>
/// <see cref="HasRequestedLength"/> says which of the two sources
/// <see cref="TargetCount"/> came from, because the counts alone cannot: a
/// capless composition's target <i>is</i> the union count, and a capped one
/// may legitimately request exactly that number, so the two are
/// indistinguishable from <see cref="TargetCount"/> and
/// <see cref="DrawnCount"/> in the case that matters. A consumer framing a
/// shortfall notice ("wanted 10 never-seen, only 4 exist" versus "here is
/// everything your categories reach") needs that fact, and it is the
/// producer's to state — this telemetry is the whole answer about one
/// enumeration (halheinrich/backgammon#12).
/// </para>
/// </summary>
/// <param name="TargetCount">
/// The composition target: the mix's <see cref="QuizMix.QuizLength"/>, or the
/// deduped union of the selected pools when no length was set — which of the
/// two is <see cref="HasRequestedLength"/>.
/// </param>
/// <param name="HasRequestedLength">
/// <see langword="true"/> when <see cref="TargetCount"/> is the length the
/// mix asked for (capped); <see langword="false"/> when it is the deduped
/// union of the selected pools, because the mix set no length (capless).
/// </param>
/// <param name="DrawnCount">
/// How many decisions were actually drawn — the lesser of
/// <see cref="TargetCount"/> and the deduped union of the selected pools.
/// Under <see cref="HasRequestedLength"/> <see langword="false"/> the two are
/// equal by construction.
/// </param>
/// <param name="Entries">Per-entry telemetry, in the mix's declared entry order.</param>
public sealed record MixComposition(
    int TargetCount,
    bool HasRequestedLength,
    int DrawnCount,
    IReadOnlyList<MixCompositionEntry> Entries);
