namespace BgGame_Lib;

using BgDataTypes_Lib;
using BgMoveGen;

/// <summary>
/// Unattended match loop: drives two <see cref="MatchParticipant"/>s from
/// <see cref="MatchState.NewMatch"/> to completion, producing a
/// <see cref="MatchResult"/> with seat-keyed scores and a full per-game
/// <see cref="Transcript"/>.
///
/// <para><b>Division of labor.</b> The runner owns turn sequencing (opening
/// roll, pre-roll cube window, play application, game/match transitions) and
/// dice (via the <see cref="IDiceSource"/> it is constructed with — agents
/// never roll). Rule legality stays where it already lives: plays validate in
/// <see cref="GameState.ApplyPlay(Play, int, int)"/>, cube legality in
/// <see cref="GameState.CanDouble"/> / <see cref="GameState.DoubleCube"/>,
/// game end and cube responses in <see cref="Referee"/>. Nothing here is
/// transport-specific: a network server adapts remote engines onto
/// <see cref="IPlayAgent"/> / <see cref="ICubeAgent"/> and hands them to this
/// same loop.</para>
///
/// <para><b>Game protocol.</b> Each game opens with an opening roll —
/// <c>Die1</c> is seat One's die, <c>Die2</c> seat Two's; ties re-roll (tie
/// rolls are consumed from the source but not transcripted); the higher die
/// wins and that seat plays the winning pair as its first roll, with no cube
/// window before it. Every subsequent turn: pre-roll cube window (gated on
/// <see cref="GameState.CanDouble"/> — the window simply does not open in a
/// Crawford game or a cubeless 1-point match), then roll and play. A dance
/// turn — the sole legal play is the empty play — is applied automatically
/// without querying the agent (there is no decision to make; agents are
/// stateless by contract, so no agent can desync) and is transcripted as a
/// normal <see cref="PlayTranscriptEntry"/> with an empty play.</para>
///
/// <para><b>Transcripts</b> record decision moments: every play (snapshot of
/// the state the decision was made in, dice, chosen play), every actual
/// double offer and its response (declined windows — NoDouble — are implicit:
/// a play entry with no preceding cube entry), and a terminating
/// <see cref="GameEndedTranscriptEntry"/>. One transcript per game.</para>
///
/// <para><b>Perspective note for cube responders.</b> The live
/// <see cref="GameState"/> passed to
/// <see cref="ICubeAgent.ChooseResponseAsync"/> is in the <i>offerer's</i>
/// (on-roll) perspective — the responder evaluates a state in which it is
/// "Opponent". This is the substrate's single-perspective model; there is
/// deliberately no public flip surface.</para>
///
/// <para><b>Failure semantics.</b> An agent returning an out-of-contract
/// value aborts the run with <see cref="AgentContractViolationException"/>
/// (the substrate is left unmutated by the offending decision); forfeit
/// policy belongs to the tournament layer. Cancellation propagates as
/// <see cref="OperationCanceledException"/>: the token is passed to every
/// agent call and checked at each turn and game boundary. A misbehaving
/// <see cref="IDiceSource"/> (out-of-range dice) throws
/// <see cref="InvalidOperationException"/>.</para>
///
/// <para>Not thread-safe: one match at a time per runner (the dice source is
/// stateful). Run sequential matches on one runner to consume a single seeded
/// dice stream across them.</para>
/// </summary>
public sealed class MatchRunner
{
    private readonly IDiceSource _diceSource;
    private readonly Referee _referee = new();

    /// <summary>Create a runner rolling from <paramref name="diceSource"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="diceSource"/> is null.</exception>
    public MatchRunner(IDiceSource diceSource)
    {
        ArgumentNullException.ThrowIfNull(diceSource);
        _diceSource = diceSource;
    }

