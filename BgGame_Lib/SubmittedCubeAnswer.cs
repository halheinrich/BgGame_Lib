namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// One cube answer submitted by a quiz user and scored at its decision: the
/// problem and the answer, which are its identity, and the truth, the cost and
/// whether it is correct, whole and in each part. The cube analog of
/// <see cref="SubmittedPlay"/>, built only by <see cref="Score"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scored from one record, so its facts cannot disagree.</b>
/// <see cref="Score"/> takes the answer and the record it answers, a
/// <see cref="CubeDecision"/>, and reads everything else off that record
/// together: the problem's key (<see cref="ProblemKey.From"/>), the truth
/// (<see cref="CubeDecisionData.BestAnswer"/>) and the answer's cost
/// (<see cref="CubeDecision.CostOf"/>, SPEC-scoring §3 as amended on
/// halheinrich/backgammon#326). There is no public constructor and no member
/// can be reassigned, so no caller can pair an answer with another decision's
/// truth or cost, file it under another problem, or state a verdict its cost
/// contradicts.
/// </para>
/// <para>
/// <b>Correctness is derived from the cost, never from the truth.</b> The
/// answer is correct when its whole cost counts as zero, and each part is
/// correct when that part does (SPEC-scoring §3, 2026-10-01; the rule is
/// stated once, on the internal <c>Correctness</c>). So at an equity tie
/// more than one answer is correct, and <see cref="BestAnswer"/> — the
/// producer's single representative, which classification and display read —
/// is never what an answer is compared with. A sum is judged as a sum: two
/// parts that each count as zero can add up to a whole that does not, so the
/// whole answer is judged on its own and not as the parts' verdicts combined.
/// </para>
/// <para>
/// <b>Where the verdicts are counted.</b> The session score
/// (<see cref="QuizScore.Plus(SubmittedCubeAnswer)"/>) and the lifetime stats
/// (<see cref="ProblemStats.Plus(SubmittedCubeAnswer, DateTimeOffset)"/>)
/// count a cube answer differently, as SPEC-scoring §3's 2026-10-01 amendment
/// rules; both read the verdicts here.
/// </para>
/// <para>
/// <b>Identity: the same problem and the same answer</b>, as a submitted
/// play's is the same problem and the same play (Hal's ruling of 2026-09-27 on
/// halheinrich/backgammon#273). Two submissions are equal exactly when they
/// answer the same problem, <see cref="ProblemKey"/>, with the same answer,
/// <see cref="Answer"/>. <see cref="BestAnswer"/>, <see cref="Cost"/> and the
/// verdicts are the result under one analysis of that problem, and a
/// content-equal record may carry another analysis, so they take no part:
/// two equal submissions can differ in every one of them.
/// </para>
/// </remarks>
public sealed class SubmittedCubeAnswer : IEquatable<SubmittedCubeAnswer>
{
    private SubmittedCubeAnswer(ProblemKey problemKey, CubeAnswer answer, CubeAnswer bestAnswer, CubeAnswerCost cost)
    {
        ProblemKey = problemKey;
        Answer = answer;
        BestAnswer = bestAnswer;
        Cost = cost;
    }

    /// <summary>
    /// Scores <paramref name="answer"/> at <paramref name="decision"/> — the
    /// one place a submitted cube answer is scored. The key, the truth and the
    /// cost are all read off <paramref name="decision"/>, so they belong to
    /// one record.
    /// </summary>
    /// <param name="answer">The answer the user submitted: one of the four.</param>
    /// <param name="decision">
    /// The analysed cube decision answered: the record, not its category, since
    /// the cost needs the position and the session beside the analysis
    /// (whether gammons are possible, <see cref="CubeDecision.GammonsPossible"/>)
    /// and the key is the record's own. A checker play cannot be passed.
    /// </param>
    /// <returns>The scored submission.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="decision"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="answer"/> is not one of the four answers.
    /// </exception>
    public static SubmittedCubeAnswer Score(CubeAnswer answer, CubeDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        // The cost first: it refuses an undefined answer before anything else is read.
        CubeAnswerCost cost = decision.CostOf(answer);
        return new(ProblemKey.From(decision), answer, decision.Decision.BestAnswer, cost);
    }

    /// <summary>
    /// Content identity of the problem this submission answers: the scored
    /// record's own key (<see cref="ProblemKey.From"/>), derived by
    /// <see cref="Score"/> and never supplied by a caller, so it cannot name
    /// another problem. Never <see langword="null"/>: every record has a key
    /// (SPEC-stats-identity.md §2, amended 2026-09-27). The lifetime stats key
    /// on it (<see cref="ProblemStatsDocument"/>); <see cref="QuizScore"/>
    /// never reads it. One half of this submission's identity (see the type
    /// remarks).
    /// </summary>
    public ProblemKey ProblemKey { get; }

