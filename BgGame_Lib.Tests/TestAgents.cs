namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;
using BgMoveGen;

/// <summary>
/// In-proc baseline bots for MatchRunner tests. Test-project-only by design:
/// the public reference bot ships with BgTournament (epic Arc 2).
/// </summary>
internal sealed class RandomPlayAgent(int seed) : IPlayAgent
{
    private readonly Random _random = new(seed);

    public ValueTask<Play> ChoosePlayAsync(
        GameState state, int die1, int die2, CancellationToken cancellationToken = default)
    {
        var plays = MoveGenerator.GeneratePlays(state.Board, die1, die2);
        return ValueTask.FromResult(plays[_random.Next(plays.Count)]);
    }
}

/// <summary>Always picks the first legal play — fully deterministic, no RNG.</summary>
internal sealed class FirstPlayAgent : IPlayAgent
{
    public ValueTask<Play> ChoosePlayAsync(
        GameState state, int die1, int die2, CancellationToken cancellationToken = default)
    {
        var plays = MoveGenerator.GeneratePlays(state.Board, die1, die2);
        return ValueTask.FromResult(plays[0]);
    }
}

/// <summary>Delegates the choice to a lambda — for spies, bad agents, and cancellation triggers.</summary>
internal sealed class DelegatePlayAgent(Func<GameState, int, int, Play> choose) : IPlayAgent
{
    public ValueTask<Play> ChoosePlayAsync(
        GameState state, int die1, int die2, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(choose(state, die1, die2));
}

/// <summary>Delegates both cube decisions to lambdas.</summary>
internal sealed class DelegateCubeAgent(
    Func<GameState, CubeAction> offer,
    Func<GameState, CubeAction> response) : ICubeAgent
{
    public ValueTask<CubeAction> ChooseOfferAsync(
        GameState state, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(offer(state));

    public ValueTask<CubeAction> ChooseResponseAsync(
        GameState state, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(response(state));
}

/// <summary>Canned cube-agent behaviors.</summary>
internal static class CubeAgents
{
    /// <summary>Never doubles; takes if somehow asked.</summary>
    public static DelegateCubeAgent Never() =>
        new(_ => CubeAction.NoDouble, _ => CubeAction.Take);

    /// <summary>Doubles at every open window; passes every double.</summary>
    public static DelegateCubeAgent AlwaysDoubleAlwaysPass() =>
        new(_ => CubeAction.Double, _ => CubeAction.Pass);

    /// <summary>Doubles at every open window; takes every double.</summary>
    public static DelegateCubeAgent AlwaysDoubleAlwaysTake() =>
        new(_ => CubeAction.Double, _ => CubeAction.Take);

    /// <summary>
    /// Doubles only from a centered cube (so the cube tops out at 2 per game —
    /// no redouble ping-pong); takes every double.
    /// </summary>
    public static DelegateCubeAgent DoubleOnceTake() =>
        new(s => s.CubeSize == 1 ? CubeAction.Double : CubeAction.NoDouble, _ => CubeAction.Take);
}
