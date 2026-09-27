namespace BgGame_Lib;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using BgDataTypes_Lib;

/// <summary>The case of a <see cref="PlaySubmission"/>: whether the play scored, and if not, why.</summary>
public enum PlaySubmissionKind
{
    /// <summary>
    /// The play is a candidate the ranking scores: a <see cref="SubmittedPlay"/>,
    /// which folds into the session score and the lifetime stats.
    /// </summary>
    Scored = 1,

    /// <summary>
    /// The play is a candidate the ranking does not score (under
    /// <see cref="PlayRanking.DepthFirst"/> only): treated as an off-list
    /// play, a skip of record that folds nothing (SPEC-scoring §2a).
    /// </summary>
    NotScored = 2,

    /// <summary>
    /// The play is no candidate of the decision: an off-list play, a skip of
    /// record that folds nothing (SPEC-scoring §2).
    /// </summary>
    OffList = 3,
}

/// <summary>
/// What a checker play submitted against a decision comes to under a
/// <see cref="PlayRanking"/>: a scored <see cref="SubmittedPlay"/>, or one of
/// the two skips of record, named by <see cref="Kind"/>.
/// <see cref="Score"/> is the one place a submitted play is scored.
/// </summary>
/// <remarks>
/// <para>
/// <b>One place, one derivation.</b> <see cref="Score"/> finds the candidate
/// the play is and reads that candidate's error off the ranking
/// (<see cref="CheckerPlayDecisionData.RankedBy"/>, SPEC-scoring §2a)
/// together, and takes the problem's key from the same record
/// (<see cref="ProblemKey.From"/>), so the problem, the matched candidate,
/// the error and the correctness of a scored submission all come from the
/// producer's derivations and never from a caller.
/// </para>
/// <para>
/// <b>Two cases yield no scored submission</b>, and both are skips of record
/// that fold nothing (SPEC-scoring §2): a play that is no candidate
/// (<see cref="PlaySubmissionKind.OffList"/>), and a candidate the ranking does
/// not score (<see cref="PlaySubmissionKind.NotScored"/>), which has no error.
/// A caller tells them apart by <see cref="Kind"/>, and <see cref="Match{TResult}"/>
/// hands each case exactly what it has.
/// </para>
/// <para>
/// Immutable, with value equality: the same case, and for a scored play the
/// same <see cref="SubmittedPlay"/> (its own equality), for a not-scored one
/// the same candidate.
/// </para>
/// </remarks>
public sealed class PlaySubmission : IEquatable<PlaySubmission>
{
    /// <summary>The one off-list outcome: the case carries nothing.</summary>
    private static readonly PlaySubmission OffListOutcome = new(PlaySubmissionKind.OffList, null, 0);

    // Set exactly for Scored.
    private readonly SubmittedPlay? _scored;

    // The matched candidate's index for NotScored; 0 in the other cases, which
    // read their candidate (if any) off the submission.
    private readonly int _notScoredCandidateIndex;

    private PlaySubmission(PlaySubmissionKind kind, SubmittedPlay? scored, int notScoredCandidateIndex)
    {
        Kind = kind;
        _scored = scored;
        _notScoredCandidateIndex = notScoredCandidateIndex;
    }

    /// <summary>Which case this is: scored, not scored, or off the list.</summary>
    public PlaySubmissionKind Kind { get; }

    /// <summary>
    /// Scores <paramref name="play"/> against <paramref name="decision"/> under
    /// <paramref name="ranking"/> — the one place a submitted play is scored.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The play is matched by identity from the decision's own position
    /// (<see cref="BoardState.IndexOfSamePlay"/>; the identity rule is stated
    /// once, on <see cref="BoardState.IsSamePlay"/>), so whatever encoding of
    /// a candidate's play the user gave finds that candidate, and a play
    /// invalid from the position matches nothing and is off the list.
    /// </para>
    /// <para>
    /// A matched candidate's error, and whether the ranking scores it, are the
    /// ranking's (<see cref="RankedPlays.ForCandidate"/>). A scored
    /// candidate yields a <see cref="SubmittedPlay"/> carrying the record's
    /// key, the candidate's index, its error and <paramref name="ranking"/>;
    /// a candidate the ranking does not score yields
    /// <see cref="PlaySubmissionKind.NotScored"/> with its index.
    /// </para>
    /// <para>
    /// The key is the record's own (<see cref="ProblemKey.From"/>), derived
    /// here rather than taken from a caller, so a submission cannot name
    /// another problem. Every record has one (SPEC-stats-identity.md §2,
    /// amended 2026-09-27), so there is no keyless case.
    /// </para>
    /// </remarks>
    /// <param name="play">The play the user submitted.</param>
    /// <param name="decision">
    /// The analysed checker-play decision answered: its position to match the
    /// play from, its candidates to rank, and its key.
    /// </param>
    /// <param name="ranking">The quiz's ranking, which decides the best play and every error.</param>
    /// <returns>The submission's outcome under <paramref name="ranking"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="decision"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ranking"/> is not a defined ranking.</exception>
    public static PlaySubmission Score(Play play, CheckerPlayDecision decision, PlayRanking ranking)
    {
        ArgumentNullException.ThrowIfNull(decision);

        // The ranking first, so an undefined one is refused whatever the play.
        RankedPlays ranked = decision.Decision.RankedBy(ranking);

        var candidates = decision.Decision.Plays;
        var candidatePlays = new Play[candidates.Count];
        for (int i = 0; i < candidatePlays.Length; i++)
            candidatePlays[i] = candidates[i].Play;

        int index = new BoardState(decision.Board).IndexOfSamePlay(play, candidatePlays);
        if (index < 0)
            return OffListOutcome;

        return ranked.ForCandidate(index).Error is double error
            ? new PlaySubmission(
                PlaySubmissionKind.Scored,
                new SubmittedPlay(ProblemKey.From(decision), play, ranking, index, error),
                notScoredCandidateIndex: 0)
            : new PlaySubmission(PlaySubmissionKind.NotScored, scored: null, index);
    }

