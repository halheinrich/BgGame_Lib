namespace BgGame_Lib;

/// <summary>
/// One selectable category of decisions for a stats-weighted quiz mix: a
/// <see cref="QuizCategoryKind"/> plus the kind's parameter, if any. Immutable
/// with value equality; instances are created only through the validating
/// factories, so a kind/parameter combination that violates a bound is
/// unrepresentable from outside.
///
/// <para>
/// This is the serializable data half of the category (the shape a
/// <see cref="QuizMix"/> persists); the matching behavior lives in internal
/// predicate types materialized by <see cref="BuildPredicate"/> — a single
/// switch that is the one home of the data-to-behavior mapping, the same
/// config-to-filters split as <c>XgFilter_Lib</c>'s <c>FilterConfig.Build</c>.
/// </para>
///
/// <para>
/// Predicates classify the <b>(decision, stats)</b> pair — the live
/// <c>BgDecisionData</c> alongside its <see cref="DecisionStats"/> record
/// (null when never quizzed) — because sightings are derived per decision
/// kind: a cube position folds two halves per sighting, so its sightings are
/// <c>Tally.Submitted / 2</c> (always exact — one id only ever folds one
/// kind). The per-half measures (<see cref="QuizCategoryKind.AvgEquityLossOver"/>,
/// <see cref="QuizCategoryKind.WrongRateOver"/>) need no such adjustment and
/// compare directly across decision kinds.
/// </para>
///
/// <para>
/// <see cref="QuizCategoryKind.EverythingElse"/> is the residual category:
/// it matches the decisions matched by no other entry selected in the same
/// mix, so it is contextual rather than a standalone predicate —
/// <see cref="BuildPredicate"/> throws for it, and composers gate on
/// <see cref="IsResidual"/> instead.
/// </para>
/// </summary>
public sealed record QuizCategory
{
    private QuizCategory(QuizCategoryKind kind, double? value)
    {
        Kind = kind;
        Value = value;
    }

    /// <summary>Which predicate this category applies.</summary>
    public QuizCategoryKind Kind { get; }

    /// <summary>
    /// The kind's parameter: always <see langword="null"/> for the
    /// parameterless kinds (<see cref="QuizCategoryKind.NeverSeen"/>,
    /// <see cref="QuizCategoryKind.GotWrong"/>,
    /// <see cref="QuizCategoryKind.EverythingElse"/>), always a validated
    /// value for the parameterized ones. Integer-parameter kinds
    /// (<see cref="QuizCategoryKind.SeenFewerThan"/>,
    /// <see cref="QuizCategoryKind.NotSeenInDays"/>) store their count
    /// exactly (every <see langword="int"/> is exact in a
    /// <see langword="double"/>).
    /// </summary>
    public double? Value { get; }

    /// <summary>
    /// Whether this is the residual <see cref="QuizCategoryKind.EverythingElse"/>
    /// category — matched by no other selected entry rather than by a
    /// standalone predicate. Callers must gate on this before
    /// <see cref="BuildPredicate"/>.
    /// </summary>
    public bool IsResidual => Kind == QuizCategoryKind.EverythingElse;

    /// <summary>Decisions never quizzed (no lifetime record).</summary>
    public static QuizCategory NeverSeen { get; } = new(QuizCategoryKind.NeverSeen, null);

    /// <summary>Decisions with at least one wrong submission (per-half for cubes).</summary>
    public static QuizCategory GotWrong { get; } = new(QuizCategoryKind.GotWrong, null);

    /// <summary>
    /// The residual category: decisions matched by no other entry selected in
    /// the same mix. See <see cref="IsResidual"/>.
    /// </summary>
    public static QuizCategory EverythingElse { get; } = new(QuizCategoryKind.EverythingElse, null);

