namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;

public class QuizScoreTests
{
    // QuizScore folds these records by reading their result fields; it never
    // reads ProblemKey — session scoring is deliberately key-independent, so
    // even a no-key (null) submission folds. Null here keeps every folding
    // assertion below unchanged in intent while pinning that independence.
    private static SubmittedPlay Play(double equityLoss, bool correct) =>
        new(ProblemKey: null, UserPlay: [], MatchedCandidateIndex: correct ? 0 : 1,
            EquityLoss: equityLoss, IsCorrect: correct);

    // Per-half correctness is derived from the answer/truth pair rather than
    // stated, so this helper composes pairs that realize the requested
    // verdicts: the truth is always (Double, Take) and each half of the
    // answer either matches it or is bent away from it. The accumulation
    // assertions below are unchanged in intent by that.
    private static SubmittedCubeAction Cube(
        double doublerLoss, bool doublerCorrect, double takerLoss, bool takerCorrect) =>
        new(ProblemKey: null,
            new CubeClaimPair(
                doublerCorrect ? CubeClaim.Double : CubeClaim.NoDouble,
                takerCorrect ? CubeAction.Take : CubeAction.Pass),
            CubeClaimPair.DoubleTake,
            DoublerEquityLoss: doublerLoss, TakerEquityLoss: takerLoss);

    [Fact]
    public void Empty_AllSegmentsAndTotalAreEmpty()
    {
        var s = QuizScore.Empty;
        Assert.Equal(ScoreSegment.Empty, s.PlayDecisions);
        Assert.Equal(ScoreSegment.Empty, s.DoubleDecisions);
        Assert.Equal(ScoreSegment.Empty, s.TakeDecisions);
        Assert.Equal(ScoreSegment.Empty, s.Total);
    }

    [Fact]
    public void PlusPlay_CorrectPlay_IncrementsPlaySubmittedAndCorrect()
    {
        var s = QuizScore.Empty.Plus(Play(equityLoss: 0.0, correct: true));

        Assert.Equal(1, s.PlayDecisions.Submitted);
        Assert.Equal(1, s.PlayDecisions.Correct);
        Assert.Equal(0.0, s.PlayDecisions.TotalEquityLoss);
    }

    [Fact]
    public void PlusPlay_IncorrectPlay_IncrementsSubmittedNotCorrect_AccumulatesEquityLoss()
    {
        var s = QuizScore.Empty
            .Plus(Play(equityLoss: 0.025, correct: false))
            .Plus(Play(equityLoss: 0.075, correct: false));

        Assert.Equal(2, s.PlayDecisions.Submitted);
        Assert.Equal(0, s.PlayDecisions.Correct);
        Assert.Equal(0.10, s.PlayDecisions.TotalEquityLoss, precision: 9);
    }

    [Fact]
    public void PlusPlay_LeavesCubeSegmentsUntouched()
    {
        var s = QuizScore.Empty.Plus(Play(equityLoss: 0.05, correct: false));

        Assert.Equal(ScoreSegment.Empty, s.DoubleDecisions);
        Assert.Equal(ScoreSegment.Empty, s.TakeDecisions);
    }

    [Fact]
    public void PlusCube_BumpsDoubleAndTakeByOneEach_PlayUntouched()
    {
        var s = QuizScore.Empty
            .Plus(Cube(doublerLoss: 0.0, doublerCorrect: true, takerLoss: 0.12, takerCorrect: false));

        Assert.Equal(1, s.DoubleDecisions.Submitted);
        Assert.Equal(1, s.DoubleDecisions.Correct);
        Assert.Equal(0.0, s.DoubleDecisions.TotalEquityLoss);

        Assert.Equal(1, s.TakeDecisions.Submitted);
        Assert.Equal(0, s.TakeDecisions.Correct);
        Assert.Equal(0.12, s.TakeDecisions.TotalEquityLoss, precision: 9);

        Assert.Equal(ScoreSegment.Empty, s.PlayDecisions);
    }

