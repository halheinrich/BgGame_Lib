namespace BgGame_Lib;

using System.Diagnostics.CodeAnalysis;
using BgDataTypes_Lib;

/// <summary>
/// The behavior half of a <see cref="QuizCategory"/>: decides whether one
/// decision belongs to the category, given its problem's lifetime stats
/// record (null when never quizzed) and the classification timestamp.
/// Materialized from the data half by <c>QuizCategory.BuildPredicate</c> —
/// the single switch that owns the kind-to-predicate mapping.
///
/// <para>
/// The context is the (decision, stats) pair rather than stats alone because
/// sightings are derived per decision kind — see
/// <see cref="QuizStatsMeasures.Sightings"/>. The <c>asOf</c> timestamp is
/// resolved once per classification pass by the caller, so every predicate in
/// a pass sees the same "now".
/// </para>
/// </summary>
internal interface IQuizCategoryPredicate
{
    /// <summary>Whether <paramref name="decision"/> belongs to this category.</summary>
    /// <param name="decision">The live decision being classified.</param>
    /// <param name="stats">Its problem's lifetime record (looked up by the decision's
    /// <see cref="ProblemKey"/>), or <see langword="null"/> when never quizzed.</param>
    /// <param name="asOf">The classification timestamp (one per pass).</param>
    bool Matches(BgDecisionData decision, ProblemStats? stats, DateTimeOffset asOf);
}

/// <summary>
/// Shared derivations over a decision's lifetime record — single-sourced so
/// every predicate agrees on what "unseen" and "sightings" mean.
/// </summary>
internal static class QuizStatsMeasures
{
    /// <summary>
    /// Whether the decision has never been quizzed: no record at all, or —
    /// defensively — a record with an empty tally.
    /// </summary>
    internal static bool IsUnseen([NotNullWhen(false)] ProblemStats? stats) =>
        stats is null || stats.Tally.Submitted == 0;

    /// <summary>
    /// How many times the decision has been sighted (quizzed as a position).
    /// Derived from the tally per decision kind: a checker play submits one
    /// decision per sighting; a cube position submits two (the two-half fold),
    /// so its sightings are <c>Submitted / 2</c> — always exact, because one
    /// key only ever folds one kind (a play key and a cube key are distinct
    /// by grammar — see <see cref="ProblemStats"/>) and cube folds add
    /// exactly 2.
    /// </summary>
    internal static int Sightings(BgDecisionData decision, ProblemStats? stats) =>
        IsUnseen(stats) ? 0
        : decision.IsCube ? stats.Tally.Submitted / 2
        : stats.Tally.Submitted;
}

/// <summary>Matches decisions never quizzed. See <see cref="QuizCategory.NeverSeen"/>.</summary>
internal sealed class NeverSeenPredicate : IQuizCategoryPredicate
{
    private NeverSeenPredicate()
    {
    }

    /// <summary>The one instance — the predicate is stateless.</summary>
    internal static NeverSeenPredicate Instance { get; } = new();

    /// <inheritdoc/>
    public bool Matches(BgDecisionData decision, ProblemStats? stats, DateTimeOffset asOf) =>
        QuizStatsMeasures.IsUnseen(stats);
}

/// <summary>
/// Matches decisions with at least one wrong submission (per-half for cubes).
/// See <see cref="QuizCategory.GotWrong"/>.
/// </summary>
internal sealed class GotWrongPredicate : IQuizCategoryPredicate
{
    private GotWrongPredicate()
    {
    }

    /// <summary>The one instance — the predicate is stateless.</summary>
    internal static GotWrongPredicate Instance { get; } = new();

    /// <inheritdoc/>
    public bool Matches(BgDecisionData decision, ProblemStats? stats, DateTimeOffset asOf) =>
        !QuizStatsMeasures.IsUnseen(stats) && stats.Wrong >= 1;
}

/// <summary>
/// Matches decisions sighted fewer than <paramref name="times"/> times
/// (never-seen has zero sightings and always matches). See
/// <see cref="QuizCategory.SeenFewerThan"/>.
/// </summary>
/// <param name="times">Exclusive upper bound on sightings; validated by the factory.</param>
internal sealed class SeenFewerThanPredicate(int times) : IQuizCategoryPredicate
{
    /// <inheritdoc/>
    public bool Matches(BgDecisionData decision, ProblemStats? stats, DateTimeOffset asOf) =>
        QuizStatsMeasures.Sightings(decision, stats) < times;
}

/// <summary>
/// Matches decisions not quizzed within the last <paramref name="days"/> days,
/// including never quizzed at all. See <see cref="QuizCategory.NotSeenInDays"/>.
/// </summary>
/// <param name="days">Days of staleness required to match; validated by the factory.</param>
internal sealed class NotSeenInDaysPredicate(int days) : IQuizCategoryPredicate
{
    /// <inheritdoc/>
    public bool Matches(BgDecisionData decision, ProblemStats? stats, DateTimeOffset asOf) =>
        QuizStatsMeasures.IsUnseen(stats)
        || asOf - stats.LastQuizzed >= TimeSpan.FromDays(days);
}

/// <summary>
/// Matches decisions whose lifetime average equity loss per submission
/// strictly exceeds <paramref name="loss"/> (per-half; never-seen does not
/// match). See <see cref="QuizCategory.AvgEquityLossOver"/>.
/// </summary>
/// <param name="loss">Exclusive lower bound on average loss; validated by the factory.</param>
internal sealed class AvgEquityLossOverPredicate(double loss) : IQuizCategoryPredicate
{
    /// <inheritdoc/>
    public bool Matches(BgDecisionData decision, ProblemStats? stats, DateTimeOffset asOf) =>
        !QuizStatsMeasures.IsUnseen(stats) && stats.Tally.AverageEquityLoss > loss;
}

/// <summary>
/// Matches decisions whose lifetime wrong fraction (1 − accuracy) strictly
/// exceeds <paramref name="rate"/> (per-half; never-seen does not match). See
/// <see cref="QuizCategory.WrongRateOver"/>.
/// </summary>
/// <param name="rate">Exclusive lower bound on the wrong fraction; validated by the factory.</param>
internal sealed class WrongRateOverPredicate(double rate) : IQuizCategoryPredicate
{
    /// <inheritdoc/>
    public bool Matches(BgDecisionData decision, ProblemStats? stats, DateTimeOffset asOf) =>
        !QuizStatsMeasures.IsUnseen(stats) && 1.0 - stats.Tally.Accuracy > rate;
}
