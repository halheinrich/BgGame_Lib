namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// When a scored loss is correct: the one statement of the verdict in this
/// library. A checker play (<see cref="SubmittedPlay.IsCorrect"/>), each part
/// of a cube answer (<see cref="SubmittedCubeAnswer.IsDoublingPartCorrect"/>,
/// <see cref="SubmittedCubeAnswer.IsTakePartCorrect"/>) and each whole cube
/// answer (<see cref="SubmittedCubeAnswer.IsCorrect"/>) is judged here and
/// nowhere else.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rule is the zero rule</b> (SPEC-scoring §2a and §3, amended
/// 2026-10-01; halheinrich/backgammon#202, folded into
/// halheinrich/backgammon#326): a loss is correct exactly when it counts as
/// zero, which is when it displays as <c>0.0000</c>. Both the precision and
/// the threshold are BgDataTypes_Lib's (<see cref="EquityLoss.CountsAsZero"/>),
/// so this class keeps neither: what a consumer shows through
/// <see cref="EquityLoss.Format"/> and what is scored here cannot disagree.
/// </para>
/// <para>
/// <b>One place, so a tolerance widens one place.</b> The user-set error
/// tolerance of halheinrich/backgammon#30 is planned as a widening of this
/// predicate, not a second rule beside it. Nothing else in the library
/// compares a loss with zero or with an answer to reach a verdict.
/// </para>
/// <para>
/// <b>Verdicts only.</b> The rule judges a loss; it changes none. Costs and
/// errors stay exact as the producer derives them, and a sum is judged as a
/// sum: two parts that each count as zero can add up to a whole that does
/// not.
/// </para>
/// </remarks>
internal static class Correctness
{
    /// <summary>
    /// Whether <paramref name="loss"/>, a scored equity loss, is correct:
    /// exactly when it counts as zero (<see cref="EquityLoss.CountsAsZero"/>).
    /// </summary>
    /// <param name="loss">A finite equity loss: a play's error, a cube answer's cost or one of its parts.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="loss"/> is NaN or infinite, as
    /// <see cref="EquityLoss.CountsAsZero"/> refuses it.
    /// </exception>
    internal static bool IsCorrect(double loss) => EquityLoss.CountsAsZero(loss);
}
