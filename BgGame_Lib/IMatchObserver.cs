namespace BgGame_Lib;

/// <summary>
/// Live per-move view of one <see cref="MatchRunner.RunMatchAsync"/> run,
/// passed per match as an optional argument. The vocabulary is the transcript
/// itself: <see cref="OnEntryRecorded"/> delivers the same
/// <see cref="TranscriptEntry"/> instances the <see cref="Transcript"/>
/// records (single source of truth — there is no parallel event family),
/// bracketed by minimal lifecycle events.
///
/// <para><b>Delivery sequence.</b> Per match:
/// <c>OnGameStarted(1) → OnEntryRecorded… → OnGameEnded(1, …) →
/// OnGameStarted(2) → … → OnMatchEnded</c>. Every transcript entry is
/// delivered, including the terminating <see cref="GameEndedTranscriptEntry"/>;
/// <see cref="OnGameEnded"/> follows it with the completed, seat-attributed
/// <see cref="GameRecord"/>. The delivered <see cref="GameRecord"/>s and the
/// <see cref="MatchResult"/> are reference-identical to what
/// <see cref="MatchRunner.RunMatchAsync"/> returns.</para>
///
/// <para><b>Threading and pacing.</b> Callbacks are synchronous, serialized
/// (never concurrent), and invoked inline on the runner's loop between agent
/// queries — an observer must return promptly, because a spectator must never
/// slow a match. Consumers that fan out (UI push, network) should enqueue to
/// a channel and drain elsewhere.</para>
///
/// <para><b>Failure semantics.</b> An exception thrown by any callback
/// propagates out of <see cref="MatchRunner.RunMatchAsync"/> unwrapped — the
/// runner adds no policy; observers intended to be non-disruptive must be
/// non-throwing by construction. When the run itself aborts (an
/// <see cref="AgentContractViolationException"/> or cancellation), the stream
/// simply stops: <see cref="OnMatchEnded"/> fires only for a completed run,
/// and there is no terminal abort callback — the observer's owner is the same
/// caller that catches the throw. Everything up to the failure point has
/// already been delivered: completed games via <see cref="OnGameEnded"/>, the
/// broken game's partial entries via <see cref="OnEntryRecorded"/>.</para>
/// </summary>
public interface IMatchObserver
{
    /// <summary>
    /// A game is beginning. Fires before the opening roll, so no board state is
    /// carried — the frame is indeterminate until the opening roll names the
    /// first mover, whose <see cref="PlayTranscriptEntry"/> is the game's first
    /// entry and carries the starting position. The frame-free facts that
    /// <em>are</em> settled at game construction — the seats' entering scores
    /// and the Crawford flag — travel in <paramref name="context"/>, so a
    /// consumer folds them without re-deriving them from completed games.
    /// </summary>
    /// <param name="context">Seat-absolute entering scores, the Crawford flag, and the 1-based game number.</param>
    void OnGameStarted(GameStartContext context);

    /// <summary>
    /// An entry was appended to the current game's <see cref="Transcript"/> —
    /// the same instance the transcript holds, delivered immediately after
    /// the append.
    /// </summary>
    /// <param name="entry">The appended entry.</param>
    void OnEntryRecorded(TranscriptEntry entry);

    /// <summary>
    /// A game completed. Fires after the game's
    /// <see cref="GameEndedTranscriptEntry"/> was delivered and the game was
    /// awarded to the match score.
    /// </summary>
    /// <param name="gameNumber">1-based game number within the match.</param>
    /// <param name="record">The completed game — the same instance <see cref="MatchResult.Games"/> will hold.</param>
    void OnGameEnded(int gameNumber, GameRecord record);

    /// <summary>
    /// The run completed. Fires once, last, with the same instance
    /// <see cref="MatchRunner.RunMatchAsync"/> returns. Does not fire when
    /// the run aborts by exception.
    /// </summary>
    /// <param name="result">The finished match.</param>
    void OnMatchEnded(MatchResult result);
}
