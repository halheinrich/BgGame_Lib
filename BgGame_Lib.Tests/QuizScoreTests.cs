namespace BgGame_Lib.Tests;

using System.Reflection;
using BgDataTypes_Lib;

/// <summary>
/// The session score's rows, Play, Double and Take, and its Total, as
/// SPEC-scoring §3's 2026-10-01 amendment rules them. Every submission here
/// is scored the way production scores it (<see cref="TestSubmissions"/>), so
/// a fold's verdicts are the submission's own.
/// </summary>
public class QuizScoreTests
{
    // QuizScore never reads a submission's key: session scoring is
    // deliberately key-independent.

    private static SubmittedPlay Play(double equityLoss) =>
        TestSubmissions.Play(equityLoss);

    /// <summary>
    /// <paramref name="answer"/> at a Double / Take truth (N +0.30, T +0.60),
    /// gammons possible. Its parts: No double (0.30, 0); Double / Take (0, 0);
    /// Double / Pass (0, 0.40); Too good (0.40, 0.40).
    /// </summary>
    private static SubmittedCubeAnswer AtDoubleTake(CubeAnswer answer) =>
        TestSubmissions.Cube(answer, noDoubleEquity: 0.30, doubleTakeEquity: 0.60);

    public static TheoryData<CubeAnswer> EveryAnswer => new(Enum.GetValues<CubeAnswer>());

    [Fact]
    public void Empty_AllRowsAndTotalAreEmpty()
    {
        var s = QuizScore.Empty;
        Assert.Equal(ScoreSegment.Empty, s.PlayDecisions);
        Assert.Equal(ScoreSegment.Empty, s.DoubleDecisions);
        Assert.Equal(ScoreSegment.Empty, s.TakeDecisions);
        Assert.Equal(ScoreSegment.Empty, s.Total);
    }

    // -----------------------------------------------------------------------
    //  The Play row
    // -----------------------------------------------------------------------

    [Fact]
    public void PlusPlay_CorrectPlay_IncrementsPlaySubmittedAndCorrect()
    {
        var s = QuizScore.Empty.Plus(Play(equityLoss: 0.0));

        Assert.Equal(new ScoreSegment(1, 1, 0.0), s.PlayDecisions);
    }

    [Fact]
    public void PlusPlay_IncorrectPlay_IncrementsSubmittedNotCorrect_AccumulatesEquityLoss()
    {
        var s = QuizScore.Empty
            .Plus(Play(equityLoss: 0.025))
            .Plus(Play(equityLoss: 0.075));

        Assert.Equal(2, s.PlayDecisions.Submitted);
        Assert.Equal(0, s.PlayDecisions.Correct);
        Assert.Equal(0.10, s.PlayDecisions.TotalEquityLoss, precision: 9);
    }

    [Fact]
    public void PlusPlay_LeavesTheCubeRowsUntouched()
    {
        var s = QuizScore.Empty.Plus(Play(equityLoss: 0.05));

        Assert.Equal(ScoreSegment.Empty, s.DoubleDecisions);
        Assert.Equal(ScoreSegment.Empty, s.TakeDecisions);
    }

