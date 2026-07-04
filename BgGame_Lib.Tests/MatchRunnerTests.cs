namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;
using BgMoveGen;

public class MatchRunnerTests
{
    // ── Helpers ───────────────────────────────────────────────────

    private static MatchParticipant Participant(IPlayAgent play, ICubeAgent cube) => new(play, cube);

    private static List<T> EntriesOf<T>(Transcript transcript) where T : TranscriptEntry =>
        transcript.Entries.OfType<T>().ToList();

    private sealed class OutOfRangeDiceSource : IDiceSource
    {
        public (int Die1, int Die2) Roll() => (7, 3);
    }

    /// <summary>Collects every observer callback, in order, as typed events.</summary>
    private sealed class RecordingObserver : IMatchObserver
    {
        public sealed record GameStarted(GameStartContext Context);
        public sealed record EntryRecorded(TranscriptEntry Entry);
        public sealed record GameEnded(int GameNumber, GameRecord Record);
        public sealed record MatchEnded(MatchResult Result);

        public List<object> Events { get; } = [];

        public void OnGameStarted(GameStartContext context) => Events.Add(new GameStarted(context));
        public void OnEntryRecorded(TranscriptEntry entry) => Events.Add(new EntryRecorded(entry));
        public void OnGameEnded(int gameNumber, GameRecord record) => Events.Add(new GameEnded(gameNumber, record));
        public void OnMatchEnded(MatchResult result) => Events.Add(new MatchEnded(result));
    }

    /// <summary>Distinctive type proving observer exceptions propagate unwrapped.</summary>
    private sealed class ObserverFailedException : Exception;

    private sealed class ThrowingObserver : IMatchObserver
    {
        public void OnGameStarted(GameStartContext context) { }
        public void OnEntryRecorded(TranscriptEntry entry) => throw new ObserverFailedException();
        public void OnGameEnded(int gameNumber, GameRecord record) { }
        public void OnMatchEnded(MatchResult result) { }
    }

    // ── Argument validation (eager, before the loop starts) ──────

    [Fact]
    public async Task RunMatchAsync_NullParticipant_Throws()
    {
        var runner = new MatchRunner(new SeededDiceSource(1));
        var p = Participant(new FirstPlayAgent(), CubeAgents.Never());

        await Assert.ThrowsAsync<ArgumentNullException>(() => runner.RunMatchAsync(null!, p, 5));
        await Assert.ThrowsAsync<ArgumentNullException>(() => runner.RunMatchAsync(p, null!, 5));
    }