    /// <summary>
    /// Decisions sighted fewer than <paramref name="times"/> times. A never-seen
    /// decision has zero sightings and always matches; a cube position's
    /// sightings are <c>Tally.Submitted / 2</c> (the two-half fold).
    /// </summary>
    /// <param name="times">Exclusive upper bound on sightings; at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="times"/> is less than 1.
    /// </exception>
    public static QuizCategory SeenFewerThan(int times)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(times, 1);
        return new(QuizCategoryKind.SeenFewerThan, times);
    }

    /// <summary>
    /// Decisions not quizzed within the last <paramref name="days"/> days —
    /// the literal reading, so a never-quizzed decision matches too (overlap
    /// with <see cref="NeverSeen"/> is safe: mix composition presents each
    /// decision at most once).
    /// </summary>
    /// <param name="days">Days of staleness required to match; at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="days"/> is less than 1.
    /// </exception>
    public static QuizCategory NotSeenInDays(int days)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(days, 1);
        return new(QuizCategoryKind.NotSeenInDays, days);
    }

    /// <summary>
    /// Decisions whose lifetime average equity loss per submission strictly
    /// exceeds <paramref name="loss"/>. Per-half for cubes with no adjustment
    /// (numerator and denominator both count per-half), so one threshold
    /// compares across decision kinds. Never-seen decisions do not match.
    /// </summary>
    /// <param name="loss">Exclusive lower bound on average equity loss; finite and non-negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="loss"/> is negative, NaN, or infinite.
    /// </exception>
    public static QuizCategory AvgEquityLossOver(double loss)
    {
        if (!double.IsFinite(loss) || loss < 0.0)
            throw new ArgumentOutOfRangeException(
                nameof(loss), loss, "Average-equity-loss threshold must be finite and non-negative.");
        return new(QuizCategoryKind.AvgEquityLossOver, loss);
    }

    /// <summary>
    /// Decisions whose lifetime wrong fraction (1 − <see cref="ScoreSegment.Accuracy"/>,
    /// in [0, 1]) strictly exceeds <paramref name="rate"/>. Per-half for cubes: a
    /// half-right cube reads a wrong rate of 0.5. Never-seen decisions do not
    /// match. The threshold is a fraction, matching the library convention that
    /// percentage rendering is a display concern.
    /// </summary>
    /// <param name="rate">Exclusive lower bound on the wrong fraction; in [0.0, 1.0).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="rate"/> is NaN or outside [0.0, 1.0) — a
    /// threshold of 1.0 or more could never be exceeded.
    /// </exception>
    public static QuizCategory WrongRateOver(double rate)
    {
        if (!double.IsFinite(rate) || rate < 0.0 || rate >= 1.0)
            throw new ArgumentOutOfRangeException(
                nameof(rate), rate, "Wrong-rate threshold must be a fraction in [0.0, 1.0).");
        return new(QuizCategoryKind.WrongRateOver, rate);
    }

    /// <summary>
    /// Recreate a category from its raw kind/value pair — the deserialization
    /// seam used by <see cref="QuizMixJsonConverter"/>. Routes through the
    /// public factories, so every bound has one definition; this switch is
    /// the one home of which kinds are parameterized.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown (as <see cref="ArgumentException"/> or a subclass) when a
    /// parameterless kind carries a value, a parameterized kind lacks one, an
    /// integer-parameter kind gets a non-integral or out-of-range value, or a
    /// value violates its factory's bounds.
    /// </exception>
    internal static QuizCategory Create(QuizCategoryKind kind, double? value) => kind switch
    {
        QuizCategoryKind.NeverSeen => RequireNoValue(NeverSeen, value),
        QuizCategoryKind.GotWrong => RequireNoValue(GotWrong, value),
        QuizCategoryKind.EverythingElse => RequireNoValue(EverythingElse, value),
        QuizCategoryKind.SeenFewerThan => SeenFewerThan(ToExactInt(value, kind)),
        QuizCategoryKind.NotSeenInDays => NotSeenInDays(ToExactInt(value, kind)),
        QuizCategoryKind.AvgEquityLossOver => AvgEquityLossOver(RequireValue(value, kind)),
        QuizCategoryKind.WrongRateOver => WrongRateOver(RequireValue(value, kind)),
        _ => throw new ArgumentException($"Unknown QuizCategoryKind '{kind}'.", nameof(kind)),
    };

    private static QuizCategory RequireNoValue(QuizCategory singleton, double? value) =>
        value is null
            ? singleton
            : throw new ArgumentException(
                $"Category kind '{singleton.Kind}' takes no parameter value (got {value}).",
                nameof(value));

    private static double RequireValue(double? value, QuizCategoryKind kind) =>
        value ?? throw new ArgumentException(
            $"Category kind '{kind}' requires a parameter value.", nameof(value));

    private static int ToExactInt(double? value, QuizCategoryKind kind)
    {
        var raw = RequireValue(value, kind);
        if (!double.IsFinite(raw) || raw != Math.Floor(raw) || raw < int.MinValue || raw > int.MaxValue)
            throw new ArgumentException(
                $"Category kind '{kind}' requires an integral parameter (got {raw}).",
                nameof(value));
        return (int)raw;
    }

    /// <summary>
    /// Materialize this category's matching behavior. The switch below is the
    /// one home of the kind-to-predicate mapping; adding a category adds one
    /// enum member, one factory, and one arm in each of the two kind switches
    /// (this one and <see cref="Create"/>).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown for <see cref="QuizCategoryKind.EverythingElse"/> — the residual
    /// is defined by the other selected entries, not by a standalone
    /// predicate; callers gate on <see cref="IsResidual"/> first.
    /// </exception>
    internal IQuizCategoryPredicate BuildPredicate() => Kind switch
    {
        QuizCategoryKind.NeverSeen => NeverSeenPredicate.Instance,
        QuizCategoryKind.GotWrong => GotWrongPredicate.Instance,
        QuizCategoryKind.SeenFewerThan => new SeenFewerThanPredicate((int)Value!.Value),
        QuizCategoryKind.NotSeenInDays => new NotSeenInDaysPredicate((int)Value!.Value),
        QuizCategoryKind.AvgEquityLossOver => new AvgEquityLossOverPredicate(Value!.Value),
        QuizCategoryKind.WrongRateOver => new WrongRateOverPredicate(Value!.Value),
        QuizCategoryKind.EverythingElse => throw new InvalidOperationException(
            "EverythingElse is the residual category — it matches whatever no other selected " +
            "entry matched and has no standalone predicate. Gate on IsResidual before calling."),
        _ => throw new InvalidOperationException($"Unknown QuizCategoryKind '{Kind}'."),
    };
}
