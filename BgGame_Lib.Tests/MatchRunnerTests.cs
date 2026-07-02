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

    private static Task<MatchResult> RunSeededMatch(int matchLength, int seed)
    {
        var runner = new MatchRunner(new SeededDiceSource(seed));
        var one = Participant(new RandomPlayAgent(seed + 1), CubeAgents.DoubleOnceTake());
        var two = Participant(new RandomPlayAgent(seed + 2), CubeAgents.DoubleOnceTake());
        return runner.RunMatchAsync(one, two, matchLength);
    }
}
