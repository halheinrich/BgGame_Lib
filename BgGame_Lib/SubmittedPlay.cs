namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// One checker play submitted by a quiz user and scored under a
/// <see cref="PlayRanking"/>: the problem and the play, which are its
/// identity, and the candidate it matched, its error under that ranking, and
/// whether it is correct. The scored outcome of
/// <see cref="PlaySubmission.Score"/>, and built nowhere else.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scored in one place, so its facts cannot disagree.</b>
/// <see cref="PlaySubmission.Score"/> finds the candidate the play is (by
/// position, from the decision's own board) and reads that candidate's
/// error off the ranking (<see cref="CheckerPlayDecisionData.RankedBy"/>,
/// SPEC-scoring §2a) in one pass, and takes the problem's key and the board
/// the play leaves from the same record. There is no public constructor, and
/// no member can be reassigned, so no caller can pair a play with another
/// candidate's error, file it under another problem, or state a correctness
/// its error contradicts.
/// </para>
/// <para>
/// <b>Only a scored play is one of these.</b> A play matching no candidate,
/// and one the ranking does not score, are skips of record that fold nothing
/// (SPEC-scoring §2 and §2a); <see cref="PlaySubmission"/> names which, and
/// neither yields a <see cref="SubmittedPlay"/>. So <see cref="QuizScore"/>
/// and <see cref="ProblemStatsDocument"/> fold only what the ranking scored.
/// </para>
/// <para>
/// <b>Correctness is derived from the error, never carried.</b>
/// <see cref="IsCorrect"/> is the one statement of the verdict, so it cannot
/// disagree with <see cref="EquityLoss"/>.
/// </para>
/// <para>
/// <b>Identity: the same problem and the same play</b> (Hal's ruling of
/// 2026-09-27 on halheinrich/backgammon#273). Two submissions are equal
/// exactly when they answer the same problem, <see cref="ProblemKey"/>, with
/// the same play, <see cref="AfterBoard"/>. The key holds the problem's
/// whole board, so equal keys mean one starting position, and from one
/// position two plays are the same play exactly when they reach the same
/// position (the rule is stated on <see cref="BoardState.IsSamePlay"/>).
/// </para>
/// <para>
/// Everything else is a fact about this one submission, outside identity.
/// <see cref="MatchedCandidateIndex"/> is which row of this record matched,
/// and a content-equal record may list its candidates in another order.
/// <see cref="UserPlay"/> is the encoding the user gave, and a play has no
/// equality (see <see cref="Play"/>). <see cref="Ranking"/>,
/// <see cref="EquityLoss"/> and <see cref="IsCorrect"/> are the result under
/// one ranking of one analysis. So two equal submissions can differ in every
/// one of these, and their equality says nothing about how either scored.
/// </para>
/// </remarks>
public sealed class SubmittedPlay : IEquatable<SubmittedPlay>
{
    /// <summary>
    /// Assembles a scored submission from facts
    /// <see cref="PlaySubmission.Score"/> has already derived together. Not a
    /// scoring door: it checks nothing, and nothing but that one method calls
    /// it.
    /// </summary>
    internal SubmittedPlay(
        ProblemKey problemKey,
        BoardPosition afterBoard,
        Play userPlay,
        PlayRanking ranking,
        int matchedCandidateIndex,
        double equityLoss)
    {
        ProblemKey = problemKey;
        AfterBoard = afterBoard;
        UserPlay = userPlay;
        Ranking = ranking;
        MatchedCandidateIndex = matchedCandidateIndex;
        EquityLoss = equityLoss;
    }

    /// <summary>
    /// Content identity of the problem this submission answers: the scored
    /// record's own key (<see cref="ProblemKey.From"/>), derived by
    /// <see cref="PlaySubmission.Score"/> from the record it scores and never
    /// supplied by a caller, so it cannot name another problem. Never
    /// <see langword="null"/>: every record has a key (SPEC-stats-identity.md
    /// §2, amended 2026-09-27). The lifetime stats key on it
    /// (<see cref="ProblemStatsDocument"/>); <see cref="QuizScore"/> never
    /// reads it. One half of this submission's identity (see the type
    /// remarks).
    /// </summary>
    public ProblemKey ProblemKey { get; }