    [Fact]
    public async Task RunMatchAsync_MoneySessionWithoutMaxGames_Throws()
    {
        var runner = new MatchRunner(new SeededDiceSource(1));
        var p = Participant(new FirstPlayAgent(), CubeAgents.Never());

        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunMatchAsync(p, p, matchLength: 0));
    }

    [Fact]
    public async Task RunMatchAsync_MaxGamesBelowOne_Throws()
    {
        var runner = new MatchRunner(new SeededDiceSource(1));
        var p = Participant(new FirstPlayAgent(), CubeAgents.Never());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => runner.RunMatchAsync(p, p, 5, maxGames: 0));
    }

    [Fact]
    public async Task RunMatchAsync_NegativeMatchLength_Throws()
    {
        var runner = new MatchRunner(new SeededDiceSource(1));
        var p = Participant(new FirstPlayAgent(), CubeAgents.Never());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => runner.RunMatchAsync(p, p, matchLength: -1));
    }

    [Fact]
    public void Constructor_NullDiceSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new MatchRunner(null!));
    }

    // ── Scripted deterministic flows ──────────────────────────────
    //
    // The double→pass script ends a game at its second turn, before any
    // second roll — each game consumes exactly its opening pair(s). That
    // makes seat mapping, transcript shape, and scoring fully scriptable
    // with a RecordedDiceSource.

    [Fact]
    public async Task DoublePass_WinsPreDoubleCubeValue_EveryGame()
    {
        // (6,5): seat One wins every opening and plays it. Turn 2 is seat Two's
        // window: Two doubles, One passes → Two scores the PRE-double value 1.
        // A money session keeps the script exact for all five games — in match
        // play the last game before the finish line would be Crawford and the
        // window would (correctly) never open.
        var dice = new RecordedDiceSource(Enumerable.Repeat((6, 5), 5));
        var runner = new MatchRunner(dice);
        var one = Participant(new FirstPlayAgent(), new DelegateCubeAgent(
            offer: _ => CubeAction.NoDouble, response: _ => CubeAction.Pass));
        var two = Participant(new FirstPlayAgent(), CubeAgents.AlwaysDoubleAlwaysPass());

        var result = await runner.RunMatchAsync(one, two, matchLength: 0, maxGames: 5);

        Assert.Null(result.Winner);   // money session has no match winner
        Assert.Equal(0, result.SeatOneScore);
        Assert.Equal(5, result.SeatTwoScore);
        Assert.Equal(5, result.Games.Count);
        Assert.Equal(0, dice.Remaining);

        foreach (var game in result.Games)
        {
            Assert.Equal(MatchSeat.Two, game.Winner);
            Assert.Equal(GameResultKind.WinSingle, game.Result.Kind);
            Assert.Equal(1, game.Result.CubeSize);   // pre-double value
            Assert.Equal(1, game.Result.Points);

            // Exact transcript shape: opening play, offer, pass, game end.
            Assert.Collection(game.Transcript.Entries,
                e => Assert.IsType<PlayTranscriptEntry>(e),
                e => Assert.Equal(CubeAction.Double, Assert.IsType<CubeTranscriptEntry>(e).Action),
                e => Assert.Equal(CubeAction.Pass, Assert.IsType<CubeTranscriptEntry>(e).Action),
                e => Assert.IsType<GameEndedTranscriptEntry>(e));

            var opening = (PlayTranscriptEntry)game.Transcript.Entries[0];
            Assert.Equal(6, opening.Die1);
            Assert.Equal(5, opening.Die2);
        }
    }

    [Fact]
    public async Task DoubleTakeRedoublePass_PassScoresAtPreRedoubleValue()
    {
        // Turn 2 (seat Two's window): Two doubles, One TAKES → cube 2, One owns.
        // Two rolls (4,3) and plays. Turn 3 (seat One's window, One owns the
        // cube): One redoubles, Two passes → One wins the pre-redouble value 2.
        var dice = new RecordedDiceSource([(6, 5), (4, 3)]);
        var runner = new MatchRunner(dice);
        var one = Participant(new FirstPlayAgent(), new DelegateCubeAgent(
            offer: _ => CubeAction.Double, response: _ => CubeAction.Take));
        var two = Participant(new FirstPlayAgent(), new DelegateCubeAgent(
            offer: s => s.CubeSize == 1 ? CubeAction.Double : CubeAction.NoDouble,
            response: _ => CubeAction.Pass));

        var result = await runner.RunMatchAsync(one, two, matchLength: 5, maxGames: 1);

        Assert.Null(result.Winner);   // capped before anyone reached 5
        Assert.Equal(2, result.SeatOneScore);
        Assert.Equal(0, result.SeatTwoScore);
        var game = Assert.Single(result.Games);
        Assert.Equal(MatchSeat.One, game.Winner);
        Assert.Equal(2, game.Result.CubeSize);   // pre-redouble value
        Assert.Equal(2, game.Result.Points);
        Assert.Equal(0, dice.Remaining);

        Assert.Collection(game.Transcript.Entries,
            e => Assert.IsType<PlayTranscriptEntry>(e),
            e => Assert.Equal(CubeAction.Double, Assert.IsType<CubeTranscriptEntry>(e).Action),
            e => Assert.Equal(CubeAction.Take, Assert.IsType<CubeTranscriptEntry>(e).Action),
            e => Assert.IsType<PlayTranscriptEntry>(e),
            e => Assert.Equal(CubeAction.Double, Assert.IsType<CubeTranscriptEntry>(e).Action),
            e => Assert.Equal(CubeAction.Pass, Assert.IsType<CubeTranscriptEntry>(e).Action),
            e => Assert.IsType<GameEndedTranscriptEntry>(e));
    }

    [Fact]
    public async Task OpeningRoll_TiesRerollAndAreConsumed()
    {
        // Two ties, then (4,2): seat One's die is higher — One plays (4,2).
        var dice = new RecordedDiceSource([(3, 3), (1, 1), (4, 2)]);
        var runner = new MatchRunner(dice);
        var one = Participant(new FirstPlayAgent(), new DelegateCubeAgent(
            offer: _ => CubeAction.NoDouble, response: _ => CubeAction.Pass));
        var two = Participant(new FirstPlayAgent(), CubeAgents.AlwaysDoubleAlwaysPass());

        var result = await runner.RunMatchAsync(one, two, matchLength: 5, maxGames: 1);

        Assert.Equal(0, dice.Remaining);   // tie rolls consumed from the source
        var game = Assert.Single(result.Games);
        var opening = Assert.IsType<PlayTranscriptEntry>(game.Transcript.Entries[0]);
        Assert.Equal(4, opening.Die1);
        Assert.Equal(2, opening.Die2);
        // Seat One played the opening, so turn 2 was seat Two's cube window.
        Assert.Equal(MatchSeat.Two, game.Winner);
    }

    [Fact]
    public async Task OpeningRoll_Die2Higher_SeatTwoPlaysFirst()
    {
        // Die1 = 2 (seat One), Die2 = 4 (seat Two) → seat Two wins the opening
        // and plays (2,4); turn 2 is seat One's window: One doubles, Two passes.
        var dice = new RecordedDiceSource([(2, 4)]);
        var runner = new MatchRunner(dice);
        var one = Participant(new FirstPlayAgent(), CubeAgents.AlwaysDoubleAlwaysPass());
        var two = Participant(new FirstPlayAgent(), new DelegateCubeAgent(
            offer: _ => CubeAction.NoDouble, response: _ => CubeAction.Pass));

        var result = await runner.RunMatchAsync(one, two, matchLength: 5, maxGames: 1);

        var game = Assert.Single(result.Games);
        Assert.Equal(MatchSeat.One, game.Winner);
        Assert.Equal(1, result.SeatOneScore);
        Assert.Equal(0, result.SeatTwoScore);
    }

    // ── Responder-frame cube query (perspective unification) ──────
    //
    // The queried player always sees its own frame: ChooseResponseAsync
    // receives a detached GameState.OpponentView of the live state. These pins
    // compare the responder's captured snapshot against the double-offer
    // transcript entry — which is captured from the live (offerer-frame) state
    // immediately before the query.

    [Fact]
    public async Task CubeResponse_QueryStateIsResponderFrame_LiveStateStaysOffererFrame()
    {
        // Game 1: (6,5) opening → One plays; turn 2 Two doubles, One passes →
        // Two leads 1–0, making game 2's scores asymmetric. Game 2, turn 2:
        // Two doubles again and One's response spy captures its query state.
        GameSnapshot? captured = null;
        var dice = new RecordedDiceSource([(6, 5), (6, 5)]);
        var runner = new MatchRunner(dice);
        var one = Participant(new FirstPlayAgent(), new DelegateCubeAgent(
            offer: _ => CubeAction.NoDouble,
            response: s => { captured = s.Snapshot(); return CubeAction.Pass; }));
        var two = Participant(new FirstPlayAgent(), CubeAgents.AlwaysDoubleAlwaysPass());

        var result = await runner.RunMatchAsync(one, two, matchLength: 5, maxGames: 2);

        Assert.Equal(2, result.Games.Count);
        Assert.Equal(0, result.SeatOneScore);
        Assert.Equal(2, result.SeatTwoScore);

        // The live frame at query time: the offer entry's snapshot (offerer =
        // seat Two on roll, leading 1–0 in its own labels).
        var offer = Assert.IsType<CubeTranscriptEntry>(result.Games[1].Transcript.Entries[1]);
        Assert.Equal(CubeAction.Double, offer.Action);
        Assert.Equal(1, offer.State.Match.OnRollScore);
        Assert.Equal(0, offer.State.Match.OpponentScore);

        // The responder saw its own frame: board flipped, scores swapped,
        // cube size / length / Crawford carried over.
        Assert.NotNull(captured);
        for (int i = 0; i < 26; i++)
            Assert.Equal(-offer.State.Board[25 - i], captured.Board[i]);
        Assert.Equal(0, captured.Match.OnRollScore);
        Assert.Equal(1, captured.Match.OpponentScore);
        Assert.Equal(CubeOwner.Centered, captured.CubeOwner);
        Assert.Equal(offer.State.CubeSize, captured.CubeSize);
        Assert.Equal(offer.State.Match.MatchLength, captured.Match.MatchLength);
        Assert.Equal(offer.State.Match.IsCrawford, captured.Match.IsCrawford);

        // The live state stayed in the offerer's frame across the query: the
        // response entry (snapshotted after it) matches the offer entry.
        var response = Assert.IsType<CubeTranscriptEntry>(result.Games[1].Transcript.Entries[2]);
        Assert.Equal(CubeAction.Pass, response.Action);
        Assert.Equal(offer.State.Board, response.State.Board);
        Assert.Equal(offer.State.CubeSize, response.State.CubeSize);
        Assert.Equal(offer.State.CubeOwner, response.State.CubeOwner);
        Assert.Equal(offer.State.Match, response.State.Match);
    }

    [Fact]
    public async Task CubeResponse_RedoubleQuery_ShowsOffererOwnedCubeAsOpponent()
    {
        // Double→take→redouble: turn 2 Two doubles, One takes (cube 2, One
        // owns); turn 3 One redoubles and Two's response spy captures its
        // query state — the offerer-owned cube must read as Opponent in the
        // responder's frame, at the pre-redouble size.
        GameSnapshot? captured = null;
        var dice = new RecordedDiceSource([(6, 5), (4, 3)]);
        var runner = new MatchRunner(dice);
        var one = Participant(new FirstPlayAgent(), new DelegateCubeAgent(
            offer: _ => CubeAction.Double, response: _ => CubeAction.Take));
        var two = Participant(new FirstPlayAgent(), new DelegateCubeAgent(
            offer: s => s.CubeSize == 1 ? CubeAction.Double : CubeAction.NoDouble,
            response: s => { captured = s.Snapshot(); return CubeAction.Pass; }));

        var result = await runner.RunMatchAsync(one, two, matchLength: 5, maxGames: 1);

        // Behavioral outcome unchanged from the offerer-frame era: One wins
        // the pre-redouble value 2.
        Assert.Equal(2, result.SeatOneScore);
        Assert.Equal(0, result.SeatTwoScore);

        Assert.NotNull(captured);
        Assert.Equal(CubeOwner.Opponent, captured.CubeOwner);
        Assert.Equal(2, captured.CubeSize);

        // Live frame at the same moment: offerer One on roll, owning the cube.
        var game = Assert.Single(result.Games);
        var redouble = Assert.IsType<CubeTranscriptEntry>(game.Transcript.Entries[4]);
        Assert.Equal(CubeAction.Double, redouble.Action);
        Assert.Equal(CubeOwner.OnRoll, redouble.State.CubeOwner);
    }

    // ── Crawford / post-Crawford (end-to-end through the runner) ──

    [Fact]
    public async Task Crawford_NoCubeWindow_AndPostCrawford_DoublingResumes()
    {
        // 2-point match with always-double / always-pass agents on both seats:
        // game 1 ends double→pass (1–0) → game 2 is Crawford and must show ZERO
        // cube entries despite both agents doubling at any open window. If the
        // trailer wins game 2 with a single (1–1), game 3 is post-Crawford and
        // the window must reopen (double→pass ends it immediately).
        //
        // Which branch occurs depends on game 2's checker play; sweep seeds so
        // both branches are exercised deterministically.
        bool sawTwoGameMatch = false, sawThreeGameMatch = false;

        for (int seed = 0; seed < 50 && !(sawTwoGameMatch && sawThreeGameMatch); seed++)
        {
            var runner = new MatchRunner(new SeededDiceSource(seed));
            var one = Participant(new RandomPlayAgent(seed + 1000), CubeAgents.AlwaysDoubleAlwaysPass());
            var two = Participant(new RandomPlayAgent(seed + 2000), CubeAgents.AlwaysDoubleAlwaysPass());

            var result = await runner.RunMatchAsync(one, two, matchLength: 2);

            Assert.NotNull(result.Winner);

            // Game 1: pre-Crawford, double→pass.
            Assert.Equal(2, EntriesOf<CubeTranscriptEntry>(result.Games[0].Transcript).Count);

            // Exactly one Crawford game, and it contains no cube entries.
            var crawfordGames = result.Games
                .Where(g => g.Transcript.Entries[0].State.Match.IsCrawford)
                .ToList();
            var crawford = Assert.Single(crawfordGames);
            Assert.Empty(EntriesOf<CubeTranscriptEntry>(crawford.Transcript));
            // The Crawford game is decided by checker play, not by a pass.
            Assert.True(crawford.Transcript.Entries.Count > 2);

            if (result.Games.Count == 2)
            {
                sawTwoGameMatch = true;
            }
            else
            {
                // Post-Crawford: the window reopened and the game ended by pass.
                Assert.Equal(3, result.Games.Count);
                var postCrawford = result.Games[2];
                Assert.False(postCrawford.Transcript.Entries[0].State.Match.IsCrawford);
                Assert.Equal(2, EntriesOf<CubeTranscriptEntry>(postCrawford.Transcript).Count);
                sawThreeGameMatch = true;
            }
        }

        Assert.True(sawTwoGameMatch, "seed sweep never produced a 2-game match");
        Assert.True(sawThreeGameMatch, "seed sweep never produced a post-Crawford game");
    }

    [Fact]
    public async Task OnePointMatch_IsCubeless_SingleGameDecides()
    {
        // Both agents would double at any open window — but a 1-point match has
        // no cube, so the window never opens and no cube entry can appear.
        var runner = new MatchRunner(new SeededDiceSource(7));
        var one = Participant(new RandomPlayAgent(1), CubeAgents.AlwaysDoubleAlwaysTake());
        var two = Participant(new RandomPlayAgent(2), CubeAgents.AlwaysDoubleAlwaysTake());

        var result = await runner.RunMatchAsync(one, two, matchLength: 1);

        Assert.NotNull(result.Winner);
        var game = Assert.Single(result.Games);
        Assert.Empty(EntriesOf<CubeTranscriptEntry>(game.Transcript));
        Assert.Equal(1, game.Result.CubeSize);
        Assert.InRange(game.Result.Points, 1, 3);   // single / gammon / backgammon at cube 1
        int winnerScore = result.Winner == MatchSeat.One ? result.SeatOneScore : result.SeatTwoScore;
        Assert.Equal(game.Result.Points, winnerScore);
    }

    // ── Money session ─────────────────────────────────────────────

    [Fact]
    public async Task MoneySession_RunsGameByGame_NoMatchEnd_NoCrawford()
    {
        var runner = new MatchRunner(new SeededDiceSource(11));
        var one = Participant(new RandomPlayAgent(3), CubeAgents.DoubleOnceTake());
        var two = Participant(new RandomPlayAgent(4), CubeAgents.DoubleOnceTake());

        var result = await runner.RunMatchAsync(one, two, matchLength: 0, maxGames: 4);

        Assert.Null(result.Winner);
        Assert.Equal(4, result.Games.Count);

        int expectedOne = result.Games.Where(g => g.Winner == MatchSeat.One).Sum(g => g.Result.Points);
        int expectedTwo = result.Games.Where(g => g.Winner == MatchSeat.Two).Sum(g => g.Result.Points);
        Assert.Equal(expectedOne, result.SeatOneScore);
        Assert.Equal(expectedTwo, result.SeatTwoScore);

        // Money has a live cube (the double-once agents used it) but no Crawford.
        Assert.Contains(result.Games, g => EntriesOf<CubeTranscriptEntry>(g.Transcript).Count > 0);
        foreach (var entry in result.Games.SelectMany(g => g.Transcript.Entries))
        {
            Assert.Equal(0, entry.State.Match.MatchLength);
            Assert.False(entry.State.Match.IsCrawford);
        }
    }

    // ── Dance turns ───────────────────────────────────────────────

    [Fact]
    public async Task DanceTurns_AutoApplied_AgentNeverQueried_GameAdvances()
    {
        // The spy asserts it is never asked to "choose" when the only legal
        // play is the empty play; the transcript scan proves dance turns did
        // occur (as empty-play entries) and the games ran to completion anyway.
        int danceEntries = 0;

        for (int seed = 0; seed < 40; seed++)
        {
            IPlayAgent Spy(int agentSeed)
            {
                var inner = new Random(agentSeed);
                return new DelegatePlayAgent((state, d1, d2) =>
                {
                    var plays = MoveGenerator.GeneratePlays(state.Board, d1, d2);
                    Assert.False(plays.Count == 1 && plays[0].Count == 0,
                        "agent was queried on a dance turn");
                    return plays[inner.Next(plays.Count)];
                });
            }

            var runner = new MatchRunner(new SeededDiceSource(seed));
            var one = Participant(Spy(seed + 1), CubeAgents.Never());
            var two = Participant(Spy(seed + 2), CubeAgents.Never());

            var result = await runner.RunMatchAsync(one, two, matchLength: 3);

            Assert.NotNull(result.Winner);
            danceEntries += result.Games
                .SelectMany(g => g.Transcript.Entries.OfType<PlayTranscriptEntry>())
                .Count(e => e.ChosenPlay.Count == 0);
        }

        Assert.True(danceEntries > 0, "seed sweep produced no dance turns — the auto-apply path went unexercised");
    }

    // ── Cancellation ──────────────────────────────────────────────

    [Fact]
    public async Task Cancellation_AbortsCleanly()
    {
        using var cts = new CancellationTokenSource();
        int calls = 0;
        var cancellingAgent = new DelegatePlayAgent((state, d1, d2) =>
        {
            if (++calls == 10) cts.Cancel();
            return MoveGenerator.GeneratePlays(state.Board, d1, d2)[0];
        });

        var runner = new MatchRunner(new SeededDiceSource(5));
        var one = Participant(cancellingAgent, CubeAgents.Never());
        var two = Participant(new FirstPlayAgent(), CubeAgents.Never());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.RunMatchAsync(one, two, matchLength: 25, cancellationToken: cts.Token));
    }

    // ── Agent contract violations ─────────────────────────────────

    [Fact]
    public async Task IllegalPlay_ThrowsViolation_WithSeatAndPlay()
    {
        // Seat One wins the opening (6,5) and returns a play its dice can't make.
        var badPlay = new Play();
        badPlay.Add(new Move(13, 12));   // a 1 — dice are (6,5)

        var runner = new MatchRunner(new RecordedDiceSource([(6, 5)]));
        var one = Participant(new DelegatePlayAgent((_, _, _) => badPlay), CubeAgents.Never());
        var two = Participant(new FirstPlayAgent(), CubeAgents.Never());

        var ex = await Assert.ThrowsAsync<AgentContractViolationException>(
            () => runner.RunMatchAsync(one, two, matchLength: 5));

        Assert.Equal(MatchSeat.One, ex.Seat);
        Assert.Equal(AgentContractViolationKind.IllegalPlay, ex.Kind);
        Assert.Equal(badPlay, ex.OffendingPlay);
        Assert.Null(ex.OffendingCubeAction);
        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Fact]
    public async Task IllegalCubeOffer_ThrowsViolation()
    {
        // Turn 2 is seat Two's window; its offer returns a response-side value.
        var runner = new MatchRunner(new RecordedDiceSource([(6, 5)]));
        var one = Participant(new FirstPlayAgent(), CubeAgents.Never());
        var two = Participant(new FirstPlayAgent(), new DelegateCubeAgent(
            offer: _ => CubeAction.Take, response: _ => CubeAction.Take));

        var ex = await Assert.ThrowsAsync<AgentContractViolationException>(
            () => runner.RunMatchAsync(one, two, matchLength: 5));

        Assert.Equal(MatchSeat.Two, ex.Seat);
        Assert.Equal(AgentContractViolationKind.IllegalCubeOffer, ex.Kind);
        Assert.Equal(CubeAction.Take, ex.OffendingCubeAction);
    }

    [Fact]
    public async Task IllegalCubeResponse_ThrowsViolation()
    {
        // Seat Two doubles; seat One's response returns an offer-side value.
        var runner = new MatchRunner(new RecordedDiceSource([(6, 5)]));
        var one = Participant(new FirstPlayAgent(), new DelegateCubeAgent(
            offer: _ => CubeAction.NoDouble, response: _ => CubeAction.Double));
        var two = Participant(new FirstPlayAgent(), CubeAgents.AlwaysDoubleAlwaysPass());

        var ex = await Assert.ThrowsAsync<AgentContractViolationException>(
            () => runner.RunMatchAsync(one, two, matchLength: 5));

        Assert.Equal(MatchSeat.One, ex.Seat);
        Assert.Equal(AgentContractViolationKind.IllegalCubeResponse, ex.Kind);
        Assert.Equal(CubeAction.Double, ex.OffendingCubeAction);
    }

    [Fact]
    public async Task OutOfRangeDiceSource_Throws()
    {
        var runner = new MatchRunner(new OutOfRangeDiceSource());
        var p = Participant(new FirstPlayAgent(), CubeAgents.Never());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunMatchAsync(p, p, matchLength: 5));
    }

    // ── Seat identity stamps ──────────────────────────────────────
    //
    // OnRollSeat is the frame seat — the seat whose perspective the entry's
    // snapshot is in. Plays flip it; cube entries don't (both halves are
    // snapshotted against the live offerer-frame state); attribution is
    // derived on the entry types (ActingSeat, Winner).

    [Fact]
    public async Task SeatStamps_DoubleTakeRedouble_ExactSequencePinned()
    {
        // Same script as DoubleTakeRedoublePass: One wins the opening (6,5)
        // and plays; turn 2 Two doubles / One takes; Two plays (4,3); turn 3
        // One redoubles / Two passes.
        var dice = new RecordedDiceSource([(6, 5), (4, 3)]);
        var runner = new MatchRunner(dice);
        var one = Participant(new FirstPlayAgent(), new DelegateCubeAgent(
            offer: _ => CubeAction.Double, response: _ => CubeAction.Take));
        var two = Participant(new FirstPlayAgent(), new DelegateCubeAgent(
            offer: s => s.CubeSize == 1 ? CubeAction.Double : CubeAction.NoDouble,
            response: _ => CubeAction.Pass));

        var result = await runner.RunMatchAsync(one, two, matchLength: 5, maxGames: 1);

        var game = Assert.Single(result.Games);
        Assert.Collection(game.Transcript.Entries,
            e => Assert.Equal(MatchSeat.One, Assert.IsType<PlayTranscriptEntry>(e).OnRollSeat),
            e =>
            {
                var offer = Assert.IsType<CubeTranscriptEntry>(e);
                Assert.Equal(MatchSeat.Two, offer.OnRollSeat);   // Two's window after One's play
                Assert.Equal(MatchSeat.Two, offer.ActingSeat);   // offer at on-roll
            },
            e =>
            {
                var take = Assert.IsType<CubeTranscriptEntry>(e);
                Assert.Equal(MatchSeat.Two, take.OnRollSeat);    // offerer frame — no flip
                Assert.Equal(MatchSeat.One, take.ActingSeat);    // response at other
            },
            e => Assert.Equal(MatchSeat.Two, Assert.IsType<PlayTranscriptEntry>(e).OnRollSeat),
            e =>
            {
                var redouble = Assert.IsType<CubeTranscriptEntry>(e);
                Assert.Equal(MatchSeat.One, redouble.OnRollSeat);
                Assert.Equal(MatchSeat.One, redouble.ActingSeat);
            },
            e =>
            {
                var pass = Assert.IsType<CubeTranscriptEntry>(e);
                Assert.Equal(MatchSeat.One, pass.OnRollSeat);    // no flip
                Assert.Equal(MatchSeat.Two, pass.ActingSeat);
            },
            e =>
            {
                var ended = Assert.IsType<GameEndedTranscriptEntry>(e);
                Assert.Equal(MatchSeat.One, ended.OnRollSeat);   // a pass leaves the offerer's frame live
                Assert.Equal(MatchSeat.One, ended.Winner);
                Assert.Equal(game.Winner, ended.Winner);
            });
    }

    [Fact]
    public async Task SeatStamps_OpeningWonBySeatTwo_StampsTwoFirst()
    {
        // Die2 higher: Two wins the opening and moves first; turn 2 is One's
        // window (One doubles, Two passes).
        var dice = new RecordedDiceSource([(2, 4)]);
        var runner = new MatchRunner(dice);
        var one = Participant(new FirstPlayAgent(), CubeAgents.AlwaysDoubleAlwaysPass());
        var two = Participant(new FirstPlayAgent(), new DelegateCubeAgent(
            offer: _ => CubeAction.NoDouble, response: _ => CubeAction.Pass));

        var result = await runner.RunMatchAsync(one, two, matchLength: 5, maxGames: 1);

        var game = Assert.Single(result.Games);
        var opening = Assert.IsType<PlayTranscriptEntry>(game.Transcript.Entries[0]);
        Assert.Equal(MatchSeat.Two, opening.OnRollSeat);
        var offer = Assert.IsType<CubeTranscriptEntry>(game.Transcript.Entries[1]);
        Assert.Equal(MatchSeat.One, offer.ActingSeat);
        var pass = Assert.IsType<CubeTranscriptEntry>(game.Transcript.Entries[2]);
        Assert.Equal(MatchSeat.Two, pass.ActingSeat);
    }

    [Theory]
    [InlineData(5, 42)]
    [InlineData(15, 7)]
    public async Task SeatStamps_ConsistentWithFrameWalk_EverySeededGame(int matchLength, int seed)
    {
        // The stamps must agree with the previously consumer-side frame walk
        // for every entry of every game: the opening-die rule names the first
        // stamp, every play entry (dance turns included) flips it, cube
        // entries carry it unflipped.
        var result = await RunSeededMatch(matchLength, seed);

        foreach (var game in result.Games)
        {
            var entries = game.Transcript.Entries;
            var opening = Assert.IsType<PlayTranscriptEntry>(entries[0]);
            MatchSeat expected = opening.Die1 > opening.Die2 ? MatchSeat.One : MatchSeat.Two;

            foreach (var entry in entries)
            {
                Assert.Equal(expected, entry.OnRollSeat);
                if (entry is PlayTranscriptEntry) expected = expected.Other();
            }

            // Win attribution is single-sourced on the terminal entry.
            var ended = Assert.IsType<GameEndedTranscriptEntry>(entries[^1]);
            Assert.Equal(game.Winner, ended.Winner);
        }
    }

    // ── Observer seam ─────────────────────────────────────────────

    [Fact]
    public async Task Observer_StreamMirrorsTranscript_SameInstances_LifecycleBracketed()
    {
        // One cursor walk over the flat event list pins order, content, and
        // completeness at once: per game a GameStarted(n), then exactly the
        // game's transcript entries (reference-identical — SSOT, no parallel
        // event family), then GameEnded(n) with the same GameRecord instance;
        // finally MatchEnded with the returned result, and nothing else.
        var observer = new RecordingObserver();
        var result = await RunSeededMatch(matchLength: 9, seed: 21, observer);

        int i = 0;
        for (int g = 0; g < result.Games.Count; g++)
        {
            var started = Assert.IsType<RecordingObserver.GameStarted>(observer.Events[i++]);
            Assert.Equal(g + 1, started.Context.GameNumber);

            foreach (var entry in result.Games[g].Transcript.Entries)
            {
                var recorded = Assert.IsType<RecordingObserver.EntryRecorded>(observer.Events[i++]);
                Assert.Same(entry, recorded.Entry);
            }

            var ended = Assert.IsType<RecordingObserver.GameEnded>(observer.Events[i++]);
            Assert.Equal(g + 1, ended.GameNumber);
            Assert.Same(result.Games[g], ended.Record);
        }

        var matchEnded = Assert.IsType<RecordingObserver.MatchEnded>(observer.Events[i++]);
        Assert.Same(result, matchEnded.Result);
        Assert.Equal(observer.Events.Count, i);
    }

    [Fact]
    public async Task Forfeit_CompletedGamesReachedObserver_StreamStopsWithoutTerminalEvents()
    {
        // Game 1 completes double→pass; in game 2 seat Two's offer returns a
        // response-side value (Take), aborting the run mid-game — after One's
        // opening play was already delivered.
        var observer = new RecordingObserver();
        var runner = new MatchRunner(new RecordedDiceSource([(6, 5), (6, 5)]));
        int offers = 0;
        var one = Participant(new FirstPlayAgent(), new DelegateCubeAgent(
            offer: _ => CubeAction.NoDouble, response: _ => CubeAction.Pass));
        var two = Participant(new FirstPlayAgent(), new DelegateCubeAgent(
            offer: _ => ++offers == 1 ? CubeAction.Double : CubeAction.Take,
            response: _ => CubeAction.Pass));

        var ex = await Assert.ThrowsAsync<AgentContractViolationException>(
            () => runner.RunMatchAsync(one, two, matchLength: 5, observer: observer));
        Assert.Equal(MatchSeat.Two, ex.Seat);
        Assert.Equal(AgentContractViolationKind.IllegalCubeOffer, ex.Kind);

        // The completed game 1 was fully delivered before the abort...
        var gameEnded = Assert.Single(observer.Events.OfType<RecordingObserver.GameEnded>());
        Assert.Equal(1, gameEnded.GameNumber);
        Assert.Equal(MatchSeat.Two, gameEnded.Record.Winner);
        Assert.Equal(4, gameEnded.Record.Transcript.Entries.Count);

        // ...game 2 started and streamed its opening play, then the stream
        // just stopped: no GameEnded(2), no MatchEnded.
        Assert.Equal(2, observer.Events.OfType<RecordingObserver.GameStarted>().Count());
        var last = Assert.IsType<RecordingObserver.EntryRecorded>(observer.Events[^1]);
        Assert.IsType<PlayTranscriptEntry>(last.Entry);
        Assert.Empty(observer.Events.OfType<RecordingObserver.MatchEnded>());
        Assert.Equal(8, observer.Events.Count);   // started, 4 entries, ended, started, 1 entry
    }

    [Fact]
    public async Task Observer_ExceptionPropagatesUnwrapped_FailFast()
    {
        var runner = new MatchRunner(new RecordedDiceSource([(6, 5)]));
        var one = Participant(new FirstPlayAgent(), CubeAgents.Never());
        var two = Participant(new FirstPlayAgent(), CubeAgents.Never());

        await Assert.ThrowsAsync<ObserverFailedException>(
            () => runner.RunMatchAsync(one, two, matchLength: 5, observer: new ThrowingObserver()));
    }

    // ── OnGameStarted context: entering scores + Crawford ─────────

    [Fact]
    public async Task OnGameStarted_CarriesSeatAbsoluteEnteringScores_Scripted()
    {
        // A fully scripted three-game run with alternating openings, each game
        // decided immediately by a double-and-pass so no in-game dice are
        // needed: opening (1,6) → seat Two wins the opening and plays, seat One
        // then doubles from on-roll and Two passes → One wins 1; opening (6,1)
        // hands the point to Two symmetrically. Scores march 0-0 → 1-0 → 1-1,
        // and the context reports them seat-absolutely regardless of which seat
        // is on roll as each game opens.
        var observer = new RecordingObserver();
        var runner = new MatchRunner(new RecordedDiceSource([(1, 6), (6, 1), (1, 6)]));
        var one = Participant(new FirstPlayAgent(), CubeAgents.AlwaysDoubleAlwaysPass());
        var two = Participant(new FirstPlayAgent(), CubeAgents.AlwaysDoubleAlwaysPass());

        await runner.RunMatchAsync(one, two, matchLength: 5, maxGames: 3, observer: observer);

        var contexts = observer.Events
            .OfType<RecordingObserver.GameStarted>()
            .Select(e => e.Context)
            .ToList();

        Assert.Equal(
            [(1, 0, 0), (2, 1, 0), (3, 1, 1)],
            contexts.Select(c => (c.GameNumber, c.SeatOneScore, c.SeatTwoScore)));
        Assert.All(contexts, c => Assert.False(c.IsCrawford));   // leader never reaches matchLength − 1
    }

    [Fact]
    public async Task OnGameStarted_CrawfordFlag_CrossesTheBoundary_SeededMatch()
    {
        // A seeded full match that provably crosses Crawford: the flag turns
        // true entering the Crawford game and back to false entering the
        // post-Crawford game. The context's per-game scores and flag are
        // cross-checked against two independent substrate witnesses — the
        // cumulative game awards and each game's own opening-play snapshot — so
        // this pins the wiring, not a re-encoding of the Crawford rule.
        var observer = new RecordingObserver();
        var result = await RunSeededMatch(matchLength: 7, seed: CrawfordCrossingSeed, observer);

        var contexts = observer.Events
            .OfType<RecordingObserver.GameStarted>()
            .Select(e => e.Context)
            .ToList();
        Assert.Equal(result.Games.Count, contexts.Count);

        int seatOne = 0, seatTwo = 0;
        for (int g = 0; g < contexts.Count; g++)
        {
            var context = contexts[g];
            Assert.Equal(g + 1, context.GameNumber);

            // Entering scores == the running tally of prior games' awards.
            Assert.Equal(seatOne, context.SeatOneScore);
            Assert.Equal(seatTwo, context.SeatTwoScore);

            // Crawford flag == the value the substrate stamped into this game's
            // first transcript entry (the opening play's match snapshot).
            var opening = Assert.IsType<PlayTranscriptEntry>(result.Games[g].Transcript.Entries[0]);
            Assert.Equal(opening.State.Match.IsCrawford, context.IsCrawford);

            if (result.Games[g].Winner == MatchSeat.One) seatOne += result.Games[g].Result.Points;
            else seatTwo += result.Games[g].Result.Points;
        }

        // The boundary is genuinely crossed: a Crawford game exists and a later
        // game runs post-Crawford (flag back to false). Guards the seed against
        // silently stopping to exercise the transition.
        int crawford = contexts.FindIndex(c => c.IsCrawford);
        Assert.InRange(crawford, 0, contexts.Count - 2);
        Assert.False(contexts[crawford + 1].IsCrawford);
        Assert.Single(contexts, c => c.IsCrawford);   // exactly one Crawford game
    }

    /// <summary>
    /// Seed whose length-7 <see cref="RunSeededMatch"/> reaches 6–x, plays a
    /// Crawford game the trailer wins, then a post-Crawford game — verified by
    /// the crossing assertions in
    /// <see cref="OnGameStarted_CrawfordFlag_CrossesTheBoundary_SeededMatch"/>.
    /// </summary>
    private const int CrawfordCrossingSeed = 1;

    // ── The arc's proof: seeded full matches, end to end ──────────

    [Theory]
    [InlineData(5, 42)]
    [InlineData(15, 7)]
    public async Task FullMatch_SeededBots_RunsToCompletion_Consistently(int matchLength, int seed)
    {
        var result = await RunSeededMatch(matchLength, seed);

        // Match resolved.
        Assert.NotNull(result.Winner);
        int winnerScore = result.Winner == MatchSeat.One ? result.SeatOneScore : result.SeatTwoScore;
        int loserScore = result.Winner == MatchSeat.One ? result.SeatTwoScore : result.SeatOneScore;
        Assert.True(winnerScore >= matchLength);
        Assert.True(loserScore < matchLength);

        // Seat scores are exactly the sum of the per-game awards.
        Assert.Equal(
            result.Games.Where(g => g.Winner == MatchSeat.One).Sum(g => g.Result.Points),
            result.SeatOneScore);
        Assert.Equal(
            result.Games.Where(g => g.Winner == MatchSeat.Two).Sum(g => g.Result.Points),
            result.SeatTwoScore);

        foreach (var game in result.Games)
        {
            var entries = game.Transcript.Entries;

            // Shape: opens with a play, ends with the game-end event.
            Assert.IsType<PlayTranscriptEntry>(entries[0]);
            var ended = Assert.IsType<GameEndedTranscriptEntry>(entries[^1]);
            Assert.Equal(game.Result, ended.Result);
            Assert.Equal(1, entries.Count(e => e is GameEndedTranscriptEntry));

            // Doubles-only cube record: entries come strictly as offer→response pairs.
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] is not CubeTranscriptEntry cube) continue;
                if (cube.Action == CubeAction.Double)
                {
                    var response = Assert.IsType<CubeTranscriptEntry>(entries[i + 1]);
                    Assert.True(response.Action is CubeAction.Take or CubeAction.Pass);
                    i++;   // consume the response
                }
                else
                {
                    Assert.Fail($"unpaired cube entry: {cube.Action}");
                }
            }
        }
    }

    [Fact]
    public async Task FullMatch_SameSeeds_IsFullyReproducible()
    {
        var first = await RunSeededMatch(matchLength: 9, seed: 21);
        var second = await RunSeededMatch(matchLength: 9, seed: 21);

        Assert.Equal(first.Winner, second.Winner);
        Assert.Equal(first.SeatOneScore, second.SeatOneScore);
        Assert.Equal(first.SeatTwoScore, second.SeatTwoScore);
        Assert.Equal(first.Games.Count, second.Games.Count);

        for (int g = 0; g < first.Games.Count; g++)
        {
            var a = first.Games[g].Transcript.Entries;
            var b = second.Games[g].Transcript.Entries;
            Assert.Equal(first.Games[g].Winner, second.Games[g].Winner);
            Assert.Equal(first.Games[g].Result, second.Games[g].Result);
            Assert.Equal(a.Count, b.Count);

            for (int i = 0; i < a.Count; i++)
            {
                Assert.Equal(a[i].GetType(), b[i].GetType());
                switch (a[i])
                {
                    case PlayTranscriptEntry pa:
                        var pb = (PlayTranscriptEntry)b[i];
                        Assert.Equal(pa.Die1, pb.Die1);
                        Assert.Equal(pa.Die2, pb.Die2);
                        Assert.Equal(pa.ChosenPlay, pb.ChosenPlay);
                        break;
                    case CubeTranscriptEntry ca:
                        Assert.Equal(ca.Action, ((CubeTranscriptEntry)b[i]).Action);
                        break;
                }
            }
        }
    }

    private static Task<MatchResult> RunSeededMatch(int matchLength, int seed, IMatchObserver? observer = null)
    {
        var runner = new MatchRunner(new SeededDiceSource(seed));
        var one = Participant(new RandomPlayAgent(seed + 1), CubeAgents.DoubleOnceTake());
        var two = Participant(new RandomPlayAgent(seed + 2), CubeAgents.DoubleOnceTake());
        return runner.RunMatchAsync(one, two, matchLength, observer: observer);
    }
}
