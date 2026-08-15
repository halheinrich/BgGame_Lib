namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// A count of decisions by the kind of answer each one calls for: checker
/// plays, plus one bucket per cube best-pair from
/// <see cref="CubeDecisionPair"/>'s closed 2×2. Immutable;
/// <see cref="Add(DecisionData)"/> and <c>operator +</c> each return a new
/// <see cref="AnswerTypeDistribution"/>.
///
/// <para>
/// The question it answers is <b>collection-scoped, not session-scoped</b>:
/// "what is my saved corpus actually made of?" — a curation-bias check
/// (over-saved takes, no too-good positions at all). Nothing here records how
/// the user answered; <see cref="ScoreSegment"/> and <see cref="QuizScore"/>
/// own that axis.
/// </para>
///
/// <para>
/// <b>Fold contract:</b> every <see cref="Add(DecisionData)"/> increments
/// <b>exactly one</b> bucket, so <see cref="Total"/> equals the number of
/// decisions folded in. A consumer folding a filtered pool therefore reads
/// that pool's size straight off <see cref="Total"/> and needs no second
/// "how many matched" pass — one enumeration, one rule, nothing that can
/// drift.
/// </para>
///
/// <para>
/// <b>A cube decision buckets once</b>, keyed by its best pair: pool
/// membership counts rows, and one cube position is one row. This is
/// deliberately <i>not</i> the two-half convention of
/// <see cref="QuizScore.Plus(SubmittedCubeAction)"/> and
/// <see cref="ProblemStats.Plus(SubmittedCubeAction, DateTimeOffset)"/>,
/// where a cube position is two scored decisions (the doubler half and the
/// taker half). Those count answers given; this counts problems present.
/// </para>
///
/// <para>
/// The four cube buckets are named for the canonical
/// <see cref="CubeDecisionPair"/> instances they count, so reading a bucket
/// back to its pair needs no translation.
/// </para>
/// </summary>
/// <param name="CheckerPlays">Decisions whose answer is a checker play.</param>
/// <param name="NoDoubleTake">
/// Cube decisions whose best pair is <see cref="CubeDecisionPair.NoDoubleTake"/>.
/// </param>
/// <param name="TooGood">
/// Cube decisions whose best pair is <see cref="CubeDecisionPair.TooGood"/> —
/// (<see cref="CubeAction.NoDouble"/>, <see cref="CubeAction.Pass"/>), the
/// doubler is too good to double.
/// </param>
/// <param name="DoubleTake">
/// Cube decisions whose best pair is <see cref="CubeDecisionPair.DoubleTake"/>.
/// </param>
/// <param name="DoublePass">
/// Cube decisions whose best pair is <see cref="CubeDecisionPair.DoublePass"/>.
/// </param>
public sealed record AnswerTypeDistribution(
    int CheckerPlays,
    int NoDoubleTake,
    int TooGood,
    int DoubleTake,
    int DoublePass)
{
    /// <summary>The empty distribution: every bucket 0.</summary>
    public static AnswerTypeDistribution Empty { get; } = new(0, 0, 0, 0, 0);

    /// <summary>
    /// Total decisions counted — the sum of all five buckets. Derived, never
    /// stored, so it cannot drift from them. Since every
    /// <see cref="Add(DecisionData)"/> increments exactly one bucket, this is
    /// also the number of decisions folded in.
    /// </summary>
    public int Total => CheckerPlays + NoDoubleTake + TooGood + DoubleTake + DoublePass;

    /// <summary>
    /// Return a new distribution with <paramref name="decision"/> counted in
    /// its bucket. Equivalent to adding that decision's one-hot distribution
    /// via <c>operator +</c>; the operator is the single definition of how
    /// distributions combine.
    /// </summary>
    /// <param name="decision">
    /// The decision to classify. A checker play
    /// (<see cref="DecisionData.IsCube"/> false) counts as a checker play; a
    /// cube decision is keyed by the pair of its
    /// <see cref="DecisionData.BestDoublerAction"/> and
    /// <see cref="DecisionData.BestTakerAction"/> — the analysis's own verdict,
    /// never re-derived from equities here.
    /// </param>
    public AnswerTypeDistribution Add(DecisionData decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return this + Classify(decision);
    }

    /// <summary>Combine two distributions bucket-wise.</summary>
    public static AnswerTypeDistribution operator +(
        AnswerTypeDistribution a, AnswerTypeDistribution b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return new AnswerTypeDistribution(
            a.CheckerPlays + b.CheckerPlays,
            a.NoDoubleTake + b.NoDoubleTake,
            a.TooGood      + b.TooGood,
            a.DoubleTake   + b.DoubleTake,
            a.DoublePass   + b.DoublePass);
    }

    /// <summary>
    /// The one-hot distribution for a single decision — the bucketing rule, and
    /// the reason <see cref="Total"/> counts decisions: every branch returns a
    /// distribution whose buckets sum to 1.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if a cube decision's best pair is outside
    /// <see cref="CubeDecisionPair"/>'s closed 2×2 — unreachable, since that
    /// type guards both halves on construction, but a silent fifth bucket would
    /// break the fold contract rather than fail.
    /// </exception>
    private static AnswerTypeDistribution Classify(DecisionData decision)
    {
        if (!decision.IsCube)
            return Empty with { CheckerPlays = 1 };

        CubeDecisionPair best = new(decision.BestDoublerAction, decision.BestTakerAction);
        return best switch
        {
            _ when best == CubeDecisionPair.NoDoubleTake => Empty with { NoDoubleTake = 1 },
            _ when best.IsTooGood                        => Empty with { TooGood      = 1 },
            _ when best == CubeDecisionPair.DoubleTake   => Empty with { DoubleTake   = 1 },
            _ when best == CubeDecisionPair.DoublePass   => Empty with { DoublePass   = 1 },
            _ => throw new ArgumentOutOfRangeException(nameof(decision), best,
                "Cube best-pair is outside CubeDecisionPair's closed 2×2."),
        };
    }
}
