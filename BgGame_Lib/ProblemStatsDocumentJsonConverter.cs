namespace BgGame_Lib;

using System.Diagnostics.CodeAnalysis;
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
///   "schemaVersion": 4,
///   "problems": {
///     "0,-2,0,0,0,0,5,0,3,0,0,0,-5,5,0,0,0,-3,0,-5,0,0,0,0,2,0/7a7/1c": {
///       "cubePair": {
///         "tally": { "submitted": 2, "correct": 1, "totalEquityLoss": 0.08 },
///         "lastQuizzed": "2026-07-18T19:04:11+00:00" } },
///     "0,-2,0,0,0,0,5,0,3,0,0,0,-5,5,0,0,0,-3,0,-5,0,0,0,0,2,0/7a7/1c/31": {
///       "checkerPlay": {
///         "tally": { "submitted": 3, "correct": 2, "totalEquityLoss": 0.125 },
///         "lastQuizzed": "2026-07-18T19:04:11+00:00" } }
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
/// <b>Each per-problem record nests under its answer-kind token</b> —
/// <c>"checkerPlay"</c> or <c>"cubePair"</c>, the v4 addition (SPEC-scoring.md
/// §4's answer-kind seam via SPEC-stats-identity.md §3's 2026-08-26 amendment;
/// halheinrich/backgammon#86). Today the kind is derivable from the key's own
/// grammar (dice ride on a play key and only there), so the wire token is
/// written from <see cref="ProblemKey.IsCubeDecision"/> and a read rejects a
/// record whose token disagrees with its key — the two spellings of one fact
/// must agree or the document is corrupt. The token is carried anyway because
/// the seam exists for the future where it stops being derivable: the
/// reserved equity-guess kind (halheinrich/backgammon#62) will let one
/// problem accrue records of more than one kind, arriving as a sibling kind
/// entry under the same key — extending this grammar rather than re-keying
/// the document. Until that arc lands, the read is maximally strict: exactly
/// one kind entry per problem, and the reserved token is an unknown kind
/// like any other.
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
/// <b>Reads are fail-loud, with one deliberate signal — and the signal covers
/// every retired version.</b> A recognised version <i>below</i>
/// <see cref="ProblemStatsDocument.CurrentSchemaVersion"/> is retired: v1 (the
/// <c>DecisionId</c>-keyed format), v2 (the <see cref="ProblemKey"/>-keyed
/// format from before the Jacoby rule entered money keys), and v3 (the
/// <see cref="ProblemKey"/>-keyed format from before answer kinds entered the
/// per-problem records). Each is recognised
/// by a shallow shape check — exactly one further property, holding that
/// version's body container, whose contents are never parsed (no migration
/// exists) — and throws <see cref="RetiredStatsSchemaException"/> carrying
/// <b>its own</b> version number, so the consumer can retire the file
/// honestly, under a per-version name, instead of surfacing a generic load
/// error (SPEC-stats-identity.md §3). The rule is a range, not a list, so a
/// version bump retires its predecessor with no second edit — see
/// <c>IsRetiredSchemaVersion</c>. Everything else throws plain
/// <see cref="JsonException"/>: a newer or unrecognised schema version, a
/// missing required property, an unknown or duplicate property at any level,
/// an invalid or duplicate key, a malformed date, or an impossible tally
/// (negative counts, correct &gt; submitted, negative equity loss) — corrupt
/// or foreign stats must never load as quietly-wrong lifetime data.
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
        int version = ReadSchemaVersion(ref reader);

        if (IsRetiredSchemaVersion(version))
            ThrowForRetiredVersionDocument(ref reader, version);

        return ReadCurrentVersionBody(ref reader);
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
    /// forbid.
    /// </summary>
    private static bool IsRetiredSchemaVersion(int version) =>
        version >= OldestRecognisedSchemaVersion
        && version < ProblemStatsDocument.CurrentSchemaVersion;

    /// <summary>
    /// Shallow recognition data for a retired schema version: the single
    /// property its body hangs from, that property's container token, and a
    /// phrase naming the format for the diagnostic. Enough to tell a genuine
    /// retired document from a corrupt one merely claiming its version — the
    /// body itself is skipped, never parsed.
    /// </summary>
    private readonly record struct RetiredSchemaShape(
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
    private static RetiredSchemaShape ShapeOfRetired(int version) => version switch
    {
        1 => new("decisions", JsonTokenType.StartArray, "the DecisionId-keyed format"),
        2 => new("problems", JsonTokenType.StartObject,
                 "the ProblemKey-keyed format from before the Jacoby rule entered money keys"),
        3 => new("problems", JsonTokenType.StartObject,
                 "the ProblemKey-keyed format from before answer kinds entered the per-problem records"),
        _ => new("problems", JsonTokenType.StartObject, "a superseded ProblemKey-keyed format"),
    };

    private static int ReadSchemaVersion(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out int version))
            throw new JsonException(
                $"Expected integer for 'schemaVersion', got {reader.TokenType}.");

        if (version > ProblemStatsDocument.CurrentSchemaVersion)
            throw new JsonException(
                $"Stats document has schema version {version}, newer than the highest " +
                $"version this library supports ({ProblemStatsDocument.CurrentSchemaVersion}).");
        if (version < OldestRecognisedSchemaVersion)
            throw new JsonException(
                $"Stats document has unsupported schema version {version}; this library " +
                $"recognises versions {OldestRecognisedSchemaVersion} through " +
                $"{ProblemStatsDocument.CurrentSchemaVersion}.");

        return version;
    }

    /// <summary>
    /// The deliberate retired-version recognition signal — never returns.
    /// Positioned after the version value, verifies the remainder shallowly
    /// against <paramref name="version"/>'s own shape — exactly one further
    /// property, holding that version's body container, whose contents are
    /// skipped and never parsed (no migration; clean break) — and throws
    /// <see cref="RetiredStatsSchemaException"/> carrying that version for the
    /// genuine article. A document that claims a retired version but is shaped
    /// otherwise throws plain <see cref="JsonException"/> instead: corrupt,
    /// not retired.
    /// </summary>
    [DoesNotReturn]
    private static void ThrowForRetiredVersionDocument(ref Utf8JsonReader reader, int version)
    {
        var shape = ShapeOfRetired(version);

        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName
            || reader.GetString() != shape.BodyProperty)
            throw new JsonException(
                $"Document claims retired schema version {version} but lacks its " +
                $"'{shape.BodyProperty}' property.");
        reader.Read();
        if (reader.TokenType != shape.BodyContainer)
            throw new JsonException(
                $"Document claims retired schema version {version} but " +
                $"'{shape.BodyProperty}' is not {shape.ContainerNoun}.");
        reader.Skip();
        if (!reader.Read() || reader.TokenType != JsonTokenType.EndObject)
            throw new JsonException(
                $"Document claims retired schema version {version} but carries " +
                $"properties beyond '{shape.BodyProperty}'.");

        throw new RetiredStatsSchemaException(
            version,
            $"Stats document has retired schema version {version} " +
            $"({shape.Description}). There is no migration: retire the file " +
            $"and start a fresh version-{ProblemStatsDocument.CurrentSchemaVersion} document.");
    }

    private static ProblemStatsDocument ReadCurrentVersionBody(ref Utf8JsonReader reader)
    {
        List<ProblemStats>? problems = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "problems" when problems is null:
                    problems = ReadProblems(ref reader);
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

    private static List<ProblemStats> ReadProblems(ref Utf8JsonReader reader)
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
            problems.Add(ReadProblemRecord(ref reader, key));
        }

        return problems;
    }

    /// <summary>
    /// The answer-kind wire tokens (SPEC-scoring.md §4's kinds; the
    /// equity-guess kind is <i>reserved</i> there, not spelled here — until
    /// its arc lands, its token is an unknown kind like any other).
    /// Hand-written property names, deliberately not a serializer-routed
    /// enum: this converter owns its whole tree, and the context's closure
    /// test pins that nothing here routes through the options.
    /// </summary>
    private const string CheckerPlayKindToken = "checkerPlay";

    /// <inheritdoc cref="CheckerPlayKindToken"/>
    private const string CubePairKindToken = "cubePair";

    /// <summary>
    /// The kind token a key's record writes under — the one derivation of
    /// kind from the key's own grammar (dice ride on a play key and only
    /// there, so <see cref="ProblemKey.IsCubeDecision"/> is the fact's
    /// key-side spelling). The read side holds each record's token against
    /// this same derivation, so the two spellings cannot drift.
    /// </summary>
    private static string KindTokenFor(ProblemKey key) =>
        key.IsCubeDecision ? CubePairKindToken : CheckerPlayKindToken;

    /// <summary>
    /// One problem's value: an object holding <b>exactly one</b> answer-kind
    /// entry whose token must agree with the key's grammar. Anything else —
    /// no kind entry, a second one, an unknown token, a token disagreeing
    /// with the key — is corrupt. When the reserved equity-guess kind
    /// arrives, this is the read that widens (a second sibling entry under
    /// the same key) — the document shape does not change again.
    /// </summary>
    private static ProblemStats ReadProblemRecord(ref Utf8JsonReader reader, ProblemKey key)
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
        var stats = ReadKindRecord(ref reader, key);

        if (!reader.Read() || reader.TokenType != JsonTokenType.EndObject)
            throw new JsonException($"Problem '{key}' carries more than one answer-kind record.");

        return stats;
    }

    private static ProblemStats ReadKindRecord(ref Utf8JsonReader reader, ProblemKey key)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException(
                $"Expected object for problem '{key}'s answer-kind record, got {reader.TokenType}.");

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
            writer.WriteStartObject(KindTokenFor(stats.Key));
            writer.WriteStartObject("tally");
            writer.WriteNumber("submitted", stats.Tally.Submitted);
            writer.WriteNumber("correct", stats.Tally.Correct);
            writer.WriteNumber("totalEquityLoss", stats.Tally.TotalEquityLoss);
            writer.WriteEndObject();
            writer.WriteString("lastQuizzed", stats.LastQuizzed);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
