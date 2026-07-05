namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// One cube decision submitted by a quiz user, scored against the position's
/// analysis. The cube analog of <see cref="SubmittedPlay"/>.
///
/// A cube position is two independent atomic decisions — the doubler's
/// offer choice and the taker's response choice — so this record carries a
/// per-half result for each: the equity loss vs. the best action on that half
/// and whether the user's action on that half was correct. The four result
/// fields are pre-computed consumer-side from the position's analysis (via
/// <c>DecisionData.DoublerActionError</c> / <c>TakerActionError</c> against
/// <c>BestDoublerAction</c> / <c>BestTakerAction</c>); this type is the carrier
/// that <see cref="QuizScore.Plus(SubmittedCubeAction)"/> folds in.
/// </summary>
/// <param name="DecisionId">Stable identity of the decision this submission answers (from BgDecisionData.Id).</param>
/// <param name="UserDecision">The doubler/taker action pair the user chose.</param>
/// <param name="DoublerEquityLoss">Equity loss of the user's doubler action vs. the best doubler action (0 if best).</param>
/// <param name="TakerEquityLoss">Equity loss of the user's taker action vs. the best taker action (0 if best).</param>
/// <param name="DoublerCorrect">True iff the user's doubler action matched the best doubler action.</param>
/// <param name="TakerCorrect">True iff the user's taker action matched the best taker action.</param>
public sealed record SubmittedCubeAction(
    DecisionId DecisionId,
    CubeDecisionPair UserDecision,
    double DoublerEquityLoss,
    double TakerEquityLoss,
    bool DoublerCorrect,
    bool TakerCorrect);