    /// <summary>
    /// Run games until a seat reaches <paramref name="matchLength"/> points, or
    /// until <paramref name="maxGames"/> games have completed, whichever comes
    /// first.
    /// </summary>
    /// <param name="seatOne">Participant in seat One (receives Die1 of each opening roll).</param>
    /// <param name="seatTwo">Participant in seat Two (receives Die2 of each opening roll).</param>
    /// <param name="matchLength">Match length in points (≥ 1), or 0 for a money session.</param>
    /// <param name="maxGames">
    /// Optional cap on games played. Required for a money session
    /// (<paramref name="matchLength"/> 0), which has no match end of its own;
    /// optional as a safety cap in match play. When the cap stops the run
    /// before a match winner exists, <see cref="MatchResult.Winner"/> is null.
    /// </param>
    /// <param name="cancellationToken">Cooperative cancellation, honored at every turn and game boundary and inside agent calls.</param>
    /// <exception cref="ArgumentNullException">A participant is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="matchLength"/> is negative, or <paramref name="maxGames"/> is non-null and &lt; 1.</exception>
    /// <exception cref="ArgumentException"><paramref name="matchLength"/> is 0 (money session) and no <paramref name="maxGames"/> cap was supplied.</exception>
    /// <exception cref="AgentContractViolationException">An agent returned an out-of-contract value.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public Task<MatchResult> RunMatchAsync(
        MatchParticipant seatOne,
        MatchParticipant seatTwo,
        int matchLength,
        int? maxGames = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seatOne);
        ArgumentNullException.ThrowIfNull(seatTwo);
        if (maxGames is int cap && cap < 1)
            throw new ArgumentOutOfRangeException(nameof(maxGames), cap, "maxGames must be ≥ 1 when supplied.");
        if (matchLength == 0 && maxGames is null)
            throw new ArgumentException(
                "A money session (matchLength 0) has no match end; supply maxGames to bound it.",
                nameof(maxGames));

        // MatchState.NewMatch validates matchLength ≥ 0 — eagerly, in this
        // non-async wrapper, so argument failures throw at the call site
        // rather than surfacing on the awaited task.
        var loop = new MatchLoop(
            _diceSource, _referee, seatOne, seatTwo,
            MatchState.NewMatch(matchLength), cancellationToken);
        return loop.RunAsync(maxGames);
    }

    /// <summary>
    /// The state of one running match: participants, the live
    /// <see cref="MatchState"/>, and the seat currently holding the on-roll
    /// perspective. Instantiated per <see cref="RunMatchAsync"/> call so the
    /// runner itself carries no per-match mutable state.
    /// </summary>
    private sealed class MatchLoop(
        IDiceSource dice,
        Referee referee,
        MatchParticipant seatOne,
        MatchParticipant seatTwo,
        MatchState match,
        CancellationToken cancellationToken)
    {
        /// <summary>
        /// The seat whose points/checkers the on-roll-relative substrate labels
        /// (Board positive counts, OnRollScore, CubeOwner.OnRoll) currently
        /// refer to. Flips on every ApplyPlay; realigned by each opening roll.
        /// The initial value is arbitrary — the first opening roll aligns it.
        /// </summary>
        private MatchSeat _onRollSeat = MatchSeat.One;

        public async Task<MatchResult> RunAsync(int? maxGames)
        {
            var games = new List<GameRecord>();
            while (!match.IsMatchOver && (maxGames is not int cap || games.Count < cap))
            {
                cancellationToken.ThrowIfCancellationRequested();
                games.Add(await PlayGameAsync().ConfigureAwait(false));
            }

            int seatOneScore = ScoreOf(MatchSeat.One);
            int seatTwoScore = ScoreOf(MatchSeat.Two);
            MatchSeat? winner = !match.IsMatchOver
                ? null
                : seatOneScore >= match.MatchLength ? MatchSeat.One : MatchSeat.Two;
            return new MatchResult(winner, seatOneScore, seatTwoScore, games);
        }

        private int ScoreOf(MatchSeat seat) =>
            seat == _onRollSeat ? match.OnRollScore : match.OpponentScore;

        private MatchParticipant ParticipantAt(MatchSeat seat) =>
            seat == MatchSeat.One ? seatOne : seatTwo;

        private async Task<GameRecord> PlayGameAsync()
        {
            var (die1, die2) = OpeningRoll();
            var game = GameState.NewGame(match);
            var transcript = new Transcript();

            // The opening-roll winner plays the roll immediately — no cube
            // window precedes a game's first turn.
            GameResult? result = await PlayTurnAsync(game, transcript, die1, die2).ConfigureAwait(false);

            while (result is null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                result = await CubeWindowAsync(game, transcript).ConfigureAwait(false);
                if (result is not null) break;

                var (d1, d2) = RollChecked();
                result = await PlayTurnAsync(game, transcript, d1, d2).ConfigureAwait(false);
            }

            // Attribute the win to an absolute seat before AwardGame (which
            // does not flip perspective, so _onRollSeat still matches `result`).
            MatchSeat winner = result.OnRollWon ? _onRollSeat : _onRollSeat.Other();
            transcript.Append(new GameEndedTranscriptEntry(game.Snapshot(), result));
            match.AwardGame(result);
            return new GameRecord(winner, result, transcript);
        }

        /// <summary>
        /// Roll for the opening: Die1 belongs to seat One, Die2 to seat Two;
        /// ties re-roll (both dice); the higher die wins. Aligns the match
        /// perspective to the winner, who plays the winning pair.
        /// </summary>
        private (int Die1, int Die2) OpeningRoll()
        {
            while (true)
            {
                var (die1, die2) = RollChecked();
                if (die1 == die2) continue;

                MatchSeat winner = die1 > die2 ? MatchSeat.One : MatchSeat.Two;
                if (winner != _onRollSeat)
                {
                    match.SwapPerspective();
                    _onRollSeat = winner;
                }
                return (die1, die2);
            }
        }

        /// <summary>
        /// The pre-roll cube window. Returns a <see cref="GameResult"/> when a
        /// double is passed (game over at the pre-double cube value), null when
        /// play continues (window closed, no double, or double taken).
        /// </summary>
        private async Task<GameResult?> CubeWindowAsync(GameState game, Transcript transcript)
        {
            if (!game.CanDouble) return null;

            CubeAction offer = await ParticipantAt(_onRollSeat).CubeAgent
                .ChooseOfferAsync(game, cancellationToken).ConfigureAwait(false);
            if (offer == CubeAction.NoDouble) return null;   // implicit in the transcript
            if (offer != CubeAction.Double)
                throw AgentContractViolationException.ForCubeOffer(_onRollSeat, offer);

            transcript.Append(new CubeTranscriptEntry(game.Snapshot(), CubeAction.Double));

            MatchSeat responderSeat = _onRollSeat.Other();
            CubeAction response = await ParticipantAt(responderSeat).CubeAgent
                .ChooseResponseAsync(game, cancellationToken).ConfigureAwait(false);
            if (response is not CubeAction.Take and not CubeAction.Pass)
                throw AgentContractViolationException.ForCubeResponse(responderSeat, response);

            transcript.Append(new CubeTranscriptEntry(game.Snapshot(), response));
            return referee.ApplyCubeResponse(game, response);
        }

        /// <summary>
        /// One play turn: choose (or auto-apply a dance), validate-and-apply,
        /// transcript, detect game end. On return the perspective — and
        /// <see cref="_onRollSeat"/> — have flipped to the next mover.
        /// </summary>
        private async Task<GameResult?> PlayTurnAsync(
            GameState game, Transcript transcript, int die1, int die2)
        {
            var legalPlays = MoveGenerator.GeneratePlays(game.Board, die1, die2);

            Play chosenPlay;
            if (legalPlays.Count == 1 && legalPlays[0].Count == 0)
            {
                // Dance: the empty play is the only legal play — nothing to
                // decide, so the agent is not queried.
                chosenPlay = legalPlays[0];
            }
            else
            {
                chosenPlay = await ParticipantAt(_onRollSeat).PlayAgent
                    .ChoosePlayAsync(game, die1, die2, cancellationToken).ConfigureAwait(false);
            }

            var decisionSnapshot = game.Snapshot();
            try
            {
                game.ApplyPlay(chosenPlay, die1, die2);
            }
            catch (ArgumentException ex)
            {
                throw AgentContractViolationException.ForPlay(_onRollSeat, chosenPlay, die1, die2, ex);
            }

            transcript.Append(new PlayTranscriptEntry(decisionSnapshot, die1, die2, chosenPlay));
            _onRollSeat = _onRollSeat.Other();

            return referee.IsGameOver(game);
        }

        /// <summary>Roll, validating the user-supplied source's output.</summary>
        private (int Die1, int Die2) RollChecked()
        {
            var (die1, die2) = dice.Roll();
            if (die1 < 1 || die1 > 6 || die2 < 1 || die2 > 6)
                throw new InvalidOperationException(
                    $"IDiceSource produced an out-of-range roll ({die1}, {die2}); each die must be in 1–6.");
            return (die1, die2);
        }
    }
}