    [Fact]
    public void Total_IsTheSumOfAllThreeSegments()
    {
        var s = QuizScore.Empty
            .Plus(Play(equityLoss: 0.02, correct: false))
            .Plus(Cube(doublerLoss: 0.03, doublerCorrect: false, takerLoss: 0.0, takerCorrect: true));

        Assert.Equal(3, s.Total.Submitted);   // 1 play + 1 double + 1 take
        Assert.Equal(1, s.Total.Correct);     // only the taker half was correct
        Assert.Equal(0.05, s.Total.TotalEquityLoss, precision: 9);
    }

    [Fact]
    public void Total_IsDerived_ChangingASegmentChangesTotal()
    {
        var before = QuizScore.Empty.Plus(Play(equityLoss: 0.04, correct: false));
        var after = before.Plus(Cube(doublerLoss: 0.06, doublerCorrect: false, takerLoss: 0.0, takerCorrect: true));

        Assert.Equal(1, before.Total.Submitted);
        Assert.Equal(3, after.Total.Submitted);
        Assert.Equal(0.10, after.Total.TotalEquityLoss, precision: 9);
    }

    [Fact]
    public void Plus_ReturnsNewInstance_OriginalUnchanged()
    {
        var original = QuizScore.Empty;
        var updated = original.Plus(Play(equityLoss: 0.05, correct: false));

        Assert.Equal(0, original.PlayDecisions.Submitted);
        Assert.Equal(1, updated.PlayDecisions.Submitted);
        Assert.NotSame(original, updated);
    }

