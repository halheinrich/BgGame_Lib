namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;

/// <summary>
/// Scored play submissions for the folds' tests, built the one way production
/// builds them: through <see cref="PlaySubmission.Score"/>, from a decision
/// whose played candidate has the stated error under the equity ranking. A
/// fold's test states the error it folds; the correctness follows from it,
/// as it does in production.
/// </summary>
internal static class TestSubmissions
{
    /// <summary>The opening 3-1's 8/5 6/5 — the best play, played when the error is 0.</summary>
    private static Play Best => [new(8, 5), new(6, 5)];

    /// <summary>The opening 3-1's 13/10 6/5 — played, at the stated error, otherwise.</summary>
    private static Play Other => [new(13, 10), new(6, 5)];

    /// <summary>
    /// A scored submission for the problem <paramref name="key"/> (or the
    /// no-key rung) whose error under <see cref="PlayRanking.Equity"/> is
    /// <paramref name="error"/>: correct exactly when it is 0.
    /// </summary>
    internal static SubmittedPlay Play(ProblemKey? key, double error)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(error);
        var decision = TestRecords.CheckerPlay(decision: TestRecords.CheckerPlayData(plays:
        [
            TestRecords.Candidate(play: Best, equity: 0.0),
            TestRecords.Candidate(play: Other, equity: -error),
        ]));

        var submission = PlaySubmission.Score(key, error == 0.0 ? Best : Other, decision, PlayRanking.Equity);
        return submission.TryGetScored(out var scored)
            ? scored
            : throw new InvalidOperationException($"The fixture play did not score: {submission.Kind}.");
    }
}
