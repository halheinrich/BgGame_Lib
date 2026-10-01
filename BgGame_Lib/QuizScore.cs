namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// The cumulative session score, in the rows SPEC-scoring §3's 2026-10-01
/// amendment rules: <see cref="PlayDecisions"/>, <see cref="DoubleDecisions"/>
/// and <see cref="TakeDecisions"/>, and their <see cref="Total"/>. Immutable;
/// built only from <see cref="Empty"/> by the <c>Plus</c> folds, each of which
/// returns a new score.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rows.</b> Each checker play adds one decision to
/// <see cref="PlayDecisions"/>. Each cube answer adds its doubling part to
/// <see cref="DoubleDecisions"/>, and its take part to
/// <see cref="TakeDecisions"/> only when it commits to a response
/// (<see cref="CubeAnswerExtensions.CommitsToResponse"/>): No double's implied
/// take is never charged, so No double is left out of the Take row. Every row
/// counts a decision correct by that decision's own verdict, which the
/// submission derives from its cost.
/// </para>
/// <para>
/// <b>The Total counts each checker play and each cube answer once</b>, and
/// is not the sum of the rows. Its submitted count is Play's plus Double's,
/// since every cube answer adds exactly one decision to Double. Its correct
/// count is its own: a cube answer is correct there when its <i>whole</i>
/// cost counts as zero (<see cref="SubmittedCubeAnswer.IsCorrect"/>), which
/// two correct parts do not guarantee. Its loss is the sum of the rows'
/// losses. Because the correct count cannot be recovered from the rows, the
/// score holds that one fact beside them; everything else in the Total is
/// derived from the rows on each read, so it cannot drift from them.
/// </para>
/// <para>
/// <b>Factory-only.</b> There is no public constructor and no
/// <c>with</c>, so no caller can state a row, or a Total, that its folds did
/// not produce. Value equality compares the three rows and the Total's own
/// correct count.
/// </para>
/// <para>
/// The session score never reads a submission's problem key, and counts a
/// cube answer differently from the lifetime stats, which keep two decisions
/// per answer (<see cref="ProblemStats.Plus(SubmittedCubeAnswer, DateTimeOffset)"/>).
/// </para>
/// </remarks>
public sealed class QuizScore : IEquatable<QuizScore>
{
    // The Total's correct cube answers: those whose whole cost counts as
    // zero. The one fact of the Total its rows cannot give (see the remarks).
    private readonly int _cubeAnswersCorrect;

    private QuizScore(
        ScoreSegment playDecisions,
        ScoreSegment doubleDecisions,
        ScoreSegment takeDecisions,
        int cubeAnswersCorrect)
    {
        PlayDecisions = playDecisions;
        DoubleDecisions = doubleDecisions;
        TakeDecisions = takeDecisions;
        _cubeAnswersCorrect = cubeAnswersCorrect;
    }

    /// <summary>The starting score: every row empty, and so the Total.</summary>
    public static QuizScore Empty { get; } =
        new(ScoreSegment.Empty, ScoreSegment.Empty, ScoreSegment.Empty, cubeAnswersCorrect: 0);

    /// <summary>The Play row: one decision per checker play, its error the loss.</summary>
    public ScoreSegment PlayDecisions { get; }

    /// <summary>
    /// The Double row: one decision per cube answer, every answer, with its
    /// doubling part (<see cref="CubeAnswerCost.DoublingPart"/>) as the loss,
    /// correct when that part counts as zero.
    /// </summary>
    public ScoreSegment DoubleDecisions { get; }

    /// <summary>
    /// The Take row: one decision per cube answer that commits to a response
    /// (<see cref="CubeAnswerExtensions.CommitsToResponse"/>), with its take
    /// part (<see cref="CubeAnswerCost.TakePart"/>) as the loss, correct when
    /// that part counts as zero. No double adds nothing here.
    /// </summary>
    public ScoreSegment TakeDecisions { get; }

