namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;

public class CubeAgentContractTests
{
    private sealed class FixedCubeAgent : ICubeAgent
    {
        public CubeAction Offer { get; init; } = CubeAction.NoDouble;
        public CubeAction Response { get; init; } = CubeAction.Take;

        public ValueTask<CubeAction> ChooseOfferAsync(
            GameState state, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Offer);
        }

        public ValueTask<CubeAction> ChooseResponseAsync(
            GameState state, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Response);
        }
    }

    [Fact]
    public async Task ChooseOffer_ReturnsConfiguredOfferSideAction()
    {
        var match = MatchState.NewMatch(7);
        var game = GameState.NewGame(match);
        var agent = new FixedCubeAgent { Offer = CubeAction.Double };

        var offer = await agent.ChooseOfferAsync(game);

        Assert.Equal(CubeAction.Double, offer);
    }

    [Fact]
    public async Task ChooseResponse_ReturnsConfiguredResponseSideAction()
    {
        var match = MatchState.NewMatch(7);
        var game = GameState.NewGame(match);
        var agent = new FixedCubeAgent { Response = CubeAction.Pass };

        var response = await agent.ChooseResponseAsync(game);

        Assert.Equal(CubeAction.Pass, response);
    }

    [Fact]
    public async Task BothMethods_HonourCancellation()
    {
        var match = MatchState.NewMatch(7);
        var game = GameState.NewGame(match);
        var agent = new FixedCubeAgent();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await agent.ChooseOfferAsync(game, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await agent.ChooseResponseAsync(game, cts.Token));
    }
}
