namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// Lifetime quiz statistics for one problem, keyed by its content identity
/// <see cref="BgDataTypes_Lib.ProblemKey"/>: a composed <see cref="ScoreSegment"/>
/// tally plus the date the problem was last quizzed. Immutable; each
/// <c>Plus</c> call returns a new <see cref="ProblemStats"/>.
///
/// <para>
/// Content-keyed, not provenance-keyed (SPEC-stats-identity.md §4;
/// halheinrich/backgammon#95): every content-equal copy of a problem —
/// whatever file or match it came from — folds into this one record, so the
/// lifetime tally can never fragment across file-relative <c>DecisionId</c>s.
/// </para>
///
/// <para>
/// The tally reuses <see cref="ScoreSegment"/> — the library's single
/// accumulation primitive — rather than restating submitted/correct/loss
/// fields locally; the wrong count is the derived <see cref="Wrong"/>, never
/// stored. Cube positions are counted the same way here as in
/// <see cref="QuizScore.Plus(SubmittedCubeAction)"/>: the doubler and taker
/// halves are two decisions, so a cube submission adds <b>two</b> to the
/// tally — one per half that was right — with both halves' equity losses
/// accumulated. One quizzed position still yields one lifetime record; only
/// the counting granularity inside it is per-half.
/// </para>
///
/// <para>
/// <b>Kind purity is structural.</b> A record only ever folds one decision
/// kind: a play key and a cube key are distinct by grammar (the dice field
/// rides on the play key), so the key-match guard on <c>Plus</c> makes a
/// mixed-kind fold unrepresentable — which is what keeps per-kind
/// derivations over the tally (e.g. <c>QuizStatsMeasures.Sightings</c>'
/// divide-by-two for cubes) exact.
/// </para>
///
/// <para>
/// Folds take an already-resolved <see cref="DateTimeOffset"/> rather than a
/// <see cref="TimeProvider"/>: the clock seam lives one level up, on
/// <see cref="ProblemStatsDocument"/> (the consumer's entry point), keeping
/// this record's fold a pure value computation — deliberate asymmetry, see
/// the document type. So is the no-key filter: a submission whose
/// <c>ProblemKey</c> is null (the no-key rung) is skipped by the document's
/// <c>Plus</c> and can never reach — or seed — a record here; at this level a
/// null or mismatched key is rejected as data corruption.
/// </para>
/// </summary>
/// <param name="Key">Content identity of the quizzed problem.</param>
/// <param name="Tally">Lifetime running tally over this problem's submissions.</param>
/// <param name="LastQuizzed">When this problem was most recently folded.</param>
public sealed record ProblemStats(ProblemKey Key, ScoreSegment Tally, DateTimeOffset LastQuizzed)
{
    /// <summary>
    /// Number of submissions that were not correct. Derived
    /// (<c>Tally.Submitted − Tally.Correct</c>) — never stored, so it cannot
    /// drift from the tally.
    /// </summary>
    public int Wrong => Tally.Submitted - Tally.Correct;

    /// <summary>
    /// Create the first-ever record for <paramref name="play"/>'s problem,
    /// with that play folded in. Defined as an empty seed plus
    /// <see cref="Plus(SubmittedPlay, DateTimeOffset)"/>, so the fold rule has
    /// a single definition.
    /// </summary>
    /// <param name="play">The first submission for this problem.</param>
    /// <param name="quizzedAt">The resolved fold timestamp.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="play"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the submission carries no <see cref="ProblemKey"/> — a
    /// no-key submission is never recorded (the document-level <c>Plus</c> is
    /// the single filter), so seeding a record from one is a caller bug.
    /// </exception>
    public static ProblemStats From(SubmittedPlay play, DateTimeOffset quizzedAt)
    {
        ArgumentNullException.ThrowIfNull(play);
        return Seed(play.ProblemKey, nameof(play)).Plus(play, quizzedAt);
    }

    /// <inheritdoc cref="From(SubmittedPlay, DateTimeOffset)"/>
    /// <param name="cube">The first submission for this problem.</param>
    /// <param name="quizzedAt">The resolved fold timestamp.</param>
    public static ProblemStats From(SubmittedCubeAction cube, DateTimeOffset quizzedAt)
    {
        ArgumentNullException.ThrowIfNull(cube);
        return Seed(cube.ProblemKey, nameof(cube)).Plus(cube, quizzedAt);
    }

    private static ProblemStats Seed(ProblemKey? key, string paramName)
    {
        if (key is null)
            throw new ArgumentException(
                "Submission carries no ProblemKey; a no-key submission is not recorded " +
                "and cannot seed a lifetime record.", paramName);
        return new(key, ScoreSegment.Empty, LastQuizzed: default);
    }

