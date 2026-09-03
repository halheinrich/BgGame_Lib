namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// A count of decisions by the kind of answer each one calls for: checker
/// plays, plus one bucket per cube verdict from SPEC-scoring §3's table —
/// the four reachable pairs of <see cref="CubeClaimPair"/>'s closed 3×2 that
/// an analysis's best pair names as a verdict (halheinrich/backgammon#86;
/// amended 2026-09-02 by halheinrich/backgammon#187). Immutable;
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
/// <b>A cube decision buckets once</b>, keyed by its best claim pair: pool
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
/// <see cref="CubeClaimPair"/> instances they count, so reading a bucket
/// back to its pair needs no translation. The classifier <b>consumes the
/// producer verdict</b> — <see cref="DecisionData.BestClaimPair"/>, the one
/// derivation site of the truth claim — and never walks the equities itself
/// (SPEC-scoring §3, "Answer-type classification consumes the claim"). The
/// sixth cell of the 3×2, the incoherent
/// <see cref="CubeClaimPair.NoDoublePass"/> that the ruled tie-breaks
/// compose as derived truth at the exact <c>NoDoubleEquity == 1</c>
/// boundary, is <b>just too good</b>: it houses in
/// <see cref="TooGoodPass"/> as the too-good posture's degenerate point,
/// not a sixth bucket (SPEC-scoring §3, ruled 2026-09-01).
/// </para>
///
/// <para>
/// <b>The retired cell.</b> <see cref="CubeClaimPair.TooGoodTake"/> had a
/// bucket of its own between the halheinrich/backgammon#86 leg and the
/// 2026-09-02 amendment; it is now the one cell of the 3×2 with no bucket.
/// Under the amended predicate Too Good requires the pass — a position the
/// opponent would take is No double / Take whatever the no-double equity —
/// so <see cref="DecisionData.BestClaimPair"/> never derives that pair and a
/// bucket for it could only ever read zero (SPEC-scoring §3, amendment
/// consequence (iii): the classifier's cube buckets return to four). The
/// data-types library keeps the cell representable, so the fold names it
/// explicitly and refuses it rather than housing it silently: a producer
/// that started deriving it again would be a regression, not a corpus to
/// count. The fold stays total over every pair the producer can derive.
/// </para>
/// </summary>
/// <param name="CheckerPlays">Decisions whose answer is a checker play.</param>
/// <param name="NoDoubleTake">
/// Cube decisions whose best pair is <see cref="CubeClaimPair.NoDoubleTake"/>
/// — including, by the 2026-09-02 ruling, every position where playing on
/// beats being taken yet the opponent would still take (XG's "Too good to
/// double/Take"), since Too Good requires the pass.
/// </param>
/// <param name="DoubleTake">
/// Cube decisions whose best pair is <see cref="CubeClaimPair.DoubleTake"/>.
/// </param>
/// <param name="DoublePass">
/// Cube decisions whose best pair is <see cref="CubeClaimPair.DoublePass"/>.
/// </param>
/// <param name="TooGoodPass">
/// Cube decisions whose best pair is <see cref="CubeClaimPair.TooGoodPass"/> —
/// playing on beats cashing and a double would be passed — plus the
/// boundary-composed <see cref="CubeClaimPair.NoDoublePass"/>, which
/// classifies as just too good by ruling (SPEC-scoring §3, 2026-09-01).
/// </param>
public sealed record AnswerTypeDistribution(
    int CheckerPlays,
    int NoDoubleTake,
    int DoubleTake,
    int DoublePass,
    int TooGoodPass)
{
    /// <summary>The empty distribution: every bucket 0.</summary>
    public static AnswerTypeDistribution Empty { get; } = new(0, 0, 0, 0, 0);

    /// <summary>
    /// Total decisions counted — the sum of all five buckets. Derived, never
    /// stored, so it cannot drift from them. Since every
    /// <see cref="Add(DecisionData)"/> increments exactly one bucket, this is
    /// also the number of decisions folded in.
    /// </summary>
    public int Total =>
        CheckerPlays + NoDoubleTake + DoubleTake + DoublePass + TooGoodPass;

    /// <summary>
    /// Return a new distribution with <paramref name="decision"/> counted in
    /// its bucket. Equivalent to adding that decision's one-hot distribution
    /// via <c>operator +</c>; the operator is the single definition of how
    /// distributions combine.
    /// </summary>
    /// <param name="decision">
    /// The decision to classify. A checker play
    /// (<see cref="DecisionData.IsCube"/> false) counts as a checker play; a
    /// cube decision is keyed by its
    /// <see cref="DecisionData.BestClaimPair"/> — the producer's verdict,
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
            a.DoubleTake   + b.DoubleTake,
            a.DoublePass   + b.DoublePass,
            a.TooGoodPass  + b.TooGoodPass);
    }

    /// <summary>
    /// The one-hot distribution for a single decision — the bucketing rule, and
    /// the reason <see cref="Total"/> counts decisions: every branch returns a
    /// distribution whose buckets sum to 1. Total over every pair the
    /// producer derives — the four reachable verdicts plus the
    /// boundary-composed incoherent cell, housed by ruling rather than by
    /// accident — so a legal corpus can never fail to fold.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if a cube decision's best pair is one the producer does not
    /// derive: the retired <see cref="CubeClaimPair.TooGoodTake"/> (Too Good
    /// requires the pass — SPEC-scoring §3, amended 2026-09-02), or a pair
    /// outside <see cref="CubeClaimPair"/>'s closed 3×2 (unreachable, since
    /// that type guards both halves on construction). Both arms exist because
    /// a silent extra bucket would break the fold contract rather than fail.
    /// </exception>
    private static AnswerTypeDistribution Classify(DecisionData decision)
    {
        if (!decision.IsCube)
            return Empty with { CheckerPlays = 1 };

        CubeClaimPair best = decision.BestClaimPair;
        return (best.Claim, best.Taker) switch
        {
            (CubeClaim.NoDouble, CubeAction.Take) => Empty with { NoDoubleTake = 1 },
            (CubeClaim.Double,   CubeAction.Take) => Empty with { DoubleTake   = 1 },
            (CubeClaim.Double,   CubeAction.Pass) => Empty with { DoublePass   = 1 },
            (CubeClaim.TooGood,  CubeAction.Pass) => Empty with { TooGoodPass  = 1 },
            // "(No Double, Pass) is just too good" — the incoherent cell is
            // the too-good posture's degenerate point, not a sixth bucket
            // (SPEC-scoring §3, ruled 2026-09-01).
            (CubeClaim.NoDouble, CubeAction.Pass) => Empty with { TooGoodPass  = 1 },
            // The retired verdict: representable in the type, never derived
            // by the producer since Too Good requires the pass (SPEC-scoring
            // §3, amended 2026-09-02; halheinrich/backgammon#187). Named
            // rather than left to the guard arm below so that a producer
            // regression fails by name.
            (CubeClaim.TooGood,  CubeAction.Take) => throw new ArgumentOutOfRangeException(
                nameof(decision), best,
                "Cube best claim pair is the retired Too good / Take verdict, which the " +
                "producer no longer derives (Too Good requires the pass)."),
            _ => throw new ArgumentOutOfRangeException(nameof(decision), best,
                "Cube best claim pair is outside CubeClaimPair's closed 3×2."),
        };
    }
}
