namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// Lifetime quiz statistics for one decision, keyed by its stable
/// <see cref="DecisionId"/>: a composed <see cref="ScoreSegment"/> tally plus
/// the date the decision was last quizzed. Immutable; each <c>Plus</c> call
/// returns a new <see cref="DecisionStats"/>.
///
/// <para>
/// The tally reuses <see cref="ScoreSegment"/> — the library's single
/// accumulation primitive — rather than restating submitted/correct/loss
/// fields locally; the wrong count is the derived <see cref="Wrong"/>, never
/// stored. Unlike <see cref="QuizScore.Plus(SubmittedCubeAction)"/>, which
/// scores a cube position's doubler and taker halves as two independent
/// segment submissions, a cube position folds into this record as <b>one</b>
/// decision: correct only when both halves were right, with the equity loss
/// of both halves accumulated. One quizzed position — one lifetime record.
/// </para>
///
/// <para>
/// Folds take an already-resolved <see cref="DateTimeOffset"/> rather than a
/// <see cref="TimeProvider"/>: the clock seam lives one level up, on
/// <see cref="DecisionStatsDocument"/> (the consumer's entry point), keeping
/// this record's fold a pure value computation — deliberate asymmetry, see
/// the document type.
/// </para>
/// </summary>
/// <param name="Id">Stable identity of the quizzed decision.</param>
/// <param name="Tally">Lifetime running tally over this decision's submissions.</param>
/// <param name="LastQuizzed">When this decision was most recently folded.</param>
public sealed record DecisionStats(DecisionId Id, ScoreSegment Tally, DateTimeOffset LastQuizzed)
{
    /// <summary>
    /// Number of submissions that were not correct. Derived
    /// (<c>Tally.Submitted − Tally.Correct</c>) — never stored, so it cannot
    /// drift from the tally.
    /// </summary>
    public int Wrong => Tally.Submitted - Tally.Correct;

    /// <summary>
    /// Create the first-ever record for <paramref name="play"/>'s decision,
    /// with that play folded in. Defined as an empty seed plus
    /// <see cref="Plus(SubmittedPlay, DateTimeOffset)"/>, so the fold rule has
    /// a single definition.
    /// </summary>
    /// <param name="play">The first submission for this decision.</param>
    /// <param name="quizzedAt">The resolved fold timestamp.</param>
    public static DecisionStats From(SubmittedPlay play, DateTimeOffset quizzedAt)
    {
        ArgumentNullException.ThrowIfNull(play);
        return Seed(play.DecisionId).Plus(play, quizzedAt);
    }

    /// <inheritdoc cref="From(SubmittedPlay, DateTimeOffset)"/>
    /// <param name="cube">The first submission for this decision.</param>
    /// <param name="quizzedAt">The resolved fold timestamp.</param>
    public static DecisionStats From(SubmittedCubeAction cube, DateTimeOffset quizzedAt)
    {
        ArgumentNullException.ThrowIfNull(cube);
        return Seed(cube.DecisionId).Plus(cube, quizzedAt);
    }

    private static DecisionStats Seed(DecisionId id) =>
        new(id, ScoreSegment.Empty, LastQuizzed: default);

    /// <summary>
    /// Return a new record with <paramref name="play"/> folded in: one
    /// submission, correct iff <see cref="SubmittedPlay.IsCorrect"/>, its
    /// <see cref="SubmittedPlay.EquityLoss"/> accumulated, and
    /// <see cref="LastQuizzed"/> advanced to <paramref name="quizzedAt"/>.
    /// </summary>
    /// <param name="play">The submission to fold.</param>
    /// <param name="quizzedAt">The resolved fold timestamp.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the submission's <see cref="SubmittedPlay.DecisionId"/> is
    /// not <see cref="Id"/> — folding under the wrong key is data corruption.
    /// </exception>
    public DecisionStats Plus(SubmittedPlay play, DateTimeOffset quizzedAt)
    {
        ArgumentNullException.ThrowIfNull(play);
        RequireMatchingId(play.DecisionId, nameof(play));
        return this with
        {
            Tally = Tally.Add(1, play.IsCorrect ? 1 : 0, play.EquityLoss),
            LastQuizzed = quizzedAt,
        };
    }

    /// <summary>
    /// Return a new record with <paramref name="cube"/> folded in as
    /// <b>one</b> decision: correct only when both the doubler and taker
    /// halves were right, with both halves' equity losses accumulated, and
    /// <see cref="LastQuizzed"/> advanced to <paramref name="quizzedAt"/>.
    /// </summary>
    /// <param name="cube">The submission to fold.</param>
    /// <param name="quizzedAt">The resolved fold timestamp.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the submission's <see cref="SubmittedCubeAction.DecisionId"/>
    /// is not <see cref="Id"/> — folding under the wrong key is data corruption.
    /// </exception>
    public DecisionStats Plus(SubmittedCubeAction cube, DateTimeOffset quizzedAt)
    {
        ArgumentNullException.ThrowIfNull(cube);
        RequireMatchingId(cube.DecisionId, nameof(cube));
        bool correct = cube.DoublerCorrect && cube.TakerCorrect;
        return this with
        {
            Tally = Tally.Add(1, correct ? 1 : 0, cube.DoublerEquityLoss + cube.TakerEquityLoss),
            LastQuizzed = quizzedAt,
        };
    }

    private void RequireMatchingId(DecisionId submissionId, string paramName)
    {
        if (submissionId != Id)
            throw new ArgumentException(
                $"Submission is for decision '{submissionId}' but this record tracks '{Id}'.",
                paramName);
    }
}
