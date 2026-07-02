namespace BgGame_Lib;

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
    /// immediately after either side reaches <see cref="MatchLength"/> − 1,
    /// during which an otherwise-live cube is suspended. An event-triggered
    /// state: it only ever becomes true via the <see cref="AwardGame"/>
    /// transition (in a match of length ≥ 2). Always false in money games and
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
        if (isCrawford && matchLength == 0)
            throw new ArgumentException("Crawford does not apply in money games.", nameof(isCrawford));
        if (isCrawford && matchLength == 1)
            throw new ArgumentException(
                "Crawford does not apply in a 1-point match — it has no cube to suspend.",
                nameof(isCrawford));

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
    /// Crawford flag for the next game.
    ///
    /// Crawford transitions:
    ///   • Pre-Crawford game ending with a side reaching MatchLength − 1 → next game is Crawford.
    ///   • Crawford game ending → next game is post-Crawford (IsCrawford = false).
    ///   • Match-ending game → IsCrawford cleared, <see cref="IsMatchOver"/> becomes true.
    ///
    /// Money games (MatchLength = 0): scores accumulate; IsCrawford stays false.
    /// </summary>
    public void AwardGame(GameResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.OnRollWon) OnRollScore += result.Points;
        else OpponentScore += result.Points;

        if (MatchLength <= 0) return;

        bool justPlayedCrawford = IsCrawford;

        if (OnRollScore >= MatchLength || OpponentScore >= MatchLength)
        {
            IsCrawford = false;
            return;
        }

        if (justPlayedCrawford)
        {
            IsCrawford = false;
            return;
        }

        int leaderScore = Math.Max(OnRollScore, OpponentScore);
        IsCrawford = (leaderScore == MatchLength - 1);
    }

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