    /// <summary>
    /// The answer the user submitted. What it reads as at its decision — the
    /// fourth answer reads Too good or No double / Pass — is the decision's
    /// (<see cref="CubeDecision.ClaimOf"/>). The other half of this
    /// submission's identity (see the type remarks).
    /// </summary>
    public CubeAnswer Answer { get; }

    /// <summary>
    /// The decision's truth, <see cref="CubeDecisionData.BestAnswer"/>: the
    /// single representative that review and classification read. Never the
    /// test of a correct answer — at an equity tie an answer other than this
    /// one can cost nothing and be correct. It takes no part in equality.
    /// </summary>
    public CubeAnswer BestAnswer { get; }

    /// <summary>
    /// What <see cref="Answer"/> costs at its decision, in its two parts:
    /// <see cref="CubeDecision.CostOf"/>, exact as the producer derives it
    /// (SPEC-scoring §3). It takes no part in equality.
    /// </summary>
    public CubeAnswerCost Cost { get; }

    /// <summary>
    /// Whether the answer is correct: its whole cost,
    /// <see cref="CubeAnswerCost.Total"/>, counts as zero. Judged on the whole,
    /// not as the parts' verdicts combined, so it can be false while both
    /// parts are correct. Derived from <see cref="Cost"/> by the library's one
    /// verdict rule, never stored, so the two cannot disagree. It takes no part
    /// in equality.
    /// </summary>
    public bool IsCorrect => Correctness.IsCorrect(Cost.Total);

    /// <summary>
    /// Whether the answer's doubling part is correct: its
    /// <see cref="CubeAnswerCost.DoublingPart"/> counts as zero. The session's
    /// Double row and the lifetime doubling half count this verdict. Derived,
    /// like <see cref="IsCorrect"/>.
    /// </summary>
    public bool IsDoublingPartCorrect => Correctness.IsCorrect(Cost.DoublingPart);

    /// <summary>
    /// Whether the answer's take part is correct: its
    /// <see cref="CubeAnswerCost.TakePart"/> counts as zero. True for No
    /// double, whose implied take is never charged (its take part is 0). The
    /// lifetime take half counts this verdict for every answer; the session's
    /// Take row counts it only for an answer that commits to its response
    /// (<see cref="CubeAnswerExtensions.CommitsToResponse"/>). Derived, like
    /// <see cref="IsCorrect"/>.
    /// </summary>
    public bool IsTakePartCorrect => Correctness.IsCorrect(Cost.TakePart);

    /// <summary>
    /// Whether <paramref name="other"/> is the same submitted answer: the same
    /// problem (equal <see cref="ProblemKey"/>) and the same answer (equal
    /// <see cref="Answer"/>), and nothing else. <see cref="BestAnswer"/>,
    /// <see cref="Cost"/> and the verdicts take no part (see the type remarks).
    /// </summary>
    /// <param name="other">The submission to compare with, or <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="other"/> answers the same
    /// problem with the same answer; <see langword="false"/> otherwise,
    /// including when <paramref name="other"/> is <see langword="null"/>.
    /// </returns>
    public bool Equals(SubmittedCubeAnswer? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        return other is not null
            && ProblemKey == other.ProblemKey
            && Answer == other.Answer;
    }

    /// <inheritdoc cref="Equals(SubmittedCubeAnswer?)"/>
    public override bool Equals(object? obj) => Equals(obj as SubmittedCubeAnswer);

    /// <summary>
    /// Hash code consistent with <see cref="Equals(SubmittedCubeAnswer?)"/>:
    /// the problem and the answer (<see cref="ProblemKey"/> and
    /// <see cref="Answer"/>), and nothing else. Safe to hash — the type is
    /// immutable.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => HashCode.Combine(ProblemKey, Answer);

    /// <summary>
    /// Equality per <see cref="Equals(SubmittedCubeAnswer?)"/>; two
    /// <see langword="null"/> operands are equal.
    /// </summary>
    /// <param name="left">The first submission, or <see langword="null"/>.</param>
    /// <param name="right">The second submission, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if both are <see langword="null"/> or equal.</returns>
    public static bool operator ==(SubmittedCubeAnswer? left, SubmittedCubeAnswer? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Negation of <see cref="operator ==(SubmittedCubeAnswer?, SubmittedCubeAnswer?)"/>.</summary>
    /// <param name="left">The first submission, or <see langword="null"/>.</param>
    /// <param name="right">The second submission, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the operands differ or only one is <see langword="null"/>.</returns>
    public static bool operator !=(SubmittedCubeAnswer? left, SubmittedCubeAnswer? right) => !(left == right);
}
