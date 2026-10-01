namespace BgGame_Lib;

using System.Diagnostics;
using BgDataTypes_Lib;

/// <summary>
/// A count of decisions by the kind of answer each one calls for: checker
/// plays, plus one bucket per cube answer — the four of SPEC-scoring §3,
/// amended 2026-09-30 on halheinrich/backgammon#326 — keyed by each cube
/// decision's truth. Immutable; <see cref="Add(BgDecisionData)"/> and
/// <c>operator +</c> each return a new <see cref="AnswerTypeDistribution"/>.
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
/// <b>Fold contract:</b> every <see cref="Add(BgDecisionData)"/> increments
/// <b>exactly one</b> bucket, so <see cref="Total"/> equals the number of
/// decisions folded in. A consumer folding a filtered pool therefore reads
/// that pool's size straight off <see cref="Total"/> and needs no second
/// "how many matched" pass — one enumeration, one rule, nothing that can
/// drift.
/// </para>
///
/// <para>
/// <b>A cube decision buckets once</b>, keyed by its truth: pool membership
/// counts rows, and one cube position is one row. This is deliberately
/// <i>not</i> how a cube answer is scored, where it is one Total decision and
/// up to two row decisions in the session (<see cref="QuizScore"/>) and two
/// half decisions in the lifetime stats (<see cref="ProblemStats"/>). Those
/// count answers given; this counts problems present.
/// </para>
///
/// <para>
/// <b>The classification consumes the producer's truth</b>,
/// <see cref="CubeDecisionData.BestAnswer"/>, the one derivation of it, and
/// never walks the equities itself (SPEC-scoring §3, "Answer-type
/// classification consumes the claim"). The four cube buckets are named for
/// the <see cref="CubeAnswer"/> members they count, so reading a bucket back
/// to its answer needs no translation. The fourth is named by its meaning,
/// "don't double, they'd pass", never by a label: it reads Too good where
/// gammons are possible and No double / Pass where they are not
/// (<see cref="CubeDecision.ClaimOf"/>), and both readings count here. At the
/// tie of halheinrich/backgammon#293 the truth is the fourth answer, so the
/// tie counts there too (SPEC-scoring §3: "(No Double, Pass) is just too
/// good"), though more than one answer is correct at it. The buckets' display
/// labels are the consuming app's copy.
/// </para>
/// </summary>
/// <param name="CheckerPlays">Decisions whose answer is a checker play.</param>
/// <param name="NoDouble">
/// Cube decisions whose truth is <see cref="CubeAnswer.NoDouble"/>: don't
/// double, and they'd take — including, by the 2026-09-02 ruling, every
/// position where playing on beats being taken yet the opponent would still
/// take.
/// </param>
/// <param name="DoubleTake">
/// Cube decisions whose truth is <see cref="CubeAnswer.DoubleTake"/>.
/// </param>
/// <param name="DoublePass">
/// Cube decisions whose truth is <see cref="CubeAnswer.DoublePass"/>.
/// </param>
/// <param name="NoDoublePass">
/// Cube decisions whose truth is <see cref="CubeAnswer.NoDoublePass"/>, the
/// fourth answer: don't double, they'd pass — under either of its labels, and
/// at the halheinrich/backgammon#293 tie.
/// </param>
public sealed record AnswerTypeDistribution(
    int CheckerPlays,
    int NoDouble,
    int DoubleTake,
    int DoublePass,
    int NoDoublePass)
{
    /// <summary>The empty distribution: every bucket 0.</summary>
    public static AnswerTypeDistribution Empty { get; } = new(0, 0, 0, 0, 0);

    /// <summary>
    /// Total decisions counted — the sum of all five buckets. Derived, never
    /// stored, so it cannot drift from them. Since every
    /// <see cref="Add(BgDecisionData)"/> increments exactly one bucket, this is
    /// also the number of decisions folded in.
    /// </summary>
    public int Total =>
        CheckerPlays + NoDouble + DoubleTake + DoublePass + NoDoublePass;

    /// <summary>
    /// Return a new distribution with <paramref name="decision"/> counted in
    /// its bucket. Equivalent to adding that decision's one-hot distribution
    /// via <c>operator +</c>; the operator is the single definition of how
    /// distributions combine.
    /// </summary>
    /// <param name="decision">
    /// The decision to classify, matched on its kind: a
    /// <see cref="CheckerPlayDecision"/> counts as a checker play; a
    /// <see cref="CubeDecision"/> is keyed by its category's
    /// <see cref="CubeDecisionData.BestAnswer"/> — the producer's truth,
    /// never re-derived from equities here.
    /// </param>
    public AnswerTypeDistribution Add(BgDecisionData decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return this + decision.Match(
            checkerPlay: static _ => Empty with { CheckerPlays = 1 },
            cube: static cube => ClassifyCube(cube.Decision.BestAnswer));
    }

    /// <summary>Combine two distributions bucket-wise.</summary>
    public static AnswerTypeDistribution operator +(
        AnswerTypeDistribution a, AnswerTypeDistribution b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return new AnswerTypeDistribution(
            a.CheckerPlays + b.CheckerPlays,
            a.NoDouble     + b.NoDouble,
            a.DoubleTake   + b.DoubleTake,
            a.DoublePass   + b.DoublePass,
            a.NoDoublePass + b.NoDoublePass);
    }

    /// <summary>
    /// The one-hot distribution for a cube decision whose truth is
    /// <paramref name="truth"/>. With the checker-play arm of
    /// <see cref="Add(BgDecisionData)"/> it is the bucketing rule, and the
    /// reason <see cref="Total"/> counts decisions: every arm returns a
    /// distribution whose buckets sum to 1. Total over the four answers, the
    /// only values the producer's truth takes.
    /// </summary>
    private static AnswerTypeDistribution ClassifyCube(CubeAnswer truth) => truth switch
    {
        CubeAnswer.NoDouble => Empty with { NoDouble = 1 },
        CubeAnswer.DoubleTake => Empty with { DoubleTake = 1 },
        CubeAnswer.DoublePass => Empty with { DoublePass = 1 },
        CubeAnswer.NoDoublePass => Empty with { NoDoublePass = 1 },
        _ => throw new UnreachableException($"A cube decision's truth is one of the four answers, not {truth}."),
    };
}
