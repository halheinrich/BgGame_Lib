namespace BgGame_Lib;

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BgDataTypes_Lib;

/// <summary>
/// Serialises <see cref="ProblemStatsDocument"/> as its versioned persistent
/// wire format. Bundled via type-level <c>[JsonConverter]</c> on the document
/// (the same pattern as <c>ProblemKeyJsonConverter</c> in BgDataTypes_Lib), so
/// consumers do not need to register anything on their
/// <see cref="JsonSerializerOptions"/>.
///
/// <para>
/// The whole tree is hand-written with fixed property names — the persisted
/// format is a file contract and must not vary with the consumer's options
/// (naming policy etc.). Whitespace is the one thing options still control
/// (<see cref="JsonSerializerOptions.WriteIndented"/> lives on the writer the
/// serializer creates), so byte-stable files additionally need fixed
/// consumer-side options. Wire shape (schema version
/// <see cref="ProblemStatsDocument.CurrentSchemaVersion"/>):
/// </para>
///
/// <code>
/// {
///   "schemaVersion": 3,
///   "problems": {
///     "0,-2,0,0,0,0,5,0,3,0,0,0,-5,5,0,0,0,-3,0,-5,0,0,0,0,2,0/7a7/1c": {
///       "tally": { "submitted": 2, "correct": 1, "totalEquityLoss": 0.08 },
///       "lastQuizzed": "2026-07-18T19:04:11+00:00" },
///     "0,-2,0,0,0,0,5,0,3,0,0,0,-5,5,0,0,0,-3,0,-5,0,0,0,0,2,0/7a7/1c/31": {
///       "tally": { "submitted": 3, "correct": 2, "totalEquityLoss": 0.125 },
///       "lastQuizzed": "2026-07-18T19:04:11+00:00" }
///   }
/// }
/// </code>
///
/// <para>
/// Problems are a JSON <b>object</b> keyed by each record's canonical
/// <see cref="ProblemKey"/> string — the map shape
/// <c>ProblemKeyJsonConverter</c>'s property-name support exists for (v1's
/// array-of-elements shape existed only because <c>DecisionId</c> lacked
/// that support). Writes order the map by canonical key (ordinal, the key
/// type's own ordering) so a given document always serializes to the same
/// content.
/// </para>
///
/// <para>
/// <b>Each per-problem value is the bare record</b> — the tally object plus
/// the last-quizzed date, and no answer-kind discriminator. The kind of
/// decision a record tallies is derivable from its key's own grammar (dice
/// ride on a play key and only there — <see cref="ProblemKey.IsCubeDecision"/>),
/// and a second spelling of a fact the key already carries is what the
/// interim version 4 wrapper was; SPEC-scoring.md §4's 2026-09-02 amendment
/// retires it and this version-3 shape is reinstated as current
/// (SPEC-stats-identity.md §3; halheinrich/backgammon#187), byte-identical to
/// the v3 files in production.
/// </para>
///
/// <para>
/// <b>Reserved, not built: the equity-estimate fields.</b> SPEC-scoring.md
/// §4 reserves the equity-estimate mode's stats
/// (halheinrich/backgammon#62) as flat sibling fields on this same record —
/// <c>equityEstimates</c> (a count) and <c>totalEquityEstimateError</c> (a
/// sum of absolute errors, never added to equity loss) — sharing the
/// record's one <c>lastQuizzed</c>. They are <b>additive under this
/// version</b>: when they land, the reader treats an absent field as zero
/// and the schema version does not move, so every v3 file written before
/// them keeps reading. Nothing added to this reader may make an absent
/// optional field a break; the fail-loud posture below is about unknown
/// and malformed content, not about fields a later reader learns to
/// expect. Until that arc lands the two names are not in the grammar and
/// read as unknown properties like any other.
/// </para>
///
/// <para>
/// <b><c>schemaVersion</c> is contractually the first property.</b> The
/// version gates how everything after it is parsed, so the reader dispatches
/// on it before touching anything else; a document whose first property is
/// anything else is rejected as corrupt or foreign. Every file this library's
/// writers have ever produced — every version, retired ones included —
/// satisfies this.
/// </para>
///
/// <para>
/// <b>Reads are fail-loud, with two deliberate signals.</b> A recognised
/// version <i>below</i> <see cref="ProblemStatsDocument.CurrentSchemaVersion"/>
/// is <b>retired</b>: v1 (the <c>DecisionId</c>-keyed format) and v2 (the
/// <see cref="ProblemKey"/>-keyed format from before the Jacoby rule entered
/// money keys). Each is recognised by a shallow shape check — exactly one
/// further property, holding that version's body container, whose contents
/// are never parsed (no migration exists) — and throws
/// <see cref="RetiredStatsSchemaException"/> carrying <b>its own</b> version
/// number, so the consumer can retire the file honestly, under a per-version
/// name, instead of surfacing a generic load error (SPEC-stats-identity.md
/// §3). The rule is a range, not a list, so a version bump retires its
/// predecessor with no second edit — see <c>IsRetiredSchemaVersion</c>.
/// Version <see cref="ProblemStatsDocument.FoldableSchemaVersion"/> (4), the
/// interim answer-kind format that never reached production, is
/// <b>foldable</b>: the same shallow check, then
/// <see cref="FoldableStatsSchemaException"/> — the retired signal's sibling
/// — and the consumer reads the file with
/// <see cref="ProblemStatsDocument.ReadFoldable(string)"/> and combines it
/// with <see cref="ProblemStatsDocument.Merge"/>. The dispatch order is the
/// retired range, then the foldable version, then the newer-than-current
/// refusal: 4 is the one version above current that is not refused as
/// newer. Everything else throws plain <see cref="JsonException"/>: a newer
/// or unrecognised schema version, a missing required property, an unknown
/// or duplicate property at any level, an invalid or duplicate key, a
/// malformed date, or an impossible tally (negative counts, correct &gt;
/// submitted, negative equity loss) — corrupt or foreign stats must never
/// load as quietly-wrong lifetime data.
/// </para>
///
/// <para>
/// Public, like every converter a type-level <c>[JsonConverter]</c> here
/// names (halheinrich/backgammon#129): a downstream
/// <see cref="JsonSerializerContext"/> that declares
/// <see cref="ProblemStatsDocument"/> instantiates this converter from its
/// own generated code, so an internal converter would fail the
/// <i>consumer's</i> compile with SYSLIB1220/SYSLIB1030 — and BgQuiz's stats
/// store is exactly that consumer. Stateless and sealed — the public
/// <c>[JsonConverter]</c> attribute on <see cref="ProblemStatsDocument"/>
/// already named it, so this publishes no decision that was not already
/// contractual.
/// </para>
/// </summary>
public sealed class ProblemStatsDocumentJsonConverter : JsonConverter<ProblemStatsDocument>
{
    /// <inheritdoc/>
    public override ProblemStatsDocument? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        int version = ReadDocumentHead(ref reader);