    /// <summary>
    /// The play's identity: the position it reaches from the problem's start,
    /// as the board it leaves — the matched candidate's after-board
    /// (<see cref="CheckerPlayDecision.AfterBoardOf"/>), which the user's play
    /// leaves too, being the same play. Taken from the scored record by
    /// <see cref="PlaySubmission.Score"/>, never computed here.
    /// <b>Frame: the next mover's</b>, as every after-board's: the opponent is
    /// on roll, so the decision-maker's checkers are negative. The flip is its
    /// own inverse, so equal after-boards are equal positions reached. The
    /// other half of this submission's identity (see the type remarks).
    /// </summary>
    public BoardPosition AfterBoard { get; }

    /// <summary>
    /// The play the user submitted, in the encoding the user gave it: the same
    /// play as the matched candidate's from the decision's position
    /// (<see cref="BoardState.IsSamePlay"/>), not necessarily the same
    /// encoding. It takes no part in equality.
    /// </summary>
    public Play UserPlay { get; }

    /// <summary>
    /// The ranking the play was scored under: the one that decided which play
    /// is best and so what <see cref="EquityLoss"/> is measured against
    /// (SPEC-scoring §2a).
    /// </summary>
    public PlayRanking Ranking { get; }

    /// <summary>
    /// Index into the decision's <see cref="CheckerPlayDecisionData.Plays"/>
    /// of the candidate the user's play is — the stored order, whatever the
    /// ranking's order (<see cref="RankedPlay.Index"/>). A fact about which
    /// row of that one record matched, not the play's identity: a
    /// content-equal record may list the same play at another index. It takes
    /// no part in equality.
    /// </summary>
    public int MatchedCandidateIndex { get; }

    /// <summary>
    /// The play's error under <see cref="Ranking"/>: the ranking's best play's
    /// equity minus the matched candidate's (<see cref="RankedPlay.Error"/>).
    /// Finite and never negative; 0 for the best play and any play tying it.
    /// </summary>
    public double EquityLoss { get; }

    /// <summary>
    /// Whether the play is correct: its error under <see cref="Ranking"/> is
    /// exactly 0 (SPEC-scoring §2a). Derived from <see cref="EquityLoss"/>,
    /// never stored, so the two cannot disagree. This is the one statement of
    /// the checker-play verdict; the pending threshold ruling of
    /// halheinrich/backgammon#202 changes this predicate and nothing else.
    /// </summary>
    public bool IsCorrect => EquityLoss == 0.0;

    /// <summary>
    /// Whether <paramref name="other"/> is the same submitted play: the same
    /// problem (equal <see cref="ProblemKey"/>) and the same play (equal
    /// <see cref="AfterBoard"/>), and nothing else.
    /// <see cref="MatchedCandidateIndex"/>, <see cref="UserPlay"/>,
    /// <see cref="Ranking"/>, <see cref="EquityLoss"/> and
    /// <see cref="IsCorrect"/> take no part (see the type remarks).
    /// </summary>
    /// <param name="other">The submission to compare with, or <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="other"/> answers the same
    /// problem with the same play; <see langword="false"/> otherwise,
    /// including when <paramref name="other"/> is <see langword="null"/>.
    /// </returns>
    public bool Equals(SubmittedPlay? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        return other is not null
            && ProblemKey == other.ProblemKey
            && AfterBoard == other.AfterBoard;
    }

    /// <inheritdoc cref="Equals(SubmittedPlay?)"/>
    public override bool Equals(object? obj) => Equals(obj as SubmittedPlay);

    /// <summary>
    /// Hash code consistent with <see cref="Equals(SubmittedPlay?)"/>: the
    /// problem and the play (<see cref="ProblemKey"/> and
    /// <see cref="AfterBoard"/>), and nothing else, so submissions that are
    /// the same play hash alike whatever their other facts. Safe to hash —
    /// the type is immutable.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => HashCode.Combine(ProblemKey, AfterBoard);

    /// <summary>
    /// Equality per <see cref="Equals(SubmittedPlay?)"/>; two
    /// <see langword="null"/> operands are equal.
    /// </summary>
    /// <param name="left">The first submission, or <see langword="null"/>.</param>
    /// <param name="right">The second submission, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if both are <see langword="null"/> or equal.</returns>
    public static bool operator ==(SubmittedPlay? left, SubmittedPlay? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Negation of <see cref="operator ==(SubmittedPlay?, SubmittedPlay?)"/>.</summary>
    /// <param name="left">The first submission, or <see langword="null"/>.</param>
    /// <param name="right">The second submission, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the operands differ or only one is <see langword="null"/>.</returns>
    public static bool operator !=(SubmittedPlay? left, SubmittedPlay? right) => !(left == right);
}
