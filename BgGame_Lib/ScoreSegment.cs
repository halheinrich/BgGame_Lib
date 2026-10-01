namespace BgGame_Lib;

/// <summary>
/// A running tally over one category of scored decisions — how many were
/// submitted, how many were correct, and the cumulative equity loss across
/// them. Immutable; <see cref="Add"/> and <c>operator +</c> each return a new
/// <see cref="ScoreSegment"/>.
///
/// This is the single accumulation primitive behind <see cref="QuizScore"/>
/// (each of its rows, Play, Double and Take, and its Total is a
/// <see cref="ScoreSegment"/>) and behind <see cref="ProblemStats"/>'s
/// lifetime tally.
/// </summary>
/// <param name="Submitted">Number of decisions folded into this segment.</param>
/// <param name="Correct">
/// Number of those decisions scored correct, by each submission's own verdict:
/// <see cref="SubmittedPlay.IsCorrect"/> for a play; for a cube answer, the
/// verdict of the part or the whole that the segment counts
/// (<see cref="SubmittedCubeAnswer.IsDoublingPartCorrect"/>,
/// <see cref="SubmittedCubeAnswer.IsTakePartCorrect"/>,
/// <see cref="SubmittedCubeAnswer.IsCorrect"/>).
/// </param>
/// <param name="TotalEquityLoss">Sum of equity loss across the folded decisions.</param>
public sealed record ScoreSegment(int Submitted, int Correct, double TotalEquityLoss)
{
    /// <summary>The empty segment: 0 / 0 / 0.0.</summary>
    public static ScoreSegment Empty { get; } = new(0, 0, 0.0);

    /// <summary>Mean equity loss across submissions; 0 when none have been submitted.</summary>
    public double AverageEquityLoss => Submitted > 0 ? TotalEquityLoss / Submitted : 0.0;

    /// <summary>
    /// Fraction of submitted decisions that were correct, in the range [0, 1];
    /// 0 when none have been submitted. The percentage rendering (×100, "0.00"
    /// formatting) is a display concern and lives in the consumer.
    /// </summary>
    public double Accuracy => Submitted > 0 ? (double)Correct / Submitted : 0.0;

    /// <summary>
    /// Return a new segment with one batch of results folded in. Equivalent to
    /// adding <c>new ScoreSegment(submitted, correct, loss)</c> via
    /// <c>operator +</c>; the operator is the single definition of how segments
    /// combine.
    /// </summary>
    /// <param name="submitted">Decisions to add to <see cref="Submitted"/>.</param>
    /// <param name="correct">Correct decisions to add to <see cref="Correct"/>.</param>
    /// <param name="loss">Equity loss to add to <see cref="TotalEquityLoss"/>.</param>
    public ScoreSegment Add(int submitted, int correct, double loss) =>
        this + new ScoreSegment(submitted, correct, loss);

    /// <summary>Combine two segments component-wise.</summary>
    public static ScoreSegment operator +(ScoreSegment a, ScoreSegment b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return new ScoreSegment(
            a.Submitted + b.Submitted,
            a.Correct + b.Correct,
            a.TotalEquityLoss + b.TotalEquityLoss);
    }
}
