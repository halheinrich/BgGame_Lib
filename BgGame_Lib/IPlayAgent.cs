namespace BgGame_Lib;

using BgMoveGen;

/// <summary>
/// Decision-maker for play turns: given the current state and dice, choose a Play.
///
/// Implementations span:
///   • Quiz-grader (passive, scores against a fixture) — in BgQuiz_Blazor.
///   • Human-via-clicks (drives a <see cref="MoveEntryState"/> from UI input) — in BgQuiz_Blazor.
///   • Replay (reads from a recorded sequence) — in BgQuiz_Blazor / replay tooling.
///   • Online evaluator (BgRLEngine inference, network bot) — wraps a network call.
///
/// The signature is async (<see cref="ValueTask{TResult}"/>) so online evaluators
/// don't have to choose between blocking and <c>Task.Run</c>. In-memory agents
/// pay no measurable overhead — <see cref="ValueTask{TResult}"/> avoids the
/// Task allocation when the result is available synchronously.
///
/// Contract: the returned <see cref="Play"/> must be one of the legal plays for
/// the supplied state and dice (i.e., present in <see cref="MoveGenerator.GeneratePlays"/>'s
/// output). <see cref="Referee.ApplyPlay"/> validates this.
/// </summary>
public interface IPlayAgent
{
    /// <summary>
    /// Choose a Play given the current state and dice.
    /// </summary>
    /// <param name="state">The current game state. Treat as input only — do not mutate.</param>
    /// <param name="die1">First die (1–6).</param>
    /// <param name="die2">Second die (1–6).</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    ValueTask<Play> ChoosePlayAsync(
        GameState state,
        int die1,
        int die2,
        CancellationToken cancellationToken = default);
}
