namespace BgGame_Lib.Tests;

using BgMoveGen;

public class PlayAgentContractTests
{
    private sealed class FixedPlayAgent : IPlayAgent
    {
        private readonly Play _play;
        public int CallCount { get; private set; }

        public FixedPlayAgent(Play play) => _play = play;

        public ValueTask<Play> ChoosePlayAsync(
            GameState state, int die1, int die2, CancellationToken cancellationToken = default)
        {
            CallCount++;
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(_play);
        }
    }

    [Fact]
    public async Task FixedAgent_ReturnsConfiguredPlay()
    {
        var match = MatchState.NewMatch(7);
        var game = GameState.NewGame(match);

        var legal = MoveGenerator.GeneratePlays(game.Board, 3, 1);
        var expected = legal[0];
        var agent = new FixedPlayAgent(expected);

        var actual = await agent.ChoosePlayAsync(game, 3, 1);

        Assert.Equal(expected.Count, actual.Count);
        Assert.Equal(1, agent.CallCount);
    }

    [Fact]
    public async Task FixedAgent_HonoursCancellationToken()
    {
        var match = MatchState.NewMatch(7);
        var game = GameState.NewGame(match);
        var agent = new FixedPlayAgent(new Play());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await agent.ChoosePlayAsync(game, 3, 1, cts.Token));
    }

    [Fact]
    public async Task ValueTaskCompletesSynchronously_NoTaskAllocation()
    {
        // Verify the contract enables synchronous completion: in-memory agents
        // should be able to return ValueTask<Play>.FromResult without forcing
        // an async state machine.
        var agent = new FixedPlayAgent(new Play());
        var match = MatchState.NewMatch(7);
        var game = GameState.NewGame(match);

        var task = agent.ChoosePlayAsync(game, 3, 1);
        Assert.True(task.IsCompletedSuccessfully);
        await task;
    }
}
