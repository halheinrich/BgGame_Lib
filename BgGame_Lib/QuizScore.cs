namespace BgGame_Lib;

/// <summary>
/// Cumulative running total across submitted plays. Immutable — each
/// <see cref="Plus"/> call returns a new <see cref="QuizScore"/>.
///
/// Holding the running total separately from the per-problem history lets
/// consumers either keep a list of <see cref="SubmittedPlay"/> alongside or
/// just keep the score, depending on whether they need replay of individual
/// problems.
/// </summary>
/// <param name="Submitted">Number of plays submitted so far.</param>
/// <param name="Correct">Number of submitted plays matching the best candidate.</param>
/// <param name="TotalEquityLoss">Sum of equity loss across all submissions.</param>
public sealed record QuizScore(int Submitted, int Correct, double TotalEquityLoss)
{
    /// <summary>The starting score: 0 / 0 / 0.0.</summary>
    public static QuizScore Empty { get; } = new(0, 0, 0.0);

    /// <summary>Mean equity loss across submissions; 0 when no plays have been submitted.</summary>
    public double AverageEquityLoss => Submitted > 0 ? TotalEquityLoss / Submitted : 0.0;

    /// <summary>Return a new score with <paramref name="play"/> folded in.</summary>
    public QuizScore Plus(SubmittedPlay play)
    {
        ArgumentNullException.ThrowIfNull(play);
        return new QuizScore(
            Submitted + 1,
            Correct + (play.IsCorrect ? 1 : 0),
            TotalEquityLoss + play.EquityLoss);
    }
}