    /// <summary>
    /// Return a new record with <paramref name="play"/> folded in: one
    /// submission, correct iff <see cref="SubmittedPlay.IsCorrect"/>, its
    /// <see cref="SubmittedPlay.EquityLoss"/> accumulated, and
    /// <see cref="LastQuizzed"/> advanced to <paramref name="quizzedAt"/>.
    /// </summary>
    /// <param name="play">The submission to fold.</param>
    /// <param name="quizzedAt">The resolved fold timestamp.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the submission's <see cref="SubmittedPlay.ProblemKey"/> is
    /// null or not <see cref="Key"/> — folding under the wrong key is data
    /// corruption.
    /// </exception>
    public ProblemStats Plus(SubmittedPlay play, DateTimeOffset quizzedAt)
    {
        ArgumentNullException.ThrowIfNull(play);
        RequireMatchingKey(play.ProblemKey, nameof(play));
        return this with
        {
            Tally = Tally.Add(1, play.IsCorrect ? 1 : 0, play.EquityLoss),
            LastQuizzed = quizzedAt,
        };
    }

    /// <summary>
    /// Return a new record with <paramref name="cube"/> folded in as <b>two</b>
    /// decisions — the doubler half and the taker half — matching
    /// <see cref="QuizScore.Plus(SubmittedCubeAction)"/>: two submissions, one
    /// correct per half that was right, both halves' equity losses
    /// accumulated, and <see cref="LastQuizzed"/> advanced to
    /// <paramref name="quizzedAt"/>. A half-right cube therefore reads
    /// 1-of-2, not 0-of-1.
    ///
    /// <para>
    /// "Right" per half is the submission's own claim-vs-claim / action-vs-action
    /// derivation (<see cref="SubmittedCubeAction.DoublerCorrect"/> /
    /// <see cref="SubmittedCubeAction.TakerCorrect"/>), the same source
    /// <see cref="QuizScore.Plus(SubmittedCubeAction)"/> folds — so a wrong
    /// claim over the right board action folds here too as one wrong half at
    /// +0.000 (SPEC-scoring §3; halheinrich/backgammon#86).
    /// </para>
    /// </summary>
    /// <param name="cube">The submission to fold.</param>
    /// <param name="quizzedAt">The resolved fold timestamp.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the submission's <see cref="SubmittedCubeAction.ProblemKey"/>
    /// is null or not <see cref="Key"/> — folding under the wrong key is data
    /// corruption.
    /// </exception>
    public ProblemStats Plus(SubmittedCubeAction cube, DateTimeOffset quizzedAt)
    {
        ArgumentNullException.ThrowIfNull(cube);
        RequireMatchingKey(cube.ProblemKey, nameof(cube));
        return this with
        {
            Tally = Tally.Add(
                2,
                (cube.DoublerCorrect ? 1 : 0) + (cube.TakerCorrect ? 1 : 0),
                cube.DoublerEquityLoss + cube.TakerEquityLoss),
            LastQuizzed = quizzedAt,
        };
    }

    /// <summary>
    /// Return a new record combining this one with <paramref name="other"/>,
    /// a second lifetime record of the <b>same</b> problem: the tallies summed
    /// field by field (<see cref="ScoreSegment"/>'s <c>operator +</c> — the
    /// single accumulation primitive, so no field is restated here) and
    /// <see cref="LastQuizzed"/> the later of the two. The per-record half of
    /// <see cref="ProblemStatsDocument.Merge"/>, which applies it per shared
    /// key (halheinrich/backgammon#187). Pure and commutative; associative up
    /// to floating-point rounding of the equity-loss sum.
    /// </summary>
    /// <remarks>
    /// "Later" compares instants. Two records quizzed at the same instant in
    /// different offsets tie; the tie goes to the larger offset so the result
    /// does not depend on which side was the receiver — commutativity holds
    /// on the offset too, not only on the instant record equality compares.
    /// </remarks>
    /// <param name="other">The record to combine with this one.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="other"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="other"/> tracks a different
    /// <see cref="Key"/> — merging records of two problems is data
    /// corruption, exactly as folding under the wrong key is.
    /// </exception>
    public ProblemStats Merge(ProblemStats other)
    {
        ArgumentNullException.ThrowIfNull(other);
        RequireMatchingKey(other.Key, nameof(other));
        return this with
        {
            Tally = Tally + other.Tally,
            LastQuizzed = Later(LastQuizzed, other.LastQuizzed),
        };
    }

    private static DateTimeOffset Later(DateTimeOffset a, DateTimeOffset b)
    {
        int byInstant = a.CompareTo(b);
        if (byInstant != 0)
            return byInstant > 0 ? a : b;
        return a.Offset >= b.Offset ? a : b;
    }

    private void RequireMatchingKey(ProblemKey? submissionKey, string paramName)
    {
        if (submissionKey != Key)
            throw new ArgumentException(
                $"Submission is for problem '{submissionKey?.ToString() ?? "<no key>"}' " +
                $"but this record tracks '{Key}'.",
                paramName);
    }
}