    /// <summary>
    /// The Total: each checker play and each cube answer counted once.
    /// Submitted is <see cref="PlayDecisions"/>' plus
    /// <see cref="DoubleDecisions"/>'; correct is the correct plays plus the
    /// cube answers whose whole cost counts as zero; the loss is the sum of
    /// the three rows' losses. Neither count is the sum of the rows'. Derived
    /// on each read from the rows and the one fact they cannot give.
    /// </summary>
    public ScoreSegment Total => new(
        PlayDecisions.Submitted + DoubleDecisions.Submitted,
        PlayDecisions.Correct + _cubeAnswersCorrect,
        PlayDecisions.TotalEquityLoss + DoubleDecisions.TotalEquityLoss + TakeDecisions.TotalEquityLoss);

    /// <summary>
    /// Return a new score with <paramref name="play"/> folded in: one decision
    /// in the Play row, correct by <see cref="SubmittedPlay.IsCorrect"/>, its
    /// <see cref="SubmittedPlay.EquityLoss"/> the loss.
    /// </summary>
    /// <param name="play">The scored play.</param>
    /// <exception cref="ArgumentNullException"><paramref name="play"/> is <see langword="null"/>.</exception>
    public QuizScore Plus(SubmittedPlay play)
    {
        ArgumentNullException.ThrowIfNull(play);
        return new(
            PlayDecisions.Add(1, play.IsCorrect ? 1 : 0, play.EquityLoss),
            DoubleDecisions,
            TakeDecisions,
            _cubeAnswersCorrect);
    }

    /// <summary>
    /// Return a new score with <paramref name="cube"/> folded in, per
    /// SPEC-scoring §3's 2026-10-01 amendment: one decision in the Double row
    /// (its doubling part, correct by
    /// <see cref="SubmittedCubeAnswer.IsDoublingPartCorrect"/>); one in the
    /// Take row only when the answer commits to a response (its take part,
    /// correct by <see cref="SubmittedCubeAnswer.IsTakePartCorrect"/>); and
    /// one answer in the Total, correct by
    /// <see cref="SubmittedCubeAnswer.IsCorrect"/>, judged on the whole cost.
    /// </summary>
    /// <param name="cube">The scored cube answer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="cube"/> is <see langword="null"/>.</exception>
    public QuizScore Plus(SubmittedCubeAnswer cube)
    {
        ArgumentNullException.ThrowIfNull(cube);
        CubeAnswerCost cost = cube.Cost;
        return new(
            PlayDecisions,
            DoubleDecisions.Add(1, cube.IsDoublingPartCorrect ? 1 : 0, cost.DoublingPart),
            cube.Answer.CommitsToResponse()
                ? TakeDecisions.Add(1, cube.IsTakePartCorrect ? 1 : 0, cost.TakePart)
                : TakeDecisions,
            _cubeAnswersCorrect + (cube.IsCorrect ? 1 : 0));
    }

    /// <summary>
    /// Whether <paramref name="other"/> is the same score: equal rows and the
    /// same number of wholly correct cube answers — so an equal
    /// <see cref="Total"/> too.
    /// </summary>
    /// <param name="other">The score to compare with, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> for the same score; <see langword="false"/> otherwise.</returns>
    public bool Equals(QuizScore? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        return other is not null
            && PlayDecisions == other.PlayDecisions
            && DoubleDecisions == other.DoubleDecisions
            && TakeDecisions == other.TakeDecisions
            && _cubeAnswersCorrect == other._cubeAnswersCorrect;
    }

    /// <inheritdoc cref="Equals(QuizScore?)"/>
    public override bool Equals(object? obj) => Equals(obj as QuizScore);

    /// <summary>Hash code consistent with <see cref="Equals(QuizScore?)"/>.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() =>
        HashCode.Combine(PlayDecisions, DoubleDecisions, TakeDecisions, _cubeAnswersCorrect);

    /// <summary>
    /// Equality per <see cref="Equals(QuizScore?)"/>; two
    /// <see langword="null"/> operands are equal.
    /// </summary>
    /// <param name="left">The first score, or <see langword="null"/>.</param>
    /// <param name="right">The second score, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if both are <see langword="null"/> or equal.</returns>
    public static bool operator ==(QuizScore? left, QuizScore? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Negation of <see cref="operator ==(QuizScore?, QuizScore?)"/>.</summary>
    /// <param name="left">The first score, or <see langword="null"/>.</param>
    /// <param name="right">The second score, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the operands differ or only one is <see langword="null"/>.</returns>
    public static bool operator !=(QuizScore? left, QuizScore? right) => !(left == right);
}