    [Fact]
    public void PlusPlay_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => QuizScore.Empty.Plus((SubmittedPlay)null!));
    }

    [Fact]
    public void PlusCube_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => QuizScore.Empty.Plus((SubmittedCubeAction)null!));
    }

    // =====================================================================
    //  Claim-vs-claim cube scoring (SPEC-scoring §3; halheinrich/backgammon#86)
    // =====================================================================
    //
    // The fold below scores a submitted CubeClaimPair against the position's
    // derived truth (DecisionData.BestClaimPair) — the doubler half claim to
    // claim, the taker half action to action. Every fixture here states the
    // two equities and lets the producer derive the truth; nothing in this
    // suite re-derives a claim.
    //
    //   BestDoublerAction = Double  iff min(E_DT, 1) > E_ND
    //   BestDoublerClaim  = TooGood iff best action is NoDouble and E_ND > 1
    //   BestTakerAction   = Take    iff E_DT < 1

    private static DecisionData CubeDecision(double noDoubleEquity, double doubleTakeEquity) => new()
    {
        IsCube = true,
        NoDoubleEquity = noDoubleEquity,
        DoubleTakeEquity = doubleTakeEquity,
    };

    /// <summary>
    /// The analysed decision whose derived truth is <paramref name="truth"/> —
    /// one fixture per cell of <see cref="CubeClaimPair"/>'s closed 3×2. Five
    /// cells are verdicts off the tie boundaries; the sixth, the incoherent
    /// (No double, Pass), is reachable as truth only ON the
    /// <c>NoDoubleEquity == 1</c> boundary, where the ruled tie-breaks compose
    /// it (SPEC-scoring §3, amended 2026-09-01).
    /// </summary>
    private static DecisionData TruthFixture(CubeClaimPair truth) =>
        (truth.Claim, truth.Taker) switch
        {
            (CubeClaim.NoDouble, CubeAction.Take) => CubeDecision(0.20, 0.10),
            (CubeClaim.Double,   CubeAction.Take) => CubeDecision(0.30, 0.60),
            (CubeClaim.Double,   CubeAction.Pass) => CubeDecision(0.50, 1.20),
            (CubeClaim.TooGood,  CubeAction.Pass) => CubeDecision(1.30, 1.50),
            (CubeClaim.TooGood,  CubeAction.Take) => CubeDecision(1.10, 0.90),
            (CubeClaim.NoDouble, CubeAction.Pass) => CubeDecision(1.00, 1.20),
            _ => throw new ArgumentOutOfRangeException(nameof(truth), truth, "Not a CubeClaimPair cell."),
        };

    /// <summary>The whole closed 3×2 of answers, in the ruled claim-axis order.</summary>
    public static TheoryData<CubeClaim, CubeAction> AllAnswers => new()
    {
        { CubeClaim.NoDouble, CubeAction.Take },
        { CubeClaim.NoDouble, CubeAction.Pass },
        { CubeClaim.Double,   CubeAction.Take },
        { CubeClaim.Double,   CubeAction.Pass },
        { CubeClaim.TooGood,  CubeAction.Take },
        { CubeClaim.TooGood,  CubeAction.Pass },
    };

    private static SubmittedCubeAction Submit(CubeClaimPair truth, CubeClaimPair answer) =>
        SubmittedCubeAction.From(problemKey: null, answer, TruthFixture(truth));

    // ---------------------------------------------------------------------
    //  Fixtures
    // ---------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllAnswers))]
    public void TruthFixtures_DeriveTheCellTheyAreIndexedBy(CubeClaim claim, CubeAction taker)
    {
        // A grid failure below can then never be mistaken for a mis-built
        // fixture.
        var cell = new CubeClaimPair(claim, taker);

        Assert.Equal(cell, TruthFixture(cell).BestClaimPair);
    }

    // ---------------------------------------------------------------------
    //  The 6×6 submission-vs-truth grid
    // ---------------------------------------------------------------------

    /// <summary>
    /// Every cell of the grid: which halves the fold counts correct when the
    /// answered pair is submitted against the decision whose derived truth is
    /// the truth pair. The diagonal is doubly correct; every off-diagonal
    /// cell is wrong on exactly the halves that differ — including the two
    /// no-double claims, which share a board action and are still
    /// distinguished.
    /// </summary>
    [Theory]
    // truth = (No double, Take)   — E_ND 0.20 / E_DT 0.10
    [InlineData(CubeClaim.NoDouble, CubeAction.Take, CubeClaim.NoDouble, CubeAction.Take, true,  true)]
    [InlineData(CubeClaim.NoDouble, CubeAction.Take, CubeClaim.NoDouble, CubeAction.Pass, true,  false)]
    [InlineData(CubeClaim.NoDouble, CubeAction.Take, CubeClaim.Double,   CubeAction.Take, false, true)]
    [InlineData(CubeClaim.NoDouble, CubeAction.Take, CubeClaim.Double,   CubeAction.Pass, false, false)]
    [InlineData(CubeClaim.NoDouble, CubeAction.Take, CubeClaim.TooGood,  CubeAction.Take, false, true)]
    [InlineData(CubeClaim.NoDouble, CubeAction.Take, CubeClaim.TooGood,  CubeAction.Pass, false, false)]
    // truth = (Double, Take)      — E_ND 0.30 / E_DT 0.60
    [InlineData(CubeClaim.Double,   CubeAction.Take, CubeClaim.NoDouble, CubeAction.Take, false, true)]
    [InlineData(CubeClaim.Double,   CubeAction.Take, CubeClaim.NoDouble, CubeAction.Pass, false, false)]
    [InlineData(CubeClaim.Double,   CubeAction.Take, CubeClaim.Double,   CubeAction.Take, true,  true)]
    [InlineData(CubeClaim.Double,   CubeAction.Take, CubeClaim.Double,   CubeAction.Pass, true,  false)]
    [InlineData(CubeClaim.Double,   CubeAction.Take, CubeClaim.TooGood,  CubeAction.Take, false, true)]
    [InlineData(CubeClaim.Double,   CubeAction.Take, CubeClaim.TooGood,  CubeAction.Pass, false, false)]
    // truth = (Double, Pass)      — E_ND 0.50 / E_DT 1.20
    [InlineData(CubeClaim.Double,   CubeAction.Pass, CubeClaim.NoDouble, CubeAction.Take, false, false)]
    [InlineData(CubeClaim.Double,   CubeAction.Pass, CubeClaim.NoDouble, CubeAction.Pass, false, true)]
    [InlineData(CubeClaim.Double,   CubeAction.Pass, CubeClaim.Double,   CubeAction.Take, true,  false)]
    [InlineData(CubeClaim.Double,   CubeAction.Pass, CubeClaim.Double,   CubeAction.Pass, true,  true)]
    [InlineData(CubeClaim.Double,   CubeAction.Pass, CubeClaim.TooGood,  CubeAction.Take, false, false)]
    [InlineData(CubeClaim.Double,   CubeAction.Pass, CubeClaim.TooGood,  CubeAction.Pass, false, true)]
    // truth = (Too good, Pass)    — E_ND 1.30 / E_DT 1.50
    [InlineData(CubeClaim.TooGood,  CubeAction.Pass, CubeClaim.NoDouble, CubeAction.Take, false, false)]
    [InlineData(CubeClaim.TooGood,  CubeAction.Pass, CubeClaim.NoDouble, CubeAction.Pass, false, true)]
    [InlineData(CubeClaim.TooGood,  CubeAction.Pass, CubeClaim.Double,   CubeAction.Take, false, false)]
    [InlineData(CubeClaim.TooGood,  CubeAction.Pass, CubeClaim.Double,   CubeAction.Pass, false, true)]
    [InlineData(CubeClaim.TooGood,  CubeAction.Pass, CubeClaim.TooGood,  CubeAction.Take, true,  false)]
    [InlineData(CubeClaim.TooGood,  CubeAction.Pass, CubeClaim.TooGood,  CubeAction.Pass, true,  true)]
    // truth = (Too good, Take)    — E_ND 1.10 / E_DT 0.90 (a match-only verdict)
    [InlineData(CubeClaim.TooGood,  CubeAction.Take, CubeClaim.NoDouble, CubeAction.Take, false, true)]
    [InlineData(CubeClaim.TooGood,  CubeAction.Take, CubeClaim.NoDouble, CubeAction.Pass, false, false)]
    [InlineData(CubeClaim.TooGood,  CubeAction.Take, CubeClaim.Double,   CubeAction.Take, false, true)]
    [InlineData(CubeClaim.TooGood,  CubeAction.Take, CubeClaim.Double,   CubeAction.Pass, false, false)]
    [InlineData(CubeClaim.TooGood,  CubeAction.Take, CubeClaim.TooGood,  CubeAction.Take, true,  true)]
    [InlineData(CubeClaim.TooGood,  CubeAction.Take, CubeClaim.TooGood,  CubeAction.Pass, true,  false)]
    // truth = (No double, Pass)   — E_ND 1.00 / E_DT 1.20, the boundary-composed
    //                               incoherent cell as derived truth
    [InlineData(CubeClaim.NoDouble, CubeAction.Pass, CubeClaim.NoDouble, CubeAction.Take, true,  false)]
    [InlineData(CubeClaim.NoDouble, CubeAction.Pass, CubeClaim.NoDouble, CubeAction.Pass, true,  true)]
    [InlineData(CubeClaim.NoDouble, CubeAction.Pass, CubeClaim.Double,   CubeAction.Take, false, false)]
    [InlineData(CubeClaim.NoDouble, CubeAction.Pass, CubeClaim.Double,   CubeAction.Pass, false, true)]
    [InlineData(CubeClaim.NoDouble, CubeAction.Pass, CubeClaim.TooGood,  CubeAction.Take, false, false)]
    [InlineData(CubeClaim.NoDouble, CubeAction.Pass, CubeClaim.TooGood,  CubeAction.Pass, false, true)]
    public void PlusCube_ScoresEveryCellOfTheSubmissionVsTruthGrid(
        CubeClaim truthClaim, CubeAction truthTaker,
        CubeClaim answerClaim, CubeAction answerTaker,
        bool doublerCorrect, bool takerCorrect)
    {
        var truth = new CubeClaimPair(truthClaim, truthTaker);
        var answer = new CubeClaimPair(answerClaim, answerTaker);
        var submission = Submit(truth, answer);

        Assert.Equal(truth, submission.BestDecision);
        Assert.Equal(doublerCorrect, submission.DoublerCorrect);
        Assert.Equal(takerCorrect, submission.TakerCorrect);

        var s = QuizScore.Empty.Plus(submission);

        Assert.Equal(1, s.DoubleDecisions.Submitted);
        Assert.Equal(doublerCorrect ? 1 : 0, s.DoubleDecisions.Correct);
        Assert.Equal(1, s.TakeDecisions.Submitted);
        Assert.Equal(takerCorrect ? 1 : 0, s.TakeDecisions.Correct);
        Assert.Equal(ScoreSegment.Empty, s.PlayDecisions);
    }

    [Theory]
    [MemberData(nameof(AllAnswers))]
    public void PlusCube_TheDiagonalIsFullyCorrectAtZeroLoss(CubeClaim claim, CubeAction taker)
    {
        // Answering a position with its own derived truth pair: both halves
        // right, both equity losses zero. True of the boundary-composed
        // incoherent cell too — it is the truth there, so it scores as one.
        var truth = new CubeClaimPair(claim, taker);
        var s = QuizScore.Empty.Plus(Submit(truth, truth));

        Assert.Equal(new ScoreSegment(Submitted: 1, Correct: 1, TotalEquityLoss: 0.0), s.DoubleDecisions);
        Assert.Equal(new ScoreSegment(Submitted: 1, Correct: 1, TotalEquityLoss: 0.0), s.TakeDecisions);
    }

    // ---------------------------------------------------------------------
    //  Right action, wrong reason — incorrect at +0.000
    // ---------------------------------------------------------------------

    /// <summary>
    /// The two no-double claims share a board action, so swapping one for the
    /// other leaves the doubler's equity untouched. The claim comparison
    /// still scores it wrong: incorrect at +0.000, the ruled "right action,
    /// wrong reason" verdict, with no partial-credit tier in between.
    /// </summary>
    [Theory]
    // truth claim,                 truth taker,      answered claim
    [InlineData(CubeClaim.NoDouble, CubeAction.Take, CubeClaim.TooGood)]
    [InlineData(CubeClaim.TooGood,  CubeAction.Pass, CubeClaim.NoDouble)]
    [InlineData(CubeClaim.TooGood,  CubeAction.Take, CubeClaim.NoDouble)]
    [InlineData(CubeClaim.NoDouble, CubeAction.Pass, CubeClaim.TooGood)]
    public void PlusCube_WrongClaimOverTheRightAction_IsIncorrectAtZeroEquityLoss(
        CubeClaim truthClaim, CubeAction truthTaker, CubeClaim answeredClaim)
    {
        var truth = new CubeClaimPair(truthClaim, truthTaker);
        var submission = Submit(truth, new CubeClaimPair(answeredClaim, truthTaker));

        Assert.Equal(
            answeredClaim.ToCubeAction(),
            TruthFixture(truth).BestDoublerAction);          // same board action ...
        Assert.False(submission.DoublerCorrect);             // ... different claim
        Assert.Equal(0.0, submission.DoublerEquityLoss);

        var s = QuizScore.Empty.Plus(submission);

        Assert.Equal(new ScoreSegment(Submitted: 1, Correct: 0, TotalEquityLoss: 0.0), s.DoubleDecisions);
        Assert.Equal(new ScoreSegment(Submitted: 1, Correct: 1, TotalEquityLoss: 0.0), s.TakeDecisions);
    }

    [Fact]
    public void PlusCube_WrongClaimOverTheWrongAction_StillCostsEquity()
    {
        // The +0.000 above is a property of the shared board action, not of
        // wrong claims generally: doubling a (No double, Take) position costs
        // the E_ND - min(E_DT, 1) gap.
        var submission = Submit(CubeClaimPair.NoDoubleTake, CubeClaimPair.DoubleTake);

        Assert.False(submission.DoublerCorrect);
        Assert.Equal(0.10, submission.DoublerEquityLoss, precision: 9);

        var s = QuizScore.Empty.Plus(submission);

        Assert.Equal(0, s.DoubleDecisions.Correct);
        Assert.Equal(0.10, s.DoubleDecisions.TotalEquityLoss, precision: 9);
    }

    // ---------------------------------------------------------------------
    //  The taker half is independent of the claim
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(CubeAction.Take)]
    [InlineData(CubeAction.Pass)]
    public void PlusCube_TakerHalfIsUnaffectedByTheClaim(CubeAction answeredTaker)
    {
        // One truth, one taker answer, all three claims: the take segment is
        // identical every time — only the double segment moves.
        var truth = CubeClaimPair.DoublePass;
        ScoreSegment[] takeSegments =
        [
            .. new[] { CubeClaim.NoDouble, CubeClaim.Double, CubeClaim.TooGood }
                .Select(claim => QuizScore.Empty
                    .Plus(Submit(truth, new CubeClaimPair(claim, answeredTaker)))
                    .TakeDecisions)
        ];

        Assert.Equal(3, takeSegments.Length);
        Assert.All(takeSegments, seg => Assert.Equal(takeSegments[0], seg));
        Assert.Equal(answeredTaker == truth.Taker ? 1 : 0, takeSegments[0].Correct);
        Assert.Equal(answeredTaker == truth.Taker ? 0.0 : 0.20, takeSegments[0].TotalEquityLoss, precision: 9);
    }

    // ---------------------------------------------------------------------
    //  The incoherent cell — (No double, Pass)
    // ---------------------------------------------------------------------

    [Fact]
    public void PlusCube_IncoherentAnswer_IsSubmittableAndSimplyWrong()
    {
        // "Not good enough to double, yet they'd pass": representable and
        // submittable by ruling (SPEC-scoring §3 — the axes are not
        // cross-disabled), and scored by the same claim-vs-claim rule as any
        // other answer. Against a (Double, Take) truth both halves differ.
        var answer = CubeClaimPair.NoDoublePass;
        Assert.True(answer.IsIncoherent);

        var submission = Submit(CubeClaimPair.DoubleTake, answer);
        var s = QuizScore.Empty.Plus(submission);

        Assert.False(submission.DoublerCorrect);
        Assert.False(submission.TakerCorrect);
        Assert.Equal(1, s.DoubleDecisions.Submitted);
        Assert.Equal(0, s.DoubleDecisions.Correct);
        Assert.Equal(1, s.TakeDecisions.Submitted);
        Assert.Equal(0, s.TakeDecisions.Correct);
    }

    [Fact]
    public void PlusCube_IncoherentAnswer_AgainstTheBoundaryComposedTruth_IsFullyCorrect()
    {
        // The one place (No double, Pass) is right: E_ND == 1 with E_DT >= 1,
        // where the ruled tie-breaks (No Double; Pass) compose it as the
        // derived truth (SPEC-scoring §3, amended 2026-09-01). The doubler
        // half is equity-tied there, so the claim comparison — not the
        // equity — is what makes this cell the correct answer and the other
        // two claims wrong.
        var boundary = TruthFixture(CubeClaimPair.NoDoublePass);
        Assert.Equal(CubeClaimPair.NoDoublePass, boundary.BestClaimPair);

        var s = QuizScore.Empty.Plus(Submit(CubeClaimPair.NoDoublePass, CubeClaimPair.NoDoublePass));

        Assert.Equal(new ScoreSegment(Submitted: 1, Correct: 1, TotalEquityLoss: 0.0), s.DoubleDecisions);
        Assert.Equal(new ScoreSegment(Submitted: 1, Correct: 1, TotalEquityLoss: 0.0), s.TakeDecisions);
    }

    [Theory]
    [MemberData(nameof(AllAnswers))]
    public void PlusCube_AtTheDoublerTie_EveryClaimCostsZeroButOnlyOneIsCorrect(
        CubeClaim claim, CubeAction taker)
    {
        // E_ND == 1 == min(E_DT, 1): all three claims are equity-identical on
        // the doubler half, so "score-neutral" there is an equity statement
        // only — the verdict still discriminates, which is the whole point of
        // the claim layer. The taker half is NOT tied at this fixture
        // (E_DT 1.20), so it keeps costing 0.20 for a Take.
        var submission = Submit(CubeClaimPair.NoDoublePass, new CubeClaimPair(claim, taker));
        var s = QuizScore.Empty.Plus(submission);

        Assert.Equal(0.0, s.DoubleDecisions.TotalEquityLoss);
        Assert.Equal(claim == CubeClaim.NoDouble ? 1 : 0, s.DoubleDecisions.Correct);
        Assert.Equal(taker == CubeAction.Pass ? 0.0 : 0.20, s.TakeDecisions.TotalEquityLoss, precision: 9);
    }

    [Theory]
    [MemberData(nameof(AllAnswers))]
    public void PlusCube_AtTheFullyTiedBoundary_EveryAnswerCostsZeroOnBothHalves(
        CubeClaim claim, CubeAction taker)
    {
        // E_ND == 1 AND E_DT == 1: both halves tie, so the spec's
        // measure-zero "every answer there costs +0.000" holds literally
        // across all six cells. Only (No double, Pass) is scored correct even
        // so.
        var decision = CubeDecision(1.00, 1.00);
        Assert.Equal(CubeClaimPair.NoDoublePass, decision.BestClaimPair);

        var submission = SubmittedCubeAction.From(
            problemKey: null, new CubeClaimPair(claim, taker), decision);
        var s = QuizScore.Empty.Plus(submission);

        Assert.Equal(0.0, s.DoubleDecisions.TotalEquityLoss);
        Assert.Equal(0.0, s.TakeDecisions.TotalEquityLoss);
        Assert.Equal(claim == CubeClaim.NoDouble ? 1 : 0, s.DoubleDecisions.Correct);
        Assert.Equal(taker == CubeAction.Pass ? 1 : 0, s.TakeDecisions.Correct);
    }

    // ---------------------------------------------------------------------
    //  SubmittedCubeAction.From — the producer read
    // ---------------------------------------------------------------------

    [Fact]
    public void From_TakesTruthAndBothLossesFromTheProducer()
    {
        var decision = CubeDecision(1.30, 1.50);   // (Too good, Pass)
        var answer = new CubeClaimPair(CubeClaim.Double, CubeAction.Take);

        var submission = SubmittedCubeAction.From(problemKey: null, answer, decision);

        Assert.Equal(answer, submission.UserDecision);
        Assert.Equal(decision.BestClaimPair, submission.BestDecision);
        Assert.Equal(decision.DoublerActionError(CubeAction.Double), submission.DoublerEquityLoss);
        Assert.Equal(decision.TakerActionError(CubeAction.Take), submission.TakerEquityLoss);
    }

    [Fact]
    public void From_CollapsesTheClaimToItsBoardActionForTheEquityRead()
    {
        // A TooGood claim's doubler equity is NoDouble's — the single-sourced
        // collapse (CubeClaimExtensions.ToCubeAction), never re-spelled here.
        var decision = CubeDecision(0.20, 0.10);   // (No double, Take)

        var tooGood = SubmittedCubeAction.From(
            problemKey: null, new CubeClaimPair(CubeClaim.TooGood, CubeAction.Take), decision);

        Assert.Equal(decision.DoublerActionError(CubeAction.NoDouble), tooGood.DoublerEquityLoss);
        Assert.Equal(0.0, tooGood.DoublerEquityLoss);
        Assert.False(tooGood.DoublerCorrect);
    }

    [Fact]
    public void From_NullDecision_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            SubmittedCubeAction.From(problemKey: null, CubeClaimPair.DoubleTake, null!));
    }

    [Fact]
    public void From_CheckerPlayDecision_Throws()
    {
        var play = new DecisionData { IsCube = false };

        Assert.Throws<InvalidOperationException>(() =>
            SubmittedCubeAction.From(problemKey: null, CubeClaimPair.DoubleTake, play));
    }
}
