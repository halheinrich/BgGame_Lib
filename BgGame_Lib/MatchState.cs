namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// Mutable match-level state. Persists across the games of a match.
///
/// All score-related fields are on-roll-relative — they refer to the player
/// currently on roll in the active game, not to fixed "Player 1" / "Player 2"
/// identities. When perspective swaps (between turns), the on-roll and
/// opponent score labels swap with it.
///
/// Lifetime: a single <see cref="MatchState"/> instance lives for the duration
/// of a match. Each game within the match is represented by a fresh
/// <see cref="GameState"/> aggregating this <see cref="MatchState"/>.
/// </summary>
public sealed class MatchState
{
    /// <summary>Match length in points. 0 = money game (Phase 2+; no Crawford / no match end).</summary>
    public int MatchLength { get; init; }

    /// <summary>Points scored by the player currently on roll.</summary>
    public int OnRollScore { get; private set; }

    /// <summary>Points scored by the opponent of the player currently on roll.</summary>
    public int OpponentScore { get; private set; }

    /// <summary>
    /// True iff the current game is the Crawford game — the one game
    /// immediately after either side first reaches <see cref="MatchLength"/> − 1,
    /// during which an otherwise-live cube is suspended. Played once per
    /// match: every later game has the cube, double match point included,
    /// though the leader is still one-away. An event-triggered state: in play
    /// it only ever becomes true via the <see cref="AwardGame"/> transition
    /// (in a match of length ≥ 2); <see cref="FromScores"/> states it for a
    /// resumed game. Always false in money games and
    /// in 1-point matches — a 1-point match has no cube to suspend, so no
    /// Crawford game exists (see <see cref="HasCube"/>).
    /// </summary>
    public bool IsCrawford { get; private set; }

    /// <summary>
    /// True iff the doubling cube is in play for this match at all. False only
    /// for a 1-point match: with a single point at stake there is nothing to
    /// double for — the cube, and with it the Crawford/post-Crawford
    /// distinction, simply never enters play. True for money games and all
    /// matches of length ≥ 2.
    ///
    /// Model note: some external match-record conventions label the games of a
    /// 1-point match "Crawford". This library deliberately does not — Crawford
    /// is a mid-match suspension of a live cube, and a cube that never existed
    /// cannot be suspended. Cube availability (this property) and the Crawford
    /// suspension (<see cref="IsCrawford"/>) are distinct predicates;
    /// <see cref="GameState.CanDouble"/> composes them.
    /// </summary>
    public bool HasCube => MatchLength != 1;

    private MatchState() { }

    /// <summary>
    /// Construct a fresh match: scores 0–0, not Crawford.
    /// </summary>
    /// <param name="matchLength">Match length in points (≥ 1). 0 = money game.</param>
    public static MatchState NewMatch(int matchLength)
    {
        if (matchLength < 0)
            throw new ArgumentOutOfRangeException(
                nameof(matchLength), matchLength, "Match length must be ≥ 0 (0 = money game).");
        return new MatchState { MatchLength = matchLength };
    }

    /// <summary>
    /// Construct from explicit scores — for resume / fixture / mid-match scenarios.
    ///
    /// <para>
    /// <paramref name="isCrawford"/> must be consistent with the model:
    /// Crawford applies only in matches of length ≥ 2 (a money game has no
    /// match score to be one-away from; a 1-point match has no cube to
    /// suspend — see <see cref="HasCube"/>). External data conventions that
    /// mark 1-point-match games "Crawford" must be normalized by the caller
    /// before constructing; this method fails fast on the inconsistent
    /// combination rather than silently reinterpreting it.
    /// </para>
    ///
    /// <para>
    /// The scores must also form a valid Crawford position: exactly one side
    /// is one-away from the match — its score equals
    /// <paramref name="matchLength"/> − 1 while the other is strictly below it.
    /// The Crawford game is the single game right after the leader first reaches
    /// that score, so double match point (both sides one-away) is provably
    /// post-Crawford and neither-side-one-away is pre-Crawford; both are
    /// rejected. The rule is BgDataTypes_Lib's Crawford standing, asked through
    /// <see cref="MatchStanding.TryCreate"/>, and the refusal is its sentence;
    /// it is also the rule that refuses Crawford in a 1-point match, whose
    /// sides start one-away each.
    /// </para>
    ///
    /// <para>
    /// The flag clear with the leader one-away is a post-Crawford state: the
    /// Crawford game has been played, and no later game is Crawford
    /// (<see cref="AwardGame"/>). A caller resuming before the Crawford game
    /// is played passes <paramref name="isCrawford"/> true.
    /// </para>
    ///
    /// <para>
    /// In match play (<paramref name="matchLength"/> &gt; 0) each score must be
    /// strictly below the match length — a score at or over the length is a
    /// finished match, not a resumable mid-match state. Money games
    /// (<paramref name="matchLength"/> 0) have no such bound.
    /// </para>
    /// </summary>
    public static MatchState FromScores(
        int matchLength, int onRollScore, int opponentScore, bool isCrawford)
    {
        if (matchLength < 0)
            throw new ArgumentOutOfRangeException(nameof(matchLength), matchLength, "Match length must be ≥ 0.");
        if (onRollScore < 0)
            throw new ArgumentOutOfRangeException(nameof(onRollScore), onRollScore, "Score must be ≥ 0.");
        if (opponentScore < 0)
            throw new ArgumentOutOfRangeException(nameof(opponentScore), opponentScore, "Score must be ≥ 0.");
        if (matchLength > 0 && onRollScore >= matchLength)
            throw new ArgumentOutOfRangeException(
                nameof(onRollScore), onRollScore,
                "Score must be below the match length — a score at or over it is a finished match, not a resumable state.");
        if (matchLength > 0 && opponentScore >= matchLength)
            throw new ArgumentOutOfRangeException(
                nameof(opponentScore), opponentScore,
                "Score must be below the match length — a score at or over it is a finished match, not a resumable state.");
        // A money game has no match standing, so its refusal is this library's.
        if (isCrawford && matchLength == 0)
            throw new ArgumentException("Crawford does not apply in money games.", nameof(isCrawford));
        // In match play the Crawford standing (exactly one side 1-away) is BgDataTypes_Lib's
        // rule; a 1-point match, 1-away each, breaks it too. The range guards above leave
        // each away score at least 1, so the Crawford standing is the one rule the owner can
        // refuse here. The rule is symmetric; the on-roll side goes in as player 1.
        if (matchLength > 0
            && !MatchStanding.TryCreate(
                matchLength - onRollScore, matchLength - opponentScore, isCrawford, out _, out string? refusal))
            throw new ArgumentException(refusal, nameof(isCrawford));

        return new MatchState
        {
            MatchLength = matchLength,
            OnRollScore = onRollScore,
            OpponentScore = opponentScore,
            IsCrawford = isCrawford,
        };
    }

