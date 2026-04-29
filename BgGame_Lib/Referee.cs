namespace BgGame_Lib;

using BgMoveGen;

/// <summary>
/// Skeletal turn-sequencing and end-of-game arbiter. Phase 1 scope:
///
///   1. End-of-game detection by counting borne-off checkers and classifying
///      win type (single / gammon / backgammon).
///   2. Play application: validate the chosen <see cref="Play"/> is one of
///      <see cref="MoveGenerator.GeneratePlays"/>'s outputs, apply each move,
///      flip perspective on the substrate.
///   3. Cube-response application: <see cref="CubeAction.Take"/> doubles the
///      cube and transfers ownership; <see cref="CubeAction.Pass"/> ends the
///      game with the offerer winning the pre-double cube value.
///
/// Out of skeletal scope (deferred to Phase 2+): driver loop / coroutine,
/// time controls, post-Crawford automatic doubling, match-end detection
/// (which lives on <see cref="MatchState.IsMatchOver"/>), legal-to-offer
/// validation against full match context.
/// </summary>
public sealed class Referee
{
    /// <summary>
    /// If the game has ended, return the result; otherwise null.
    ///
    /// A game ends when one side has borne off all 15 checkers. Win type:
    ///   • <see cref="GameResultKind.WinSingle"/> — loser borne off ≥ 1 checker.
    ///   • <see cref="GameResultKind.WinGammon"/> — loser borne off 0 and has no checker
    ///     in the winner's home board or on the bar.
    ///   • <see cref="GameResultKind.WinBackgammon"/> — loser borne off 0 and has a
    ///     checker in the winner's home board or on the bar.
    ///
    /// Called after <see cref="ApplyPlay"/> in normal flow, by which time
    /// perspective has flipped and the just-moved (winning) player is the
    /// "opponent" from the current perspective. The natural-flow case is therefore
    /// "opponent has 0 checkers; on-roll lost." The reverse case (on-roll has 0)
    /// is also handled for fixture / construction-from-finished-position scenarios.
    /// </summary>
    public GameResult? IsGameOver(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        int onRollCheckers = 0;
        int opponentCheckers = 0;
        bool onRollOnBar = false;
        bool onRollInOpponentHome = false;
        bool opponentOnBar = false;
        bool opponentInOnRollHome = false;

        for (int i = 0; i < 26; i++)
        {
            int v = state.Board.Points[i];
            if (v > 0)
            {
                onRollCheckers += v;
                if (i == 25) onRollOnBar = true;
                else if (i >= 19 && i <= 24) onRollInOpponentHome = true;
            }
            else if (v < 0)
            {
                opponentCheckers += -v;
                if (i == 0) opponentOnBar = true;
                else if (i >= 1 && i <= 6) opponentInOnRollHome = true;
            }
        }

        if (opponentCheckers == 0 && onRollCheckers > 0)
        {
            // Opponent (just-moved player in normal flow) won.
            var kind = ClassifyWin(
                loserCheckersOnBoard: onRollCheckers,
                loserOnBar: onRollOnBar,
                loserInWinnerHome: onRollInOpponentHome);
            return new GameResult(kind, OnRollWon: false, CubeSize: state.CubeSize);
        }

        if (onRollCheckers == 0 && opponentCheckers > 0)
        {
            // On-roll player won (off-flow / fixture case).
            var kind = ClassifyWin(
                loserCheckersOnBoard: opponentCheckers,
                loserOnBar: opponentOnBar,
                loserInWinnerHome: opponentInOnRollHome);
            return new GameResult(kind, OnRollWon: true, CubeSize: state.CubeSize);
        }

        return null;
    }

    /// <summary>
    /// Apply a chosen play to the state and flip perspective.
    ///
    /// Validates that <paramref name="play"/> is one of the legal plays returned
    /// by <see cref="MoveGenerator.GeneratePlays"/> for the current board and
    /// dice. After all moves are applied, perspective swaps so the new on-roll
    /// player sees the board from their POV.
    /// </summary>
    /// <exception cref="ArgumentException">The play is not legal from the current state and dice.</exception>
    public void ApplyPlay(GameState state, Play play, int die1, int die2)
    {
        ArgumentNullException.ThrowIfNull(state);

        var legalPlays = MoveGenerator.GeneratePlays(state.Board, die1, die2);
        if (!ContainsPlay(legalPlays, play))
            throw new ArgumentException(
                "The supplied play is not one of the legal plays for the current board and dice.",
                nameof(play));

        for (int i = 0; i < play.Count; i++)
            MoveGenerator.ApplyMove(state.Board, play[i]);

        state.SwapPerspective();
    }

    /// <summary>
    /// Apply a cube response to a pending double offer.
    ///
    ///   • <see cref="CubeAction.Take"/>: cube size doubles; ownership transfers
    ///     to the responder (the current opponent in this perspective). Returns null
    ///     — the game continues.
    ///   • <see cref="CubeAction.Pass"/>: the offerer wins the pre-double cube
    ///     value as a single-win game result. Returns the <see cref="GameResult"/>.
    ///     State is not mutated; the caller awards the game via <see cref="MatchState.AwardGame"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="response"/> is not <see cref="CubeAction.Take"/> or <see cref="CubeAction.Pass"/>.</exception>
    public GameResult? ApplyCubeResponse(GameState state, CubeAction response)
    {
        ArgumentNullException.ThrowIfNull(state);

        return response switch
        {
            CubeAction.Take => HandleTake(state),
            CubeAction.Pass => HandlePass(state),
            _ => throw new ArgumentException(
                $"Cube response must be Take or Pass; got {response}.", nameof(response)),
        };
    }

    private static GameResult? HandleTake(GameState state)
    {
        state.DoubleCube();
        return null;
    }

    private static GameResult HandlePass(GameState state)
    {
        // Pass: offerer (current on-roll player, who just doubled) wins the
        // pre-double cube value as a single-win. State is left unchanged so
        // callers can choose how to record / dispose of the game.
        return new GameResult(GameResultKind.WinSingle, OnRollWon: true, CubeSize: state.CubeSize);
    }

    private static GameResultKind ClassifyWin(
        int loserCheckersOnBoard, bool loserOnBar, bool loserInWinnerHome)
    {
        if (loserCheckersOnBoard < 15) return GameResultKind.WinSingle;
        if (loserOnBar || loserInWinnerHome) return GameResultKind.WinBackgammon;
        return GameResultKind.WinGammon;
    }

    private static bool ContainsPlay(List<Play> plays, Play candidate)
    {
        var key = candidate.DeduplicationKey();
        foreach (var p in plays)
        {
            if (p.Count != candidate.Count) continue;
            if (p.DeduplicationKey().Equals(key)) return true;
        }
        return false;
    }
}