        if (IsRetiredSchemaVersion(version))
            ThrowForRetiredVersionDocument(ref reader, version);
        if (version == ProblemStatsDocument.FoldableSchemaVersion)
            ThrowForFoldableVersionDocument(ref reader);

        return ReadBody(ref reader, ProblemValueShape.Bare);
    }

    /// <summary>
    /// The fold reader behind
    /// <see cref="ProblemStatsDocument.ReadFoldable(string)"/>: parses a
    /// whole document text that must declare
    /// <see cref="ProblemStatsDocument.FoldableSchemaVersion"/>, reading each
    /// per-problem value through its answer-kind wrapper into the current
    /// record shape. Its own <see cref="Utf8JsonReader"/>, deliberately not
    /// a <see cref="JsonSerializer"/> call: the ordinary read path's job is
    /// to <i>signal</i> a v4, and a converter that read v4 quietly would
    /// erase the signal the consumer's fold depends on. Rejects trailing
    /// content after the root object, as the serializer would, and holds
    /// the serializer's exception contract: malformed JSON <i>text</i>, which
    /// the reader reports through its own internal
    /// <see cref="JsonException"/> subtype, is re-thrown as plain
    /// <see cref="JsonException"/> exactly as
    /// <see cref="JsonSerializer"/> re-throws it, so a consumer's exact-type
    /// handling sees one type whichever layer the failure came from.
    /// </summary>
    internal static ProblemStatsDocument ReadFoldable(string json)
    {
        try
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
            if (!reader.Read())
                throw new JsonException("Expected a stats document, got no JSON at all.");

            int version = ReadDocumentHead(ref reader);
            if (version != ProblemStatsDocument.FoldableSchemaVersion)
                throw new JsonException(
                    $"ReadFoldable reads schema version {ProblemStatsDocument.FoldableSchemaVersion} " +
                    $"only; the document declares version {version}.");

            var document = ReadBody(ref reader, ProblemValueShape.AnswerKindWrapped);

            if (reader.Read())
                throw new JsonException(
                    $"Stats document is followed by trailing content ({reader.TokenType}).");

            return document;
        }
        catch (JsonException ex) when (ex.GetType() != typeof(JsonException))
        {
            throw new JsonException(ex.Message, ex);
        }
    }

    /// <summary>
    /// Positioned on the document's first token, requires the root object
    /// and the version-first property, and returns the schema version after
    /// <see cref="ReadSchemaVersion"/>'s range checks. Leaves the reader on
    /// the version value.
    /// </summary>
    private static int ReadDocumentHead(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException(
                $"Expected object for ProblemStatsDocument, got {reader.TokenType}.");

        // Version-first dispatch: the version decides how the rest is read,
        // so it must come first — anything else reads as corrupt/foreign.
        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName
            || reader.GetString() != "schemaVersion")
            throw new JsonException(
                "Expected 'schemaVersion' as the first property of a stats document.");
        reader.Read();
        return ReadSchemaVersion(ref reader);
    }

    /// <summary>
    /// The oldest schema version this library recognises at all. Below it a
    /// document is unrecognised rather than retired, and fails loud exactly as
    /// a version above the current one does.
    /// </summary>
    private const int OldestRecognisedSchemaVersion = 1;

    /// <summary>
    /// The retirement rule: <b>every recognised version below the current
    /// one</b> — a range, deliberately, not a list of versions. Bumping
    /// <see cref="ProblemStatsDocument.CurrentSchemaVersion"/> must retire the
    /// version it supersedes with no second edit anywhere: an omitted edit
    /// would drop that version's holders into the generic fail-loud path with
    /// their stats silently dead — the one outcome SPEC-stats-identity.md
    /// §3's "deliberate recognition, small and mandatory" ruling exists to
    /// forbid. The foldable version sits above current and is outside this
    /// range by construction; a future bump past it would fold it into the
    /// range and retire it, which is that bump's decision to confirm or
    /// override.
    /// </summary>
    private static bool IsRetiredSchemaVersion(int version) =>
        version >= OldestRecognisedSchemaVersion
        && version < ProblemStatsDocument.CurrentSchemaVersion;

    /// <summary>
    /// Shallow recognition data for a schema version the ordinary read path
    /// signals rather than reads: the single property its body hangs from,
    /// that property's container token, and a phrase naming the format for
    /// the diagnostic. Enough to tell a genuine document of that version
    /// from a corrupt one merely claiming it — the body itself is skipped,
    /// never parsed here.
    /// </summary>
    private readonly record struct VersionShape(
        string BodyProperty,
        JsonTokenType BodyContainer,
        string Description)
    {
        /// <summary>The body container named for a diagnostic message.</summary>
        public string ContainerNoun =>
            BodyContainer == JsonTokenType.StartArray ? "an array" : "an object";
    }

    /// <summary>
    /// The retired versions' shapes. Total over
    /// <see cref="IsRetiredSchemaVersion"/> by construction: the fallback arm
    /// describes the <c>problems</c>-map body every version from 2 onward
    /// carries, so a future
    /// <see cref="ProblemStatsDocument.CurrentSchemaVersion"/> bump retires its
    /// predecessor correctly without touching this table — a row is added
    /// only to sharpen the diagnostic.
    /// </summary>
    private static VersionShape ShapeOfRetired(int version) => version switch
    {
        1 => new("decisions", JsonTokenType.StartArray, "the DecisionId-keyed format"),
        2 => new("problems", JsonTokenType.StartObject,
                 "the ProblemKey-keyed format from before the Jacoby rule entered money keys"),
        _ => new("problems", JsonTokenType.StartObject, "a superseded ProblemKey-keyed format"),
    };

    /// <summary>The foldable version's shape: the same problems map, each value kind-wrapped.</summary>
    private static readonly VersionShape FoldableShape = new(
        "problems", JsonTokenType.StartObject,
        "the ProblemKey-keyed format with each per-problem record wrapped in its answer kind");

    private static int ReadSchemaVersion(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out int version))
            throw new JsonException(
                $"Expected integer for 'schemaVersion', got {reader.TokenType}.");

        // The foldable version is checked before the newer-than-current
        // refusal: it is the one version above current that is recognised.
        if (version > ProblemStatsDocument.CurrentSchemaVersion
            && version != ProblemStatsDocument.FoldableSchemaVersion)
            throw new JsonException(
                $"Stats document has schema version {version}, newer than the highest " +
                $"version this library supports ({ProblemStatsDocument.CurrentSchemaVersion}).");
        if (version < OldestRecognisedSchemaVersion)
            throw new JsonException(
                $"Stats document has unsupported schema version {version}; this library " +
                $"recognises versions {OldestRecognisedSchemaVersion} through " +
                $"{ProblemStatsDocument.CurrentSchemaVersion}, and folds version " +
                $"{ProblemStatsDocument.FoldableSchemaVersion}.");

        return version;
    }

    /// <summary>
    /// Positioned after the version value, verifies the remainder shallowly
    /// against <paramref name="shape"/> — exactly one further property,
    /// holding that version's body container, whose contents are skipped and
    /// never parsed — and leaves the reader on the root object's end. A
    /// document that claims <paramref name="version"/> but is shaped
    /// otherwise throws plain <see cref="JsonException"/>: corrupt, not a
    /// genuine document of that version.
    /// </summary>
    private static void RequireShallowShape(ref Utf8JsonReader reader, int version, VersionShape shape)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName
            || reader.GetString() != shape.BodyProperty)
            throw new JsonException(
                $"Document claims schema version {version} but lacks its " +
                $"'{shape.BodyProperty}' property.");
        reader.Read();
        if (reader.TokenType != shape.BodyContainer)
            throw new JsonException(
                $"Document claims schema version {version} but " +
                $"'{shape.BodyProperty}' is not {shape.ContainerNoun}.");
        reader.Skip();
        if (!reader.Read() || reader.TokenType != JsonTokenType.EndObject)
            throw new JsonException(
                $"Document claims schema version {version} but carries " +
                $"properties beyond '{shape.BodyProperty}'.");
    }

    /// <summary>
    /// The deliberate retired-version recognition signal — never returns.
    /// Shallow shape check per <see cref="RequireShallowShape"/>, then
    /// <see cref="RetiredStatsSchemaException"/> carrying
    /// <paramref name="version"/> (no migration; clean break).
    /// </summary>
    [DoesNotReturn]
    private static void ThrowForRetiredVersionDocument(ref Utf8JsonReader reader, int version)
    {
        var shape = ShapeOfRetired(version);
        RequireShallowShape(ref reader, version, shape);

        throw new RetiredStatsSchemaException(
            version,
            $"Stats document has retired schema version {version} " +
            $"({shape.Description}). There is no migration: retire the file " +
            $"and start a fresh version-{ProblemStatsDocument.CurrentSchemaVersion} document.");
    }

    /// <summary>
    /// The deliberate foldable-version recognition signal — never returns.
    /// The same shallow shape check as the retired signal, then
    /// <see cref="FoldableStatsSchemaException"/> carrying
    /// <see cref="ProblemStatsDocument.FoldableSchemaVersion"/>. Shallow on
    /// purpose: the records are read, fully validated, on the fold path
    /// (<see cref="ReadFoldable"/>), where a corrupt v4 surfaces as corrupt.
    /// </summary>
    [DoesNotReturn]
    private static void ThrowForFoldableVersionDocument(ref Utf8JsonReader reader)
    {
        const int version = ProblemStatsDocument.FoldableSchemaVersion;
        RequireShallowShape(ref reader, version, FoldableShape);

        throw new FoldableStatsSchemaException(
            version,
            $"Stats document has schema version {version} ({FoldableShape.Description}), " +
            $"which folds into the current version-{ProblemStatsDocument.CurrentSchemaVersion} " +
            $"document rather than retiring: read it with ProblemStatsDocument.ReadFoldable " +
            $"and Merge the result into the current document.");
    }

    /// <summary>
    /// How a per-problem value is laid out: the current bare record, or the
    /// foldable version's record wrapped in its single answer-kind entry.
    /// The one point where the current and fold reads differ — everything
    /// above the value (root, map, keys) is shared.
    /// </summary>
    private enum ProblemValueShape
    {
        /// <summary>The current shape: the record itself.</summary>
        Bare,

        /// <summary>The foldable v4 shape: one kind entry holding the record.</summary>
        AnswerKindWrapped,
    }

    /// <summary>
    /// Positioned on the version value, reads the rest of the root object —
    /// the <c>problems</c> map and nothing else — leaving the reader on the
    /// root object's end.
    /// </summary>
    private static ProblemStatsDocument ReadBody(ref Utf8JsonReader reader, ProblemValueShape shape)
    {
        List<ProblemStats>? problems = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "problems" when problems is null:
                    problems = ReadProblems(ref reader, shape);
                    break;
                case "problems":
                    throw new JsonException("Duplicate ProblemStatsDocument property 'problems'.");
                default:
                    throw new JsonException(
                        $"Unknown ProblemStatsDocument property '{name}'.");
            }
        }

        if (problems is null)
            throw new JsonException("Missing required property 'problems'.");

        // Duplicate keys were rejected in ReadProblems, so FromStats cannot throw.
        return ProblemStatsDocument.FromStats(problems);
    }

    private static List<ProblemStats> ReadProblems(ref Utf8JsonReader reader, ProblemValueShape shape)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException(
                $"Expected object for 'problems', got {reader.TokenType}.");

        var problems = new List<ProblemStats>();
        var seen = new HashSet<ProblemKey>();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            if (!ProblemKey.TryParse(name, provider: null, out var key))
                throw new JsonException($"Invalid ProblemKey canonical form: '{name}'.");
            if (!seen.Add(key))
                throw new JsonException($"Duplicate problem key '{key}'.");
            reader.Read();
            problems.Add(shape == ProblemValueShape.AnswerKindWrapped
                ? ReadAnswerKindWrappedRecord(ref reader, key)
                : ReadRecord(ref reader, key));
        }

        return problems;
    }

    // -----------------------------------------------------------------------
    //  The foldable v4 value layer — read on the fold path only, never
    //  written.
    // -----------------------------------------------------------------------

    /// <summary>
    /// The interim version 4's answer-kind wire tokens (SPEC-scoring.md §4's
    /// kinds as that version spelled them; the equity-guess kind was reserved,
    /// never spelled). Read by the fold path only: the current format carries
    /// no kind discriminator and the writer never emits these.
    /// </summary>
    private const string CheckerPlayKindToken = "checkerPlay";

    /// <inheritdoc cref="CheckerPlayKindToken"/>
    private const string CubePairKindToken = "cubePair";

    /// <summary>
    /// The kind token a v4 record sits under, derived from the key's own
    /// grammar (dice ride on a play key and only there, so
    /// <see cref="ProblemKey.IsCubeDecision"/> is the fact's key-side
    /// spelling) — the same derivation v4's writer used, held against each
    /// record's token on the fold so a wrapper disagreeing with its key reads
    /// as corrupt.
    /// </summary>
    private static string KindTokenFor(ProblemKey key) =>
        key.IsCubeDecision ? CubePairKindToken : CheckerPlayKindToken;

    /// <summary>
    /// One v4 problem value: an object holding <b>exactly one</b> answer-kind
    /// entry whose token must agree with the key's grammar, wrapping the
    /// record. Anything else — no kind entry, a second one, an unknown token
    /// (the reserved equity-guess token included), a token disagreeing with
    /// the key, the bare v3 record with no wrapper — is corrupt. As strict as
    /// v4's own reader was: the fold reads exactly what v4 wrote.
    /// </summary>
    private static ProblemStats ReadAnswerKindWrappedRecord(ref Utf8JsonReader reader, ProblemKey key)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException(
                $"Expected object for problem '{key}', got {reader.TokenType}.");

        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName)
            throw new JsonException($"Problem '{key}' is missing its answer-kind record.");

        var kind = reader.GetString();
        if (kind is not (CheckerPlayKindToken or CubePairKindToken))
            throw new JsonException($"Unknown answer kind '{kind}' for problem '{key}'.");
        if (kind != KindTokenFor(key))
            throw new JsonException(
                $"Problem '{key}' is a {(key.IsCubeDecision ? "cube decision" : "checker play")} " +
                $"but its record is under answer kind '{kind}'.");

        reader.Read();
        var stats = ReadRecord(ref reader, key);

        if (!reader.Read() || reader.TokenType != JsonTokenType.EndObject)
            throw new JsonException($"Problem '{key}' carries more than one answer-kind record.");

        return stats;
    }

    // -----------------------------------------------------------------------
    //  The record — the current per-problem value, and the body of a v4
    //  kind entry.
    // -----------------------------------------------------------------------

    private static ProblemStats ReadRecord(ref Utf8JsonReader reader, ProblemKey key)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException(
                $"Expected object for problem '{key}', got {reader.TokenType}.");

        ScoreSegment? tally = null;
        DateTimeOffset? lastQuizzed = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "tally":
                    tally = ReadTally(ref reader);
                    break;
                case "lastQuizzed":
                    lastQuizzed = ReadLastQuizzed(ref reader);
                    break;
                default:
                    throw new JsonException($"Unknown problem property '{name}'.");
            }
        }

        if (tally is null)
            throw new JsonException($"Problem '{key}' is missing required property 'tally'.");
        if (lastQuizzed is null)
            throw new JsonException($"Problem '{key}' is missing required property 'lastQuizzed'.");

        return new ProblemStats(key, tally, lastQuizzed.Value);
    }

    private static ScoreSegment ReadTally(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Expected object for 'tally', got {reader.TokenType}.");

        int? submitted = null;
        int? correct = null;
        double? totalEquityLoss = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "submitted":
                    submitted = ReadInt(ref reader, "submitted");
                    break;
                case "correct":
                    correct = ReadInt(ref reader, "correct");
                    break;
                case "totalEquityLoss":
                    if (reader.TokenType != JsonTokenType.Number)
                        throw new JsonException(
                            $"Expected number for 'totalEquityLoss', got {reader.TokenType}.");
                    totalEquityLoss = reader.GetDouble();
                    break;
                default:
                    throw new JsonException($"Unknown tally property '{name}'.");
            }
        }

        if (submitted is null)
            throw new JsonException("Tally is missing required property 'submitted'.");
        if (correct is null)
            throw new JsonException("Tally is missing required property 'correct'.");
        if (totalEquityLoss is null)
            throw new JsonException("Tally is missing required property 'totalEquityLoss'.");

        if (submitted < 0 || correct < 0)
            throw new JsonException(
                $"Tally counts must be non-negative (submitted {submitted}, correct {correct}).");
        if (correct > submitted)
            throw new JsonException(
                $"Tally has correct ({correct}) greater than submitted ({submitted}).");
        if (totalEquityLoss < 0.0)
            throw new JsonException(
                $"Tally has negative totalEquityLoss ({totalEquityLoss}).");

        return new ScoreSegment(submitted.Value, correct.Value, totalEquityLoss.Value);
    }

    private static int ReadInt(ref Utf8JsonReader reader, string propertyName)
    {
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out int value))
            throw new JsonException(
                $"Expected integer for '{propertyName}', got {reader.TokenType}.");
        return value;
    }

    private static DateTimeOffset ReadLastQuizzed(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.String || !reader.TryGetDateTimeOffset(out var value))
            throw new JsonException(
                $"Expected ISO 8601 date-time string for 'lastQuizzed', got {reader.TokenType}.");
        return value;
    }

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer,
        ProblemStatsDocument value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", ProblemStatsDocument.CurrentSchemaVersion);
        writer.WriteStartObject("problems");

        foreach (var stats in value.Problems.Values.OrderBy(s => s.Key))
        {
            writer.WriteStartObject(stats.Key.ToString());
            writer.WriteStartObject("tally");
            writer.WriteNumber("submitted", stats.Tally.Submitted);
            writer.WriteNumber("correct", stats.Tally.Correct);
            writer.WriteNumber("totalEquityLoss", stats.Tally.TotalEquityLoss);
            writer.WriteEndObject();
            writer.WriteString("lastQuizzed", stats.LastQuizzed);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