    /// <summary>
    /// True iff either side has reached <see cref="MatchLength"/>. Always false in money games.
    /// </summary>
    public bool IsMatchOver =>
        MatchLength > 0 && (OnRollScore >= MatchLength || OpponentScore >= MatchLength);

    /// <summary>
    /// Apply the result of a completed game: credit the winner, transition the
    /// Crawford flag for the next game. The flag follows from the phase the
    /// match was in when the game was played and from the new scores, never
    /// from the leader's score alone — the leader stays one-away after the
    /// Crawford game until the match ends.
    ///
    /// Crawford transitions:
    ///   • Pre-Crawford game ending with a side reaching MatchLength − 1 → next game is Crawford.
    ///   • Crawford game ending → next game is post-Crawford (IsCrawford = false).
    ///   • Post-Crawford game ending → next game is post-Crawford, double match
    ///     point included (IsCrawford = false).
    ///   • Match-ending game → IsCrawford cleared, <see cref="IsMatchOver"/> becomes true.
    ///
    /// Money games (MatchLength = 0): scores accumulate; IsCrawford stays false.
    /// </summary>
    public void AwardGame(GameResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        CrawfordPhase playedIn = Phase;

        if (result.OnRollWon) OnRollScore += result.Points;
        else OpponentScore += result.Points;

        if (MatchLength <= 0) return;

        // One game credits one side, so from pre-Crawford at most the winner
        // reaches one-away: the next game is Crawford exactly when it did and
        // did not also win the match.
        IsCrawford = playedIn == CrawfordPhase.PreCrawford
            && !IsMatchOver
            && LeaderScore == MatchLength - 1;
    }

    /// <summary>The higher of the two scores, whichever label it is under.</summary>
    private int LeaderScore => Math.Max(OnRollScore, OpponentScore);

    /// <summary>
    /// Where a match of length ≥ 1 stands relative to its Crawford game.
    /// Private: <see cref="IsCrawford"/> is the public reading, and the other
    /// two phases follow from it and the scores.
    /// </summary>
    private enum CrawfordPhase
    {
        /// <summary>Neither side has reached <see cref="MatchLength"/> − 1.</summary>
        PreCrawford,

        /// <summary>The current game is the Crawford game.</summary>
        Crawford,

        /// <summary>
        /// The Crawford game has been played: the leader is one-away and every
        /// later game has the cube. A 1-point match starts here — both sides
        /// one-away — and so never enters Crawford.
        /// </summary>
        PostCrawford,
    }

    /// <summary>
    /// The match's phase, derived from what the state holds and never stored:
    /// scores never fall, so the flag set is the Crawford game, a leader below
    /// one-away is pre-Crawford, and a leader at one-away with the flag clear
    /// is post-Crawford (a leader past it has won, and counts as post-Crawford
    /// too, so nothing re-enters Crawford). Read by <see cref="AwardGame"/>
    /// before it credits the game. A money game has no Crawford game: its
    /// phase is read there but never acted on.
    /// </summary>
    private CrawfordPhase Phase =>
        IsCrawford ? CrawfordPhase.Crawford
        : LeaderScore >= MatchLength - 1 ? CrawfordPhase.PostCrawford
        : CrawfordPhase.PreCrawford;

    /// <summary>
    /// Swap on-roll / opponent score labels. Internal — invoked by
    /// <see cref="Referee"/> as part of perspective flipping after a play applies.
    /// IsCrawford and MatchLength do not flip.
    /// </summary>
    internal void SwapPerspective()
    {
        (OnRollScore, OpponentScore) = (OpponentScore, OnRollScore);
    }

    /// <summary>Take an immutable snapshot of the current match-level state.</summary>
    public MatchSnapshot Snapshot() =>
        new(MatchLength, OnRollScore, OpponentScore, IsCrawford);
}
