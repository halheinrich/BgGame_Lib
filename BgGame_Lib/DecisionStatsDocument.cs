namespace BgGame_Lib;

using System.Collections.Immutable;
using System.Text.Json.Serialization;
using BgDataTypes_Lib;

/// <summary>
/// The persistent per-decision quiz-stats document: an immutable collection of
/// <see cref="DecisionStats"/> keyed by <see cref="DecisionId"/>, one per
/// decision ever quizzed from a problem-set corpus. Each <c>Plus</c> call
/// returns a new document with that submission folded into its decision's
/// lifetime record (creating the record on first sight).
///
/// <para>
/// This is the storage-agnostic model behind BgQuiz's stats file (a versioned
/// JSON document kept beside the quizzed corpus); the library does no I/O —
/// consumers load bytes, deserialize, fold, and serialize back. JSON round-trips
/// via the bundled <see cref="DecisionStatsDocumentJsonConverter"/> (type-level
/// <c>[JsonConverter]</c>, so no consumer-side registration) with schema
/// version <see cref="CurrentSchemaVersion"/>; see the converter for the pinned
/// wire format and the fail-loud read posture.
/// </para>
///
/// <para>
/// <b>Clock seam.</b> The fold overloads take a <see cref="TimeProvider"/> and
/// resolve the timestamp themselves — the model never reads ambient time
/// (<c>DateTime.Now</c>), so folding stays deterministic under test via a fake
/// provider. The seam deliberately lives here, on the consumer's entry point,
/// where the type system makes ambient-time misuse impossible;
/// <see cref="DecisionStats"/> folds take the already-resolved
/// <see cref="DateTimeOffset"/> and stay pure value computations.
/// </para>
///
/// <para>
/// A plain class, not a record: the wrapped dictionary would give record
/// equality reference semantics, making value equality a lie. Instances are
/// compared by reference; compare <see cref="Decisions"/> content if needed.
/// </para>
/// </summary>
[JsonConverter(typeof(DecisionStatsDocumentJsonConverter))]
public sealed class DecisionStatsDocument
{
    /// <summary>
    /// The schema version this library reads and writes. Reads reject any
    /// other version (fail-loud); see
    /// <see cref="DecisionStatsDocumentJsonConverter"/>.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    private readonly ImmutableDictionary<DecisionId, DecisionStats> _decisions;

    /// <summary>The starting document: no decisions quizzed yet.</summary>
    public static DecisionStatsDocument Empty { get; } =
        new(ImmutableDictionary<DecisionId, DecisionStats>.Empty);

    private DecisionStatsDocument(ImmutableDictionary<DecisionId, DecisionStats> decisions) =>
        _decisions = decisions;

    /// <summary>
    /// Create a document holding the given per-decision records.
    /// </summary>
    /// <param name="stats">The records; each decision may appear at most once.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="stats"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="stats"/> contains a <see langword="null"/>
    /// element or two records with the same <see cref="DecisionStats.Id"/>.
    /// </exception>
    public static DecisionStatsDocument FromStats(IEnumerable<DecisionStats> stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        var builder = ImmutableDictionary.CreateBuilder<DecisionId, DecisionStats>();
        foreach (var s in stats)
        {
            if (s is null)
                throw new ArgumentException("Stats collection contains a null element.", nameof(stats));
            if (builder.ContainsKey(s.Id))
                throw new ArgumentException($"Duplicate DecisionId '{s.Id}'.", nameof(stats));
            builder.Add(s.Id, s);
        }
        return builder.Count == 0 ? Empty : new(builder.ToImmutable());
    }

    /// <summary>Number of decisions with a lifetime record in this document.</summary>
    public int Count => _decisions.Count;

    /// <summary>
    /// The per-decision records, keyed by <see cref="DecisionStats.Id"/>.
    /// Enumeration order is unspecified — the serialized form orders by
    /// canonical id string, not this view.
    /// </summary>
    public IReadOnlyDictionary<DecisionId, DecisionStats> Decisions => _decisions;

    /// <summary>
    /// Return a new document with <paramref name="play"/> folded into its
    /// decision's lifetime record — created via
    /// <see cref="DecisionStats.From(SubmittedPlay, DateTimeOffset)"/> when the
    /// decision has never been quizzed before.
    /// </summary>
    /// <param name="play">The submission to fold.</param>
    /// <param name="clock">Source of the fold timestamp (its <see cref="TimeProvider.GetUtcNow"/>).</param>
    public DecisionStatsDocument Plus(SubmittedPlay play, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(play);
        ArgumentNullException.ThrowIfNull(clock);
        var quizzedAt = clock.GetUtcNow();
        var updated = _decisions.TryGetValue(play.DecisionId, out var existing)
            ? existing.Plus(play, quizzedAt)
            : DecisionStats.From(play, quizzedAt);
        return new(_decisions.SetItem(play.DecisionId, updated));
    }

    /// <summary>
    /// Return a new document with <paramref name="cube"/> folded into its
    /// decision's lifetime record — created via
    /// <see cref="DecisionStats.From(SubmittedCubeAction, DateTimeOffset)"/>
    /// when the decision has never been quizzed before. The cube position
    /// folds as one decision (both halves must be right to count correct) —
    /// see <see cref="DecisionStats.Plus(SubmittedCubeAction, DateTimeOffset)"/>.
    /// </summary>
    /// <param name="cube">The submission to fold.</param>
    /// <param name="clock">Source of the fold timestamp (its <see cref="TimeProvider.GetUtcNow"/>).</param>
    public DecisionStatsDocument Plus(SubmittedCubeAction cube, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(cube);
        ArgumentNullException.ThrowIfNull(clock);
        var quizzedAt = clock.GetUtcNow();
        var updated = _decisions.TryGetValue(cube.DecisionId, out var existing)
            ? existing.Plus(cube, quizzedAt)
            : DecisionStats.From(cube, quizzedAt);
        return new(_decisions.SetItem(cube.DecisionId, updated));
    }
}
