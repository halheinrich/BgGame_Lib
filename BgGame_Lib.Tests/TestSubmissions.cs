namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;

/// <summary>
/// Scored play submissions for the folds' tests, built the one way production
/// builds them: through <see cref="PlaySubmission.Score"/>, from a decision
/// whose played candidate has the stated error under the equity ranking. A
/// fold's test states the error it folds; the correctness follows from it,
/// as it does in production, and the key is the decision's own.
/// </summary>
internal static class TestSubmissions
{
    /// <summary>The opening 3-1's 8/5 6/5 — the best play, played when the error is 0.</summary>
    private static Play Best => [new(8, 5), new(6, 5)];

    /// <summary>The opening 3-1's 13/10 6/5 — played, at the stated error, otherwise.</summary>
    private static Play Other => [new(13, 10), new(6, 5)];

    /// <summary>
    /// A scored submission whose error under <see cref="PlayRanking.Equity"/>
    /// is <paramref name="error"/>: correct exactly when it is 0. Its problem
    /// is a checker play from <paramref name="position"/> (by default the
    /// standard start, 7-away/7-away, the cube centred on 1) with
    /// <paramref name="dice"/> (by default 3-1), so its key is that
    /// record's — a test naming a key names the problem that spells it.
    /// </summary>
    /// <param name="error">The played candidate's error; 0 plays the best.</param>
    /// <param name="position">
    /// The problem's position. Its board must be the standard start: the
    /// candidates are the opening's, and a record holds only candidates
    /// valid from its own board.
    /// </param>
    /// <param name="dice">The problem's roll.</param>
    internal static SubmittedPlay Play(
        double error, PositionData? position = null, IReadOnlyList<int>? dice = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(error);
        var decision = TestRecords.CheckerPlay(
            position: position,
            decision: TestRecords.CheckerPlayData(
                dice: dice,
                plays:
                [
                    TestRecords.Candidate(play: Best, equity: 0.0),
                    TestRecords.Candidate(play: Other, equity: -error),
                ]));

        var submission = PlaySubmission.Score(error == 0.0 ? Best : Other, decision, PlayRanking.Equity);
        return submission.TryGetScored(out var scored)
            ? scored
            : throw new InvalidOperationException($"The fixture play did not score: {submission.Kind}.");
    }
}
