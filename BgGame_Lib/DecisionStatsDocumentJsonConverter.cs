namespace BgGame_Lib;

using System.Text.Json;
using System.Text.Json.Serialization;
using BgDataTypes_Lib;

/// <summary>
/// Serialises <see cref="DecisionStatsDocument"/> as its versioned persistent
/// wire format. Bundled via type-level <c>[JsonConverter]</c> on the document
/// (the same pattern as <c>DecisionIdJsonConverter</c> in BgDataTypes_Lib), so
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
/// <see cref="DecisionStatsDocument.CurrentSchemaVersion"/>):
/// </para>
///
/// <code>
/// {
///   "schemaVersion": 1,
///   "decisions": [
///     { "id": "file.xg:g3:m12:play",
///       "tally": { "submitted": 3, "correct": 2, "totalEquityLoss": 0.125 },
///       "lastQuizzed": "2026-07-18T19:04:11+00:00" }
///   ]
/// }
/// </code>
///
/// <para>
/// Decisions are a JSON <b>array</b> keyed by each element's canonical
/// <see cref="DecisionId"/> string, not an id-keyed JSON object —
/// <c>DecisionIdJsonConverter</c> does not implement property-name conversion,
/// and a list round-trips without it. Writes order the array by canonical id
/// (ordinal) so a given document always serializes to the same content.
/// </para>
///
/// <para>
/// <b>Reads are fail-loud.</b> A schema version other than
/// <see cref="DecisionStatsDocument.CurrentSchemaVersion"/> (a version bump is
/// the format's only evolution mechanism), a missing required property, an
/// unknown property at any level, an invalid or duplicate id, a malformed
/// date, or an impossible tally (negative counts, correct &gt; submitted,
/// negative equity loss) all throw <see cref="JsonException"/> — corrupt or
/// foreign stats must never load as quietly-wrong lifetime data.
/// </para>
/// </summary>
internal sealed class DecisionStatsDocumentJsonConverter : JsonConverter<DecisionStatsDocument>
{
    public override DecisionStatsDocument? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException(
                $"Expected object for DecisionStatsDocument, got {reader.TokenType}.");

        int? schemaVersion = null;
        List<DecisionStats>? decisions = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "schemaVersion":
                    schemaVersion = ReadSchemaVersion(ref reader);
                    break;
                case "decisions":
                    decisions = ReadDecisions(ref reader);
                    break;
                default:
                    throw new JsonException(
                        $"Unknown DecisionStatsDocument property '{name}'.");
            }
        }

        if (schemaVersion is null)
            throw new JsonException("Missing required property 'schemaVersion'.");
        if (decisions is null)
            throw new JsonException("Missing required property 'decisions'.");

        // Duplicate ids were rejected in ReadDecisions, so FromStats cannot throw.
        return DecisionStatsDocument.FromStats(decisions);
    }

    private static int ReadSchemaVersion(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out int version))
            throw new JsonException(
                $"Expected integer for 'schemaVersion', got {reader.TokenType}.");

        if (version > DecisionStatsDocument.CurrentSchemaVersion)
            throw new JsonException(
                $"Stats document has schema version {version}, newer than the highest " +
                $"version this library supports ({DecisionStatsDocument.CurrentSchemaVersion}).");
        if (version != DecisionStatsDocument.CurrentSchemaVersion)
            throw new JsonException(
                $"Stats document has unsupported schema version {version}; " +
                $"expected {DecisionStatsDocument.CurrentSchemaVersion}.");

        return version;
    }

    private static List<DecisionStats> ReadDecisions(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException(
                $"Expected array for 'decisions', got {reader.TokenType}.");

        var decisions = new List<DecisionStats>();
        var seen = new HashSet<DecisionId>();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var stats = ReadDecision(ref reader);
            if (!seen.Add(stats.Id))
                throw new JsonException($"Duplicate decision id '{stats.Id}'.");
            decisions.Add(stats);
        }

        return decisions;
    }

    private static DecisionStats ReadDecision(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException(
                $"Expected object for a decisions element, got {reader.TokenType}.");

        DecisionId? id = null;
        ScoreSegment? tally = null;
        DateTimeOffset? lastQuizzed = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "id":
                    id = ReadId(ref reader);
                    break;
                case "tally":
                    tally = ReadTally(ref reader);
                    break;
                case "lastQuizzed":
                    lastQuizzed = ReadLastQuizzed(ref reader);
                    break;
                default:
                    throw new JsonException($"Unknown decision property '{name}'.");
            }
        }

        if (id is null)
            throw new JsonException("Decision element is missing required property 'id'.");
        if (tally is null)
            throw new JsonException("Decision element is missing required property 'tally'.");
        if (lastQuizzed is null)
            throw new JsonException("Decision element is missing required property 'lastQuizzed'.");

        return new DecisionStats(id, tally, lastQuizzed.Value);
    }

    private static DecisionId ReadId(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected string for 'id', got {reader.TokenType}.");

        var s = reader.GetString();
        if (!DecisionId.TryParse(s, provider: null, out var id))
            throw new JsonException($"Invalid DecisionId canonical form: '{s}'.");

        return id;
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

    public override void Write(
        Utf8JsonWriter writer,
        DecisionStatsDocument value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", DecisionStatsDocument.CurrentSchemaVersion);
        writer.WriteStartArray("decisions");

        foreach (var stats in value.Decisions.Values
            .OrderBy(s => s.Id.ToString(), StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("id", stats.Id.ToString());
            writer.WriteStartObject("tally");
            writer.WriteNumber("submitted", stats.Tally.Submitted);
            writer.WriteNumber("correct", stats.Tally.Correct);
            writer.WriteNumber("totalEquityLoss", stats.Tally.TotalEquityLoss);
            writer.WriteEndObject();
            writer.WriteString("lastQuizzed", stats.LastQuizzed);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
