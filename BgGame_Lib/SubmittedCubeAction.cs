namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// One cube decision submitted by a quiz user, scored against the position's
/// analysis. The cube analog of <see cref="SubmittedPlay"/>.
///
/// A cube position is two independent decisions — the doubler's three-valued
/// <em>claim</em> and the taker's response-if-doubled — so this record carries
/// the user's answer and the analysis' derived truth as two
/// <see cref="CubeClaimPair"/>s, plus the equity loss on each half. Per-half
/// correctness is <b>derived</b> from that pair of pairs
/// (<see cref="DoublerCorrect"/> / <see cref="TakerCorrect"/>) rather than
/// carried, and <see cref="QuizScore.Plus(SubmittedCubeAction)"/> /
/// <see cref="ProblemStats.Plus(SubmittedCubeAction, DateTimeOffset)"/> fold
/// those derivations.
/// </summary>
/// <remarks>
/// <para>
/// <b>The doubler half is scored claim vs. claim</b> (SPEC-scoring §3, ruled
/// 2026-08-26; halheinrich/backgammon#86), not action vs. action: since
/// <see cref="CubeClaim.NoDouble"/> and <see cref="CubeClaim.TooGood"/>
/// collapse to the identical board action, an action-level comparison would
/// score a no-double answer on a too-good position as fully correct — the
/// exact mistake the claim layer exists to catch. The taker half compares as
/// it always has, <see cref="CubeAction.Take"/> against
/// <see cref="CubeAction.Pass"/>, and stays independent of the doubler half.
/// </para>
/// <para>
/// A wrong claim over the right action is therefore <b>incorrect at
/// +0.000</b>: the claim halves differ, so <see cref="DoublerCorrect"/> is
/// false, while <see cref="DoublerEquityLoss"/> — an equity fact about the
/// board action — is zero. That "right action, wrong reason" verdict is
/// ruled, not incidental; no partial-credit tier exists in
/// <see cref="ScoreSegment"/> and none is added. The incoherent
/// <see cref="CubeClaimPair.NoDoublePass"/> answer is submittable like any
/// other and scored by the same rule: wrong on whichever halves differ, and
/// fully correct on the measure-zero boundary where the ruled tie-breaks
/// compose it as <see cref="DecisionData.BestClaimPair"/>.
/// </para>
/// <para>
/// <b>Why the truth pair is carried and the correctness is not.</b> The truth
/// is a producer fact — <see cref="DecisionData.BestClaimPair"/>, the
/// ecosystem's one derivation site — so it enters from outside; nothing here
/// re-derives a claim from equities. Correctness, by contrast, is a pure
/// function of the two pairs this record already holds, so deriving it is
/// what makes a stated result unable to disagree with the answer it
/// describes. The equity losses stay carried for the same reason the truth
/// pair does: they are facts about the analysis' equities, which this carrier
/// deliberately does not hold. <see cref="From"/> is the one construction
/// that reads all three off a <see cref="DecisionData"/> together, and is how
/// a consumer should build one.
/// </para>
/// </remarks>
/// <param name="ProblemKey">
/// Content identity of the problem this submission answers, derived
/// producer-side via <see cref="ProblemKey.TryDerive"/> from the quizzed
/// record's facts. <see langword="null"/> is the no-key rung
/// (SPEC-stats-identity.md §2): the submission still scores the session
/// (<see cref="QuizScore"/> never reads the key) but is not recorded in
/// lifetime stats — <see cref="ProblemStatsDocument.Plus(SubmittedCubeAction, TimeProvider)"/>
/// skips it.
/// </param>
/// <param name="UserDecision">The claim/taker answer pair the user chose.</param>
/// <param name="BestDecision">
/// The position's derived truth — <see cref="DecisionData.BestClaimPair"/>,
/// taken from the producer rather than re-derived here.
/// </param>
/// <param name="DoublerEquityLoss">
/// Equity loss of the board action behind the user's claim vs. the best
/// doubler action (0 if best) — <see cref="DecisionData.DoublerActionError"/>
/// of <see cref="CubeClaimExtensions.ToCubeAction"/>. Zero on a wrong claim
/// over the right action.
/// </param>
/// <param name="TakerEquityLoss">
/// Equity loss of the user's taker action vs. the best taker action (0 if
/// best) — <see cref="DecisionData.TakerActionError"/>.
/// </param>
public sealed record SubmittedCubeAction(
    ProblemKey? ProblemKey,
    CubeClaimPair UserDecision,
    CubeClaimPair BestDecision,
    double DoublerEquityLoss,
    double TakerEquityLoss)
{
    /// <summary>
    /// True iff the user's claim matched the position's derived truth claim —
    /// the ruled claim-vs-claim comparison, so a no-double answer on a
    /// too-good position reads false. Derived from
    /// <see cref="UserDecision"/> and <see cref="BestDecision"/>; never
    /// stored, so it cannot drift from them.
    /// </summary>
    public bool DoublerCorrect => UserDecision.Claim == BestDecision.Claim;

    /// <summary>
    /// True iff the user's taker action matched the best taker action.
    /// Independent of <see cref="DoublerCorrect"/> — the two halves are
    /// scored, and tallied, separately. Derived, like its doubler sibling.
    /// </summary>
    public bool TakerCorrect => UserDecision.Taker == BestDecision.Taker;

    /// <summary>
    /// Build the submission for <paramref name="answer"/> against
    /// <paramref name="decision"/>'s analysis: the truth pair and both equity
    /// losses are read off the producer here, in one place, so a caller
    /// cannot pair an answer with a truth or a loss that belongs to some
    /// other decision.
    /// </summary>
    /// <param name="problemKey">
    /// Content identity of the quizzed problem, or <see langword="null"/> for
    /// the no-key rung.
    /// </param>
    /// <param name="answer">The claim/taker pair the user submitted.</param>
    /// <param name="decision">The analysed cube decision answered.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="decision"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="decision"/> is a checker play
    /// (<see cref="DecisionData.IsCube"/> false) — the cube surfaces it reads
    /// are undefined there.
    /// </exception>
    public static SubmittedCubeAction From(
        ProblemKey? problemKey, CubeClaimPair answer, DecisionData decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return new(
            problemKey,
            answer,
            decision.BestClaimPair,
            decision.DoublerActionError(answer.Claim.ToCubeAction()),
            decision.TakerActionError(answer.Taker));
    }
}
