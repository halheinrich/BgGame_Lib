namespace BgGame_Lib;

/// <summary>
/// Per-entry composition telemetry: how one <see cref="QuizMixEntry"/> fared
/// in a <see cref="MixedProblemSetSource"/> enumeration. Part of
/// <see cref="MixComposition"/>; exists so the consumer can render an honest
/// notice when an entry could not fill its share.
/// </summary>
/// <param name="Category">The entry's category.</param>
/// <param name="Percent">The entry's configured share of the quiz, in 1–100.</param>
/// <param name="PoolSize">
/// How many decisions matched this entry's category — before the global
/// once-per-quiz dedupe, so overlapping entries each count a shared decision.
/// </param>
/// <param name="Requested">
/// The entry's initial largest-remainder share of
/// <see cref="MixComposition.TargetCount"/>.
/// </param>
/// <param name="Drawn">
/// How many decisions the entry actually supplied, including any
/// redistribution top-ups. <see cref="Drawn"/> below <see cref="Requested"/>
/// means the entry's pool ran dry (its share was redistributed);
/// above means it absorbed another entry's shortfall.
/// </param>
public sealed record MixCompositionEntry(
    QuizCategory Category,
    int Percent,
    int PoolSize,
    int Requested,
    int Drawn);
