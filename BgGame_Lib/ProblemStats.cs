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
/// stored. <b>A cube answer adds two decisions</b>, its doubling half and its
/// take half, each correct when its part of the cost counts as zero, with the
/// whole cost as the loss: the shape, unit and schema version the record has
/// always had (SPEC-scoring §3, 2026-10-01; SPEC-stats-identity §3, v3). No
/// double's take part is 0, so its take half is a correct zero. One quizzed
/// position still yields one lifetime record; only the counting granularity
/// inside it is per-half. The session score counts a cube answer otherwise
/// (<see cref="QuizScore.Plus(SubmittedCubeAnswer)"/>: no Take row for No
/// double, and the Total counts each answer once), so the two are not
/// required to reconcile for cube answers.
/// </para>
/// <para>
/// <b>Records written before the 2026-10-01 amendment are kept as they
/// are.</b> Their tallies were folded under the earlier cube scoring and are
/// not re-scored or restarted; a new fold adds to them.
/// </para>
///
/// <para>
/// <b>Kind purity is structural.</b> A record only ever folds one decision
/// kind: a play key and a cube key are distinct by grammar (the dice field
/// rides on the play key), so the key-match guard on <c>Plus</c> makes a
/// mixed-kind fold unrepresentable — which is what keeps per-kind
/// derivations over the tally (e.g. <c>QuizStatsMeasures.Sightings</c>'
/// divide-by-two for cubes) exact. The other half of that exactness is that
/// a cube record's count is a whole number of answers
/// (<see cref="HoldsWholeAnswers"/>): every fold keeps it so, and a document
/// refuses a record that is not (<see cref="ProblemStatsDocument.FromStats"/>).
/// </para>
///
/// <para>
/// Folds take an already-resolved <see cref="DateTimeOffset"/> rather than a
/// <see cref="TimeProvider"/>: the clock seam lives one level up, on
/// <see cref="ProblemStatsDocument"/> (the consumer's entry point), keeping
/// this record's fold a pure value computation — deliberate asymmetry, see
/// the document type.
/// </para>
///
/// <para>
/// <b>Every submission carries its record's key</b>, derived where it is
/// scored (<see cref="PlaySubmission.Score"/>, <see cref="SubmittedCubeAnswer.Score"/>),
/// so there is no keyless case. A submission can still be handed to another
/// problem's record, and that is refused: folding under a key that is not
/// <see cref="Key"/>, or merging another problem's record, is data corruption.
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
    /// Whether <see cref="Tally"/> counts a whole number of answers to this
    /// record's problem: always for a checker play, which folds one
    /// submission per answer; for a cube problem, only an even count, since
    /// every answer folds its two halves
    /// (<see cref="Plus(SubmittedCubeAnswer, DateTimeOffset)"/>, and every
    /// cube fold since the stats document's version 2). The kind is read off
    /// the key's grammar (<see cref="ProblemKey.IsCubeDecision"/>), the one
    /// statement of it a record carries.
    /// </summary>
    /// <remarks>
    /// The folds keep this true of every record that starts true, and
    /// <see cref="ProblemStatsDocument.FromStats"/> — the door every whole
    /// record enters a document by, the readers' included — refuses a record
    /// for which it is false. So a seen record in a document always has a
    /// whole sighting to divide its loss by, and a measure over it is finite.
    /// </remarks>
    internal bool HoldsWholeAnswers => !Key.IsCubeDecision || int.IsEvenInteger(Tally.Submitted);

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
    public static ProblemStats From(SubmittedPlay play, DateTimeOffset quizzedAt)
    {
        ArgumentNullException.ThrowIfNull(play);
        return Seed(play.ProblemKey).Plus(play, quizzedAt);
    }

    /// <summary>
    /// Create the first-ever record for <paramref name="cube"/>'s problem,
    /// with that answer folded in. Defined as an empty seed plus
    /// <see cref="Plus(SubmittedCubeAnswer, DateTimeOffset)"/>, so the fold
    /// rule has a single definition.
    /// </summary>
    /// <param name="cube">The first submission for this problem.</param>
    /// <param name="quizzedAt">The resolved fold timestamp.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="cube"/> is <see langword="null"/>.
    /// </exception>
    public static ProblemStats From(SubmittedCubeAnswer cube, DateTimeOffset quizzedAt)
    {
        ArgumentNullException.ThrowIfNull(cube);
        return Seed(cube.ProblemKey).Plus(cube, quizzedAt);
    }

    /// <summary>The empty record of <paramref name="key"/>'s problem, which a first fold starts from.</summary>
    private static ProblemStats Seed(ProblemKey key) => new(key, ScoreSegment.Empty, LastQuizzed: default);

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
    /// not <see cref="Key"/> — folding under the wrong key is data
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
    /// decisions (SPEC-scoring §3, 2026-10-01): its doubling half, correct by
    /// <see cref="SubmittedCubeAnswer.IsDoublingPartCorrect"/>, and its take
    /// half, correct by <see cref="SubmittedCubeAnswer.IsTakePartCorrect"/>,
    /// with the whole cost (<see cref="CubeAnswerCost.Total"/>) accumulated
    /// and <see cref="LastQuizzed"/> advanced to <paramref name="quizzedAt"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every answer adds both halves, No double included: its implied take is
    /// never charged, so its take part is 0 and its take half a correct zero.
    /// That keeps the unit at two per cube answer, which the record has always
    /// had, so a cube record's sightings stay its count ÷ 2, its wrong rate
    /// stays per half, and its loss per answer sums the two halves
    /// (<c>QuizStatsMeasures.LossPerAnswer</c>).
    /// </para>
    /// <para>
    /// The halves are judged one by one. An answer whose two parts each count
    /// as zero is two correct halves here, even where their sum does not count
    /// as zero and the session's Total counts the answer wrong
    /// (<see cref="QuizScore.Plus(SubmittedCubeAnswer)"/>).
    /// </para>
    /// </remarks>
    /// <param name="cube">The submission to fold.</param>
    /// <param name="quizzedAt">The resolved fold timestamp.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="cube"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the submission's <see cref="SubmittedCubeAnswer.ProblemKey"/>
    /// is not <see cref="Key"/> — folding under the wrong key is data
    /// corruption.
    /// </exception>
    public ProblemStats Plus(SubmittedCubeAnswer cube, DateTimeOffset quizzedAt)
    {
        ArgumentNullException.ThrowIfNull(cube);
        RequireMatchingKey(cube.ProblemKey, nameof(cube));
        return this with
        {
            Tally = Tally.Add(
                2,
                (cube.IsDoublingPartCorrect ? 1 : 0) + (cube.IsTakePartCorrect ? 1 : 0),
                cube.Cost.Total),
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

    /// <summary>
    /// Refuses <paramref name="key"/> unless it is this record's
    /// <see cref="Key"/>: a submission folded into, or a record merged with,
    /// another problem's record is data corruption.
    /// </summary>
    private void RequireMatchingKey(ProblemKey key, string paramName)
    {
        if (key != Key)
            throw new ArgumentException(
                $"Submission is for problem '{key}' but this record tracks '{Key}'.",
                paramName);
    }
}
