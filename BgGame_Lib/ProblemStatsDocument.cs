namespace BgGame_Lib;

using System.Collections.Immutable;
using System.Text.Json;
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
/// wire format, the fail-loud read posture, and the two recognition signals
/// (<see cref="RetiredStatsSchemaException"/> for versions set aside unread,
/// <see cref="FoldableStatsSchemaException"/> for the one version read via
/// <see cref="ReadFoldable"/> and combined via <see cref="Merge"/>).
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
    /// The schema version this library reads and writes. <b>Every</b>
    /// recognised version below it is retired — version 1 (the
    /// <c>DecisionId</c>-keyed format) and version 2 (the
    /// <see cref="ProblemKey"/>-keyed format from before the Jacoby rule
    /// entered money keys) — each recognised and signalled via
    /// <see cref="RetiredStatsSchemaException"/> carrying its own version
    /// number. Above it, <see cref="FoldableSchemaVersion"/> alone is
    /// recognised, as foldable; anything older or newer is rejected
    /// fail-loud. See <see cref="ProblemStatsDocumentJsonConverter"/>.
    /// </summary>
    /// <remarks>
    /// Version 3 is the <b>reinstated</b> current version
    /// (SPEC-stats-identity.md §3, amended 2026-09-02;
    /// halheinrich/backgammon#187). The interim version 4 of the
    /// halheinrich/backgammon#86 leg wrapped each per-problem record in an
    /// answer-kind token and retired v3 on the premise that v3's cube
    /// tallies, accrued under action-vs-action doubler scoring, were not
    /// comparable with claim-vs-claim tallies. The amended Too Good
    /// predicate (Too Good requires the pass) makes the two regimes coincide
    /// on every reachable position, so the premise no longer holds; and the
    /// kind wrapper was a second source of a fact the key's own grammar
    /// already carries (SPEC-scoring.md §4, amended 2026-09-02). The flat
    /// v3 record is therefore current again, byte-for-byte the format
    /// v1.9.1 ships, and v4 — which never reached production — folds into
    /// it rather than retiring.
    /// </remarks>
    public const int CurrentSchemaVersion = 3;

    /// <summary>
    /// The one schema version above <see cref="CurrentSchemaVersion"/> that
    /// the reader recognises: the interim answer-kind format, which
    /// <b>folds</b> into the current document instead of being refused as
    /// newer or set aside as retired. A genuine document in this version
    /// throws <see cref="FoldableStatsSchemaException"/> on the ordinary read
    /// path; <see cref="ReadFoldable"/> reads it into the current shape and
    /// <see cref="Merge"/> combines it (SPEC-stats-identity.md §3, amended
    /// 2026-09-02; halheinrich/backgammon#187).
    /// </summary>
    public const int FoldableSchemaVersion = 4;

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

    /// <summary>
    /// Read a document in the foldable schema version
    /// (<see cref="FoldableSchemaVersion"/>, 4) into the current shape: each
    /// per-problem value's single answer-kind record is unwrapped to the bare
    /// tally-plus-date record the current format carries. This is the read
    /// half of the fold path a <see cref="FoldableStatsSchemaException"/>
    /// names; <see cref="Merge"/> is the combine half, and the file dance
    /// around them — which file is the base, what the folded file is
    /// renamed to — is the consumer's (SPEC-stats-identity.md §3, amended
    /// 2026-09-02; halheinrich/backgammon#187).
    /// </summary>
    /// <param name="json">The whole document text.</param>
    /// <returns>The document's records in the current shape.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="json"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="JsonException">
    /// Thrown for anything but a well-formed version-4 document: another
    /// schema version (the current one included — that is the ordinary
    /// <see cref="JsonSerializer"/> path's), a malformed answer-kind layer
    /// (no kind record, a second one, an unknown or reserved token, a token
    /// disagreeing with its key's grammar, v3's bare record under a v4
    /// version), any malformed record, invalid or duplicate keys, or
    /// trailing content. Never the retired or foldable signals: this reader
    /// is the fold, not the dispatcher.
    /// </exception>
    public static ProblemStatsDocument ReadFoldable(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return ProblemStatsDocumentJsonConverter.ReadFoldable(json);
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

    /// <summary>
    /// Return a new document combining this one with <paramref name="other"/>
    /// — the document algebra behind the fold path, and the one combine rule
    /// this library owns. Per key: a problem present on both sides merges
    /// via <see cref="ProblemStats.Merge"/> (tallies summed field by field,
    /// <see cref="ProblemStats.LastQuizzed"/> the later of the two); a
    /// problem present on one side only passes through unchanged. Pure:
    /// neither input is mutated. Commutative and associative by construction
    /// — integer counts exactly, the equity-loss sum up to floating-point
    /// rounding — with <see cref="Empty"/> the identity on both sides; all
    /// pinned by test.
    /// </summary>
    /// <remarks>
    /// This is a value operation over two documents, not a conflict
    /// resolver: it does not know which file was the base, which is newer, or
    /// whether the two share a history. Concurrency machinery stays absent
    /// (single user, single writer). What is sound to merge is the
    /// consumer's call — today, a folded version-4 document into the set-
    /// aside version-3 base (SPEC-stats-identity.md §3, amended 2026-09-02;
    /// halheinrich/backgammon#187), where the two accrued disjoint sessions
    /// and summing is exactly right. Merging two copies of one history would
    /// double-count, and nothing here can tell.
    /// </remarks>
    /// <param name="other">The document to combine with this one.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="other"/> is <see langword="null"/>.
    /// </exception>
    public ProblemStatsDocument Merge(ProblemStatsDocument other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other._problems.IsEmpty)
            return this;
        if (_problems.IsEmpty)
            return other;

        var builder = _problems.ToBuilder();
        foreach (var (key, theirs) in other._problems)
        {
            builder[key] = builder.TryGetValue(key, out var mine)
                ? mine.Merge(theirs)
                : theirs;
        }
        return new(builder.ToImmutable());
    }
}
