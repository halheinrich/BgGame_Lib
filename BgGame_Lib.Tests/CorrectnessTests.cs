namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;

/// <summary>
/// Correctness is the zero rule, in one place: a checker play, each part of a
/// cube answer and each whole cube answer is correct exactly when its loss
/// counts as zero — when it displays as <c>0.0000</c>, below 0.00005
/// (SPEC-scoring §2a and §3, amended 2026-10-01; halheinrich/backgammon#202).
/// </summary>
public class CorrectnessTests
{
    /// <summary>
    /// The double nearest 0.00005: just above it, so it shows as <c>0.0001</c>
    /// and does not count as zero. "At 0.00005" below means this value.
    /// </summary>
    private const double AtTheBoundary = 0.00005;

    /// <summary>The loss <paramref name="step"/> doubles from <see cref="AtTheBoundary"/>: −1 just below, 0 at, +1 just above.</summary>
    private static double Near(int step) => step switch
    {
        -1 => Math.BitDecrement(AtTheBoundary),
        0 => AtTheBoundary,
        1 => Math.BitIncrement(AtTheBoundary),
        _ => throw new ArgumentOutOfRangeException(nameof(step)),
    };

    [Theory]
    [InlineData(-1, "0.0000")]
    [InlineData(0, "0.0001")]
    [InlineData(1, "0.0001")]
    public void TheBoundaryFixtures_ShowAsTheirNamesSay(int step, string shown)
    {
        // So a verdict below can never pass on a mis-built boundary.
        Assert.Equal(shown, EquityDisplay.FormatLoss(Near(step)));
    }