    /// <summary>
    /// Yields the scored submission when there is one — exactly when
    /// <see cref="Kind"/> is <see cref="PlaySubmissionKind.Scored"/>.
    /// </summary>
    /// <param name="submitted">The scored submission, or <see langword="null"/> for a skip.</param>
    /// <returns><see langword="true"/> exactly for a scored play.</returns>
    public bool TryGetScored([NotNullWhen(true)] out SubmittedPlay? submitted)
    {
        submitted = _scored;
        return submitted is not null;
    }

    /// <summary>
    /// Hands the case to the one branch for it, with what that case has: the
    /// scored submission, the not-scored candidate's index into
    /// <see cref="CheckerPlayDecisionData.Plays"/>, or nothing for an
    /// off-list play.
    /// </summary>
    /// <typeparam name="TResult">What each branch returns.</typeparam>
    /// <param name="scored">The branch for <see cref="PlaySubmissionKind.Scored"/>.</param>
    /// <param name="notScored">The branch for <see cref="PlaySubmissionKind.NotScored"/>.</param>
    /// <param name="offList">The branch for <see cref="PlaySubmissionKind.OffList"/>.</param>
    /// <returns>The chosen branch's result.</returns>
    /// <exception cref="ArgumentNullException">A branch is <see langword="null"/>.</exception>
    public TResult Match<TResult>(
        Func<SubmittedPlay, TResult> scored, Func<int, TResult> notScored, Func<TResult> offList)
    {
        ArgumentNullException.ThrowIfNull(scored);
        ArgumentNullException.ThrowIfNull(notScored);
        ArgumentNullException.ThrowIfNull(offList);
        return Kind switch
        {
            PlaySubmissionKind.Scored => scored(_scored!),
            PlaySubmissionKind.NotScored => notScored(_notScoredCandidateIndex),
            PlaySubmissionKind.OffList => offList(),
            _ => throw new UnreachableException($"Unhandled PlaySubmissionKind value: {Kind}."),
        };
    }

    /// <summary>
    /// Whether <paramref name="other"/> is the same outcome: the same
    /// <see cref="Kind"/>, and for a scored play an equal
    /// <see cref="SubmittedPlay"/>, for a not-scored one the same candidate.
    /// </summary>
    /// <param name="other">The outcome to compare with, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> for the same outcome; <see langword="false"/> otherwise.</returns>
    public bool Equals(PlaySubmission? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        return other is not null
            && Kind == other.Kind
            && _notScoredCandidateIndex == other._notScoredCandidateIndex
            && _scored == other._scored;
    }

    /// <inheritdoc cref="Equals(PlaySubmission?)"/>
    public override bool Equals(object? obj) => Equals(obj as PlaySubmission);

    /// <summary>Hash code consistent with <see cref="Equals(PlaySubmission?)"/>.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => HashCode.Combine(Kind, _notScoredCandidateIndex, _scored);

    /// <summary>
    /// Equality per <see cref="Equals(PlaySubmission?)"/>; two
    /// <see langword="null"/> operands are equal.
    /// </summary>
    /// <param name="left">The first outcome, or <see langword="null"/>.</param>
    /// <param name="right">The second outcome, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if both are <see langword="null"/> or equal.</returns>
    public static bool operator ==(PlaySubmission? left, PlaySubmission? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Negation of <see cref="operator ==(PlaySubmission?, PlaySubmission?)"/>.</summary>
    /// <param name="left">The first outcome, or <see langword="null"/>.</param>
    /// <param name="right">The second outcome, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the operands differ or only one is <see langword="null"/>.</returns>
    public static bool operator !=(PlaySubmission? left, PlaySubmission? right) => !(left == right);
}
