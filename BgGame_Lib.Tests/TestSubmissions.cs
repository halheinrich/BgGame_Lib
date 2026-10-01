namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;

/// <summary>
/// Scored submissions for the folds' tests, built the one way production
/// builds them: a play through <see cref="PlaySubmission.Score"/>, a cube
/// answer through <see cref="SubmittedCubeAnswer.Score"/>. A fold's test
/// states the facts the producer derives a result from — a play's error, a
/// cube decision's two equities — and the cost, the correctness and the key
/// follow from them, as they do in production.
/// </summary>
internal static class TestSubmissions
{
    /// <summary>
    /// A loss of 2^-15, 0.0000305…: it shows as <c>0.0000</c> and so counts as
    /// zero, while twice it, 2^-14 = 0.0000610…, shows as <c>0.0001</c> and does
    /// not. A power of two, so every sum and difference the fixtures take of it
    /// near the cash is exact.
    /// </summary>
    internal const double ShowsAsZero = 1.0 / 32768;

    /// <summary>The opening 3-1's 8/5 6/5 — the best play, played when the error is 0.</summary>
    private static Play Best => [new(8, 5), new(6, 5)];

    /// <summary>The opening 3-1's 13/10 6/5 — played, at the stated error, otherwise.</summary>
    private static Play Other => [new(13, 10), new(6, 5)];

    /// <summary>
    /// A scored submission whose error under <see cref="PlayRanking.Equity"/>
    /// is <paramref name="error"/>, exactly: correct exactly when it counts as
    /// zero. Its problem is a checker play from <paramref name="position"/>
    /// (by default the standard start, 7-away/7-away, the cube centred on 1)
    /// with <paramref name="dice"/> (by default 3-1), so its key is that
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

    /// <summary>
    /// A cube decision whose no-double equity (N) and double/take equity (T)
    /// are the ones given, the truth and every answer's cost following from
    /// them. Its position is <paramref name="position"/>, by default
    /// <see cref="TestRecords.Cube"/>'s: a short race at 0-0 in a 7-point
    /// match, the cube centred, where gammons are possible.
    /// </summary>
    internal static CubeDecision CubeRecord(
        double noDoubleEquity, double doubleTakeEquity, PositionData? position = null) =>
        TestRecords.Cube(
            position: position,
            decision: TestRecords.CubeData(noDoubleEquity: noDoubleEquity, doubleTakeEquity: doubleTakeEquity));

    /// <summary>
    /// <paramref name="answer"/>, scored at
    /// <see cref="CubeRecord(double, double, PositionData?)"/> of the same
    /// arguments: its key, truth and cost are that record's.
    /// </summary>
    internal static SubmittedCubeAnswer Cube(
        CubeAnswer answer, double noDoubleEquity, double doubleTakeEquity, PositionData? position = null) =>
        SubmittedCubeAnswer.Score(answer, CubeRecord(noDoubleEquity, doubleTakeEquity, position));

    /// <summary>
    /// A position where gammons are not possible: the standard start in money
    /// under the Jacoby rule, the cube centred
    /// (<see cref="BgDataTypes_Lib.CubeDecision.GammonsPossible"/>). The fourth
    /// answer reads No double / Pass here, and §3's conventions do not apply.
    /// </summary>
    internal static PositionData NoGammons =>
        TestRecords.Position(session: TestRecords.MoneySession(isJacoby: true));

    /// <summary>
    /// The whole-versus-parts case: Too good when they'd take, at a Double /
    /// Take truth (N +0.5) one <see cref="ShowsAsZero"/> below the cash. Each
    /// part is 1 − T = <see cref="ShowsAsZero"/>, which counts as zero; the
    /// whole, 2(1 − T), does not. Gammons are possible, so the convention
    /// charges both parts.
    /// </summary>
    internal static SubmittedCubeAnswer PartsZeroWholeNot =>
        Cube(CubeAnswer.NoDoublePass, noDoubleEquity: 0.5, doubleTakeEquity: 1.0 - ShowsAsZero);
}
