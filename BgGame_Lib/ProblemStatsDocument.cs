namespace BgGame_Lib;

using System.Collections.Immutable;
using System.Text.Json.Serialization;
using BgDataTypes_Lib;

/// <summary>
/// The persistent per-problem quiz-stats document: an immutable collection of
/// <see cref="ProblemStats"/> keyed by <see cref="ProblemKey"/> — the content
/// identity, so every content-equal copy of a problem across files and
/// matches folds into one lifetime record (SPEC-stats-identity.md;
/// halheinrich/backgammon#95). Each <c>Plus</c> call returns a new document
/// with that submission folded into its problem's lifetime record (creating
/// the record on first sight).
///
/// <para>
/// This is the storage-agnostic model behind BgQuiz's stats file (a versioned
/// JSON document kept beside the quizzed corpus); the library does no I/O —
/// consumers load bytes, deserialize, fold, and serialize back. JSON round-trips
/// via the bundled <see cref="ProblemStatsDocumentJsonConverter"/> (type-level
/// <c>[JsonConverter]</c>, so no consumer-side registration) with schema
/// version <see cref="CurrentSchemaVersion"/>; see the converter for the pinned
/// wire format, the fail-loud read posture, and the retired-version
/// recognition signal (<see cref="RetiredStatsSchemaException"/>).
/// </para>
///
/// <para>
/// <b>The no-key rung.</b> A submission whose <c>ProblemKey</c> is
/// <see langword="null"/> (underivable facts — SPEC-stats-identity.md §2) is
/// not recorded: <c>Plus</c> returns the document unchanged. Degrade, never
/// block — the quiz session proceeds and scores normally; only the lifetime
/// record abstains. This document-level skip is the <i>single</i> no-key
/// filter; <see cref="ProblemStats"/> itself rejects keyless folds as
/// corruption.
/// </para>
///
/// <para>
/// <b>Clock seam.</b> The fold overloads take a <see cref="TimeProvider"/> and
/// resolve the timestamp themselves — the model never reads ambient time
/// (<c>DateTime.Now</c>), so folding stays deterministic under test via a fake
/// provider. The seam deliberately lives here, on the consumer's entry point,
/// where the type system makes ambient-time misuse impossible;
/// <see cref="ProblemStats"/> folds take the already-resolved
/// <see cref="DateTimeOffset"/> and stay pure value computations.
/// </para>
///
/// <para>
/// A plain class, not a record: the wrapped dictionary would give record
/// equality reference semantics, making value equality a lie. Instances are
/// compared by reference; compare <see cref="Problems"/> content if needed.
/// </para>
/// </summary>
[JsonConverter(typeof(ProblemStatsDocumentJsonConverter))]
public sealed class ProblemStatsDocument
{
    /// <summary>
    /// The schema version this library reads and writes. Version 1 (the
    /// retired <c>DecisionId</c>-keyed format) is recognised and signalled
    /// via <see cref="RetiredStatsSchemaException"/>; any other version is
    /// rejected fail-loud. See <see cref="ProblemStatsDocumentJsonConverter"/>.
    /// </summary>
    public const int CurrentSchemaVersion = 2;

    private readonly ImmutableDictionary<ProblemKey, ProblemStats> _problems;

    /// <summary>The starting document: no problems quizzed yet.</summary>
    public static ProblemStatsDocument Empty { get; } =
        new(ImmutableDictionary<ProblemKey, ProblemStats>.Empty);

    private ProblemStatsDocument(ImmutableDictionary<ProblemKey, ProblemStats> problems) =>
        _problems = problems;

    /// <summary>
    /// Create a document holding the given per-problem records.
    /// </summary>
    /// <param name="stats">The records; each problem may appear at most once.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="stats"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="stats"/> contains a <see langword="null"/>
    /// element or two records with the same <see cref="ProblemStats.Key"/>.
    /// </exception>
    public static ProblemStatsDocument FromStats(IEnumerable<ProblemStats> stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        var builder = ImmutableDictionary.CreateBuilder<ProblemKey, ProblemStats>();
        foreach (var s in stats)
        {
            if (s is null)
                throw new ArgumentException("Stats collection contains a null element.", nameof(stats));
            if (builder.ContainsKey(s.Key))
                throw new ArgumentException($"Duplicate ProblemKey '{s.Key}'.", nameof(stats));
            builder.Add(s.Key, s);
        }
        return builder.Count == 0 ? Empty : new(builder.ToImmutable());
    }

    /// <summary>Number of problems with a lifetime record in this document.</summary>
    public int Count => _problems.Count;

    /// <summary>
    /// The per-problem records, keyed by <see cref="ProblemStats.Key"/>.
    /// Enumeration order is unspecified — the serialized form orders by
    /// canonical key string, not this view.
    /// </summary>
    public IReadOnlyDictionary<ProblemKey, ProblemStats> Problems => _problems;

    /// <summary>
    /// Return a new document with <paramref name="play"/> folded into its
    /// problem's lifetime record — created via
    /// <see cref="ProblemStats.From(SubmittedPlay, DateTimeOffset)"/> when the
    /// problem has never been quizzed before. A submission carrying no
    /// <see cref="SubmittedPlay.ProblemKey"/> is not recorded: the same
    /// document is returned (the no-key rung — see the type remarks).
    /// </summary>
    /// <param name="play">The submission to fold.</param>
    /// <param name="clock">Source of the fold timestamp (its <see cref="TimeProvider.GetUtcNow"/>).</param>
    public ProblemStatsDocument Plus(SubmittedPlay play, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(play);
        ArgumentNullException.ThrowIfNull(clock);
        if (play.ProblemKey is not { } key)
            return this;
        var quizzedAt = clock.GetUtcNow();
        var updated = _problems.TryGetValue(key, out var existing)
            ? existing.Plus(play, quizzedAt)
            : ProblemStats.From(play, quizzedAt);
        return new(_problems.SetItem(key, updated));
    }

    /// <summary>
    /// Return a new document with <paramref name="cube"/> folded into its
    /// problem's lifetime record — created via
    /// <see cref="ProblemStats.From(SubmittedCubeAction, DateTimeOffset)"/>
    /// when the problem has never been quizzed before. The cube position
    /// folds as <b>two</b> decisions — the doubler half and the taker half,
    /// each counted separately into the one record, so a half-right cube reads
    /// 1-of-2 — see
    /// <see cref="ProblemStats.Plus(SubmittedCubeAction, DateTimeOffset)"/>.
    /// A submission carrying no <see cref="SubmittedCubeAction.ProblemKey"/>
    /// is not recorded: the same document is returned (the no-key rung — see
    /// the type remarks).
    /// </summary>
    /// <param name="cube">The submission to fold.</param>
    /// <param name="clock">Source of the fold timestamp (its <see cref="TimeProvider.GetUtcNow"/>).</param>
    public ProblemStatsDocument Plus(SubmittedCubeAction cube, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(cube);
        ArgumentNullException.ThrowIfNull(clock);
        if (cube.ProblemKey is not { } key)
            return this;
        var quizzedAt = clock.GetUtcNow();
        var updated = _problems.TryGetValue(key, out var existing)
            ? existing.Plus(cube, quizzedAt)
            : ProblemStats.From(cube, quizzedAt);
        return new(_problems.SetItem(key, updated));
    }
}