    // -----------------------------------------------------------------------
    //  The Double row: every cube answer
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(EveryAnswer))]
    public void PlusCube_EveryAnswer_AddsOneDoubleDecision_ItsDoublingPart(CubeAnswer answer)
    {
        var cube = AtDoubleTake(answer);

        var s = QuizScore.Empty.Plus(cube);

        Assert.Equal(
            new ScoreSegment(1, cube.IsDoublingPartCorrect ? 1 : 0, cube.Cost.DoublingPart),
            s.DoubleDecisions);
        Assert.Equal(ScoreSegment.Empty, s.PlayDecisions);
    }

    [Theory]
    [InlineData(CubeAnswer.NoDouble, false, 0.30)]
    [InlineData(CubeAnswer.DoubleTake, true, 0.0)]
    [InlineData(CubeAnswer.DoublePass, true, 0.0)]
    [InlineData(CubeAnswer.NoDoublePass, false, 0.40)]
    public void PlusCube_TheDoubleRow_JudgesTheDoublingPart(CubeAnswer answer, bool correct, double loss)
    {
        // At the Double / Take truth: doubling is right, so both doubling
        // answers' doubling parts are 0; not doubling misses T − N = 0.30,
        // and Too good when they'd take is charged 1 − T = 0.40 here.
        var s = QuizScore.Empty.Plus(AtDoubleTake(answer));

        Assert.Equal(correct ? 1 : 0, s.DoubleDecisions.Correct);
        Assert.Equal(loss, s.DoubleDecisions.TotalEquityLoss, precision: 12);
    }

    // -----------------------------------------------------------------------
    //  The Take row: every answer but No double
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(EveryAnswer))]
    public void PlusCube_TheTakeRow_GainsEveryAnswerThatCommitsToAResponse(CubeAnswer answer)
    {
        var cube = AtDoubleTake(answer);

        var s = QuizScore.Empty.Plus(cube);

        if (answer == CubeAnswer.NoDouble)
        {
            Assert.Equal(ScoreSegment.Empty, s.TakeDecisions);
        }
        else
        {
            Assert.Equal(
                new ScoreSegment(1, cube.IsTakePartCorrect ? 1 : 0, cube.Cost.TakePart),
                s.TakeDecisions);
        }
    }

    [Fact]
    public void PlusCube_NoDouble_IsLeftOutOfTheTakeRow_EvenWhereTheTakeItImpliesIsWrong()
    {
        // At a Double / Pass truth No double's implied take would be wrong;
        // it is never charged, so the Take row does not see it at all.
        var noDouble = TestSubmissions.Cube(CubeAnswer.NoDouble, noDoubleEquity: 0.50, doubleTakeEquity: 1.20);

        var s = QuizScore.Empty.Plus(noDouble);

        Assert.Equal(ScoreSegment.Empty, s.TakeDecisions);
        Assert.Equal(new ScoreSegment(1, 0, noDouble.Cost.DoublingPart), s.DoubleDecisions);
    }

    // -----------------------------------------------------------------------
    //  The Total: each play and each answer once
    // -----------------------------------------------------------------------

    [Fact]
    public void Total_CountsEachPlayAndEachCubeAnswerOnce_NotTheSumOfTheRows()
    {
        // One correct play and all four answers at the Double / Take truth.
        // The rows count 1 + 4 + 3 = 8 decisions; the Total 1 + 4 = 5. Correct
        // in the Total: the play and Double / Take, the one answer whose whole
        // cost is 0 — 2, where the rows' correct counts sum to 1 + 2 + 1 = 4.
        var s = Enum.GetValues<CubeAnswer>()
            .Aggregate(QuizScore.Empty.Plus(Play(equityLoss: 0.0)), (score, a) => score.Plus(AtDoubleTake(a)));

        Assert.Equal(new ScoreSegment(1, 1, 0.0), s.PlayDecisions);
        Assert.Equal(4, s.DoubleDecisions.Submitted);
        Assert.Equal(2, s.DoubleDecisions.Correct);
        Assert.Equal(3, s.TakeDecisions.Submitted);
        Assert.Equal(1, s.TakeDecisions.Correct);

        Assert.Equal(s.PlayDecisions.Submitted + s.DoubleDecisions.Submitted, s.Total.Submitted);
        Assert.Equal(5, s.Total.Submitted);
        Assert.Equal(2, s.Total.Correct);
        Assert.NotEqual(
            s.PlayDecisions.Submitted + s.DoubleDecisions.Submitted + s.TakeDecisions.Submitted,
            s.Total.Submitted);
        Assert.NotEqual(
            s.PlayDecisions.Correct + s.DoubleDecisions.Correct + s.TakeDecisions.Correct,
            s.Total.Correct);
    }

    [Fact]
    public void Total_ItsLossIsTheSumOfTheRowsLosses()
    {
        var s = QuizScore.Empty
            .Plus(Play(equityLoss: 0.02))
            .Plus(AtDoubleTake(CubeAnswer.NoDoublePass))
            .Plus(AtDoubleTake(CubeAnswer.NoDouble));

        Assert.Equal(
            s.PlayDecisions.TotalEquityLoss + s.DoubleDecisions.TotalEquityLoss + s.TakeDecisions.TotalEquityLoss,
            s.Total.TotalEquityLoss);
        Assert.Equal(0.02 + 0.80 + 0.30, s.Total.TotalEquityLoss, precision: 12);
    }

    [Fact]
    public void Total_JudgesACubeAnswerOnItsWholeCost_NotOnItsPartsVerdicts()
    {
        // Both parts count as zero, the whole does not: right in both rows,
        // wrong in the Total.
        var s = QuizScore.Empty.Plus(TestSubmissions.PartsZeroWholeNot);

        Assert.Equal(1, s.DoubleDecisions.Correct);
        Assert.Equal(1, s.TakeDecisions.Correct);
        Assert.Equal(1, s.Total.Submitted);
        Assert.Equal(0, s.Total.Correct);
    }

    [Fact]
    public void Total_IsDerived_FoldingAnAnswerMovesIt()
    {
        var before = QuizScore.Empty.Plus(Play(equityLoss: 0.04));
        var after = before.Plus(AtDoubleTake(CubeAnswer.DoubleTake));

        Assert.Equal(new ScoreSegment(1, 0, 0.04), before.Total);
        Assert.Equal(new ScoreSegment(2, 1, 0.04), after.Total);
    }

    // -----------------------------------------------------------------------
    //  Immutable, factory-only, value-equal
    // -----------------------------------------------------------------------

    [Fact]
    public void Plus_ReturnsANewScore_TheOriginalUnchanged()
    {
        var original = QuizScore.Empty;
        var updated = original.Plus(Play(equityLoss: 0.05)).Plus(AtDoubleTake(CubeAnswer.DoubleTake));

        Assert.NotSame(original, updated);
        Assert.Equal(ScoreSegment.Empty, original.Total);
        Assert.Equal(2, updated.Total.Submitted);
    }

    [Fact]
    public void QuizScore_HasNoPublicConstructor_AndNoWith()
    {
        // The Total's correct count is held beside the rows; a public
        // constructor or a record's `with` could state one the folds did not.
        Assert.Empty(typeof(QuizScore).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(QuizScore).GetMethod("<Clone>$"));
        Assert.All(
            typeof(QuizScore).GetProperties(BindingFlags.Public | BindingFlags.Instance),
            p => Assert.Null(p.SetMethod));
    }

    [Fact]
    public void Equality_TheSameFolds_AreTheSameScore()
    {
        QuizScore Fold() => QuizScore.Empty
            .Plus(Play(equityLoss: 0.02))
            .Plus(AtDoubleTake(CubeAnswer.DoublePass))
            .Plus(TestSubmissions.PartsZeroWholeNot);

        var a = Fold();
        var b = Fold();

        Assert.NotSame(a, b);
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equality_ADifferentRow_IsADifferentScore()
    {
        var a = QuizScore.Empty.Plus(AtDoubleTake(CubeAnswer.DoubleTake));
        var b = QuizScore.Empty.Plus(AtDoubleTake(CubeAnswer.DoublePass));

        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void Equality_TheSameRows_ButAnotherTotal_IsADifferentScore()
    {
        // Two sessions with identical rows whose Totals differ, because the
        // Total's correct count is not in the rows. e is 2^-15, which counts as
        // zero; 2e does not.
        //   X: Too good at e below the cash (e, e), then Double / Take there
        //      (0, 0) — one answer wholly correct.
        //   Y: Double / Take where not doubling is e better (e, 0), then
        //      Double / Pass at e below the cash (0, e) — two wholly correct.
        const double e = TestSubmissions.ShowsAsZero;
        var x = QuizScore.Empty
            .Plus(TestSubmissions.PartsZeroWholeNot)
            .Plus(TestSubmissions.Cube(CubeAnswer.DoubleTake, 0.5, 1.0 - e));
        var y = QuizScore.Empty
            .Plus(TestSubmissions.Cube(CubeAnswer.DoubleTake, 0.5 + e, 0.5))
            .Plus(TestSubmissions.Cube(CubeAnswer.DoublePass, 0.5, 1.0 - e));

        Assert.Equal(x.PlayDecisions, y.PlayDecisions);
        Assert.Equal(new ScoreSegment(2, 2, e), x.DoubleDecisions);
        Assert.Equal(x.DoubleDecisions, y.DoubleDecisions);
        Assert.Equal(new ScoreSegment(2, 2, e), x.TakeDecisions);
        Assert.Equal(x.TakeDecisions, y.TakeDecisions);

        Assert.Equal(new ScoreSegment(2, 1, 2 * e), x.Total);
        Assert.Equal(new ScoreSegment(2, 2, 2 * e), y.Total);
        Assert.NotEqual(x, y);
        Assert.True(x != y);
    }

    [Fact]
    public void Equality_WithNull()
    {
        Assert.False(QuizScore.Empty.Equals(null));
        Assert.False(QuizScore.Empty == null);
        Assert.True((QuizScore?)null == null);
    }

    [Fact]
    public void PlusPlay_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => QuizScore.Empty.Plus((SubmittedPlay)null!));
    }

    [Fact]
    public void PlusCube_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => QuizScore.Empty.Plus((SubmittedCubeAnswer)null!));
    }
}