    // -----------------------------------------------------------------------
    //  The one place
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0.0)]
    [InlineData(1e-9)]
    [InlineData(0.0000499)]
    [InlineData(0.00005)]
    [InlineData(0.0000501)]
    [InlineData(0.0001)]
    [InlineData(1.0267)]
    public void Correctness_IsTheProducersZeroRule(double loss)
    {
        Assert.Equal(EquityDisplay.CountsAsZero(loss), Correctness.IsCorrect(loss));
    }

    // -----------------------------------------------------------------------
    //  A checker play
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public void ACheckerPlay_IsCorrectExactlyBelowTheBoundary(int step, bool correct)
    {
        var play = TestSubmissions.Play(Near(step));

        Assert.Equal(Near(step), play.EquityLoss);     // the error stays exact
        Assert.Equal(correct, play.IsCorrect);
    }

    // -----------------------------------------------------------------------
    //  The doubling part, and a whole answer
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public void TheDoublingPart_AndTheWholeAnswer_AreCorrectExactlyBelowTheBoundary(int step, bool correct)
    {
        // No double at a Double / Take truth with N 0 and T the loss: its
        // doubling part is T − N = T, exactly, and its take part 0, so the
        // whole is that loss too.
        double loss = Near(step);
        var noDouble = TestSubmissions.Cube(CubeAnswer.NoDouble, noDoubleEquity: 0.0, doubleTakeEquity: loss);

        Assert.Equal(CubeAnswer.DoubleTake, noDouble.BestAnswer);
        Assert.Equal(loss, noDouble.Cost.DoublingPart);
        Assert.Equal(loss, noDouble.Cost.Total);
        Assert.Equal(correct, noDouble.IsDoublingPartCorrect);
        Assert.Equal(correct, noDouble.IsCorrect);
        Assert.True(noDouble.IsTakePartCorrect);
    }

    // -----------------------------------------------------------------------
    //  The take part, and a whole answer
    // -----------------------------------------------------------------------

    /// <summary>
    /// The double/take equity <paramref name="step"/> doubles from +1.00005.
    /// A take part near the boundary is T − 1, exact, so it moves in steps of
    /// 2^-52 and can never be the double nearest 0.00005. +1.00005 gives the
    /// nearest it comes, 0.0000500000000001…, just above the boundary as that
    /// double is; the step below gives 0.0000499999999998…, just below.
    /// </summary>
    private static double TakeEquityNear(int step) => step switch
    {
        -1 => Math.BitDecrement(1.00005),
        0 => 1.00005,
        1 => Math.BitIncrement(1.00005),
        _ => throw new ArgumentOutOfRangeException(nameof(step)),
    };

    [Theory]
    [InlineData(-1, true, "0.0000")]
    [InlineData(0, false, "0.0001")]
    [InlineData(1, false, "0.0001")]
    public void TheTakePart_AndTheWholeAnswer_AreCorrectExactlyBelowTheBoundary(
        int step, bool correct, string shown)
    {
        // Double / Take at a Double / Pass truth: doubling is right, so the
        // doubling part is 0, and the take part is T − 1, so the whole is too.
        double t = TakeEquityNear(step);
        var doubleTake = TestSubmissions.Cube(CubeAnswer.DoubleTake, noDoubleEquity: 0.5, doubleTakeEquity: t);

        Assert.Equal(CubeAnswer.DoublePass, doubleTake.BestAnswer);
        Assert.Equal(0.0, doubleTake.Cost.DoublingPart);
        Assert.Equal(t - 1.0, doubleTake.Cost.TakePart);
        Assert.Equal(shown, EquityDisplay.FormatLoss(doubleTake.Cost.TakePart));
        Assert.Equal(correct, doubleTake.IsTakePartCorrect);
        Assert.Equal(correct, doubleTake.IsCorrect);
        Assert.True(doubleTake.IsDoublingPartCorrect);
    }

    // -----------------------------------------------------------------------
    //  The whole versus the parts
    // -----------------------------------------------------------------------

    [Fact]
    public void TwoPartsThatEachCountAsZero_AddUpToAWholeThatDoesNot()
    {
        // Too good when they'd take, one 2^-15 below the cash: each part is
        // 2^-15 (0.0000), the whole 2^-14 (0.0001). The answer is wrong in the
        // session's Total, judged on the whole, while each half is right in
        // the lifetime fold, judged one by one (SPEC-scoring §3, 2026-10-01).
        var answer = TestSubmissions.PartsZeroWholeNot;

        Assert.Equal(TestSubmissions.ShowsAsZero, answer.Cost.DoublingPart);
        Assert.Equal(TestSubmissions.ShowsAsZero, answer.Cost.TakePart);
        Assert.Equal("0.0000", EquityDisplay.FormatLoss(answer.Cost.DoublingPart));
        Assert.Equal("0.0001", EquityDisplay.FormatLoss(answer.Cost.Total));
        Assert.True(answer.IsDoublingPartCorrect);
        Assert.True(answer.IsTakePartCorrect);
        Assert.False(answer.IsCorrect);

        var score = QuizScore.Empty.Plus(answer);

        Assert.Equal(new ScoreSegment(1, 1, TestSubmissions.ShowsAsZero), score.DoubleDecisions);
        Assert.Equal(new ScoreSegment(1, 1, TestSubmissions.ShowsAsZero), score.TakeDecisions);
        Assert.Equal(new ScoreSegment(1, 0, 2 * TestSubmissions.ShowsAsZero), score.Total);

        var lifetime = ProblemStats.From(answer, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(new ScoreSegment(2, 2, 2 * TestSubmissions.ShowsAsZero), lifetime.Tally);
    }

    [Fact]
    public void TheVerdictChangesNoStoredNumber()
    {
        // A play whose error shows as 0.0000 is correct and keeps its error;
        // the session and the lifetime record accumulate the exact loss.
        const double error = 1e-9;
        var play = TestSubmissions.Play(error);

        Assert.True(play.IsCorrect);
        Assert.Equal(new ScoreSegment(1, 1, error), QuizScore.Empty.Plus(play).PlayDecisions);
        Assert.Equal(
            new ScoreSegment(1, 1, error),
            ProblemStats.From(play, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero)).Tally);
    }
}
