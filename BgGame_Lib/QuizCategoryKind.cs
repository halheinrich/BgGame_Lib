namespace BgGame_Lib;

/// <summary>
/// The discriminator of a <see cref="QuizCategory"/>: which lifetime-stats
/// predicate the category applies when classifying decisions for a
/// stats-weighted <see cref="QuizMix"/>.
///
/// <para>
/// Adding a category is additive: a new member here, a validating factory on
/// <see cref="QuizCategory"/>, and one arm in each of that type's two kind
/// switches (<c>BuildPredicate</c> and the deserialization seam
/// <c>Create</c>) — no existing surface changes.
/// </para>
/// </summary>
public enum QuizCategoryKind
{
    /// <summary>The decision has never been quizzed (no lifetime record).</summary>
    NeverSeen = 1,

    /// <summary>The decision has at least one wrong submission (per-half for cubes).</summary>
    GotWrong,

    /// <summary>
    /// The decision has been sighted fewer than the parameter's number of
    /// times. Sightings are derived from the lifetime tally: a checker play
    /// counts one per submission, a cube position two per sighting (the
    /// two-half fold), so its sightings are <c>Submitted / 2</c>.
    /// </summary>
    SeenFewerThan,

    /// <summary>
    /// The decision has not been quizzed within the parameter's number of
    /// days — including never quizzed at all.
    /// </summary>
    NotSeenInDays,

    /// <summary>
    /// The decision's lifetime average equity loss per submission exceeds the
    /// parameter. Per-half for cubes with no adjustment needed: both halves'
    /// losses and both half-counts enter the average, so it is already the
    /// per-half figure, directly comparable across decision kinds.
    /// </summary>
    AvgEquityLossOver,

    /// <summary>
    /// The decision's lifetime wrong fraction (1 − accuracy, in [0, 1])
    /// exceeds the parameter. Per-half for cubes: a half-right cube reads
    /// a wrong rate of 0.5, consistent with the 1-of-2 fold.
    /// </summary>
    WrongRateOver,

    /// <summary>
    /// The residual category: matches exactly the decisions matched by no
    /// other entry selected in the same mix. Contextual by definition — it is
    /// not a standalone predicate (see <see cref="QuizCategory.IsResidual"/>).
    /// </summary>
    EverythingElse,
}
