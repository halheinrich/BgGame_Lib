namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// Cumulative quiz score split into three independently-tracked segments —
/// play decisions, double (cube-offer) decisions, and take (cube-response)
/// decisions — with a derived <see cref="Total"/> across all three. Immutable;
/// each <c>Plus</c> call returns a new <see cref="QuizScore"/>.
///
/// A cube position is scored as two independent decisions: the doubler's
/// offer choice and the taker's response choice. They accumulate into
/// <see cref="DoubleDecisions"/> and <see cref="TakeDecisions"/> separately,
/// so a consumer can report play / double / take accuracy on their own axes
/// while <see cref="Total"/> gives the overall figure. There is no combine
/// rule across a cube position's two halves — each contributes one submission
/// to its own segment.
/// </summary>
/// <param name="PlayDecisions">Running tally over submitted plays.</param>
/// <param name="DoubleDecisions">Running tally over submitted doubler (offer) decisions.</param>
/// <param name="TakeDecisions">Running tally over submitted taker (response) decisions.</param>
public sealed record QuizScore(
    ScoreSegment PlayDecisions,
    ScoreSegment DoubleDecisions,
    ScoreSegment TakeDecisions)
{
    /// <summary>The starting score: three empty segments.</summary>
    public static QuizScore Empty { get; } =
        new(ScoreSegment.Empty, ScoreSegment.Empty, ScoreSegment.Empty);

    /// <summary>
    /// The combined tally across all three segments. Derived on each access —
    /// never stored — so it cannot drift from its constituent segments.
    /// </summary>
    public ScoreSegment Total => PlayDecisions + DoubleDecisions + TakeDecisions;

    /// <summary>Return a new score with <paramref name="play"/> folded into the play segment.</summary>
    public QuizScore Plus(SubmittedPlay play)
    {
        ArgumentNullException.ThrowIfNull(play);
        return this with { PlayDecisions = PlayDecisions.Add(1, play.IsCorrect ? 1 : 0, play.EquityLoss) };
    }

    /// <summary>
    /// Return a new score with <paramref name="cube"/> folded in: one doubler
    /// decision into <see cref="DoubleDecisions"/> and one taker decision into
    /// <see cref="TakeDecisions"/>. A cube position contributes exactly one
    /// submission to each of the two cube segments.
    /// </summary>
    /// <remarks>
    /// The doubler half is scored <b>claim vs. claim</b> against the
    /// position's derived truth (<see cref="DecisionData.BestClaimPair"/>,
    /// carried on the submission), the taker half action vs. action —
    /// SPEC-scoring §3's ruled comparison, spelled once on
    /// <see cref="SubmittedCubeAction.DoublerCorrect"/> /
    /// <see cref="SubmittedCubeAction.TakerCorrect"/> and read here
    /// (halheinrich/backgammon#86). A wrong claim over the right board action
    /// therefore folds as one incorrect doubler decision carrying +0.000
    /// equity loss: the ruled "right action, wrong reason" verdict, with no
    /// partial-credit tier in between.
    /// </remarks>
    public QuizScore Plus(SubmittedCubeAction cube)
    {
        ArgumentNullException.ThrowIfNull(cube);
        return this with
        {
            DoubleDecisions = DoubleDecisions.Add(1, cube.DoublerCorrect ? 1 : 0, cube.DoublerEquityLoss),
            TakeDecisions = TakeDecisions.Add(1, cube.TakerCorrect ? 1 : 0, cube.TakerEquityLoss),
        };
    }
}
