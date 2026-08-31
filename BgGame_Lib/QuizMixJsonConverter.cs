namespace BgGame_Lib;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Serialises <see cref="QuizMix"/> as its versioned persistent wire format.
/// Bundled via type-level <c>[JsonConverter]</c> on the mix (the same pattern
/// as <see cref="ProblemStatsDocumentJsonConverter"/>), so consumers do not
/// need to register anything on their <see cref="JsonSerializerOptions"/>.
///
/// <para>
/// The whole tree is hand-written with fixed property names — the persisted
/// format is a storage contract and must not vary with the consumer's options
/// (naming policy etc.). All properties are always written and always
/// required on read (<c>quizLength</c> is written as the JSON <c>null</c>
/// when unset). Entries are written in declared order — order is contractual
/// (earlier entries draw first under overlap), so the wire preserves it.
/// Wire shape (schema version <see cref="QuizMix.CurrentSchemaVersion"/>):
/// </para>
///
/// <code>
/// {
///   "schemaVersion": 1,
///   "randomOrder": true,
///   "quizLength": 25,
///   "entries": [
///     { "category": { "kind": "NeverSeen" }, "percent": 50 },
///     { "category": { "kind": "SeenFewerThan", "value": 3 }, "percent": 20 },
///     { "category": { "kind": "EverythingElse" }, "percent": 30 }
///   ]
/// }
/// </code>
///
/// <para>
/// <c>kind</c> is the exact <see cref="QuizCategoryKind"/> declaration name
/// (case-sensitive; numeric forms rejected). <c>value</c> is required for
/// parameterized kinds and forbidden for parameterless ones; integer-kind
/// values must be integral. Category reconstruction routes through the
/// internal <c>QuizCategory.Create</c> seam, and the mix through the public
/// <see cref="QuizMix"/> constructor, so every wire-level bound has the same
/// single definition as the in-memory one.
/// </para>
///
/// <para>
/// <b>Reads are fail-loud.</b> A schema version other than
/// <see cref="QuizMix.CurrentSchemaVersion"/> (with a distinguished
/// "newer than this library supports" message — a version bump is the
/// format's only evolution mechanism), a missing required property, an
/// unknown property at any level, an invalid kind or value, an out-of-range
/// percent or length, a duplicate category, or percents not summing to 100
/// all throw <see cref="JsonException"/> — a corrupt or foreign mix must
/// never load as a quietly-different composition.
/// <see cref="QuizMix.TryFromJson"/> is the tolerant restore path.
/// </para>
/// </summary>
internal sealed class QuizMixJsonConverter : JsonConverter<QuizMix>
{
    public override QuizMix? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Expected object for QuizMix, got {reader.TokenType}.");

        int? schemaVersion = null;
        bool? randomOrder = null;
        int? quizLength = null;
        bool quizLengthSeen = false;
        List<QuizMixEntry>? entries = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "schemaVersion":
                    schemaVersion = ReadSchemaVersion(ref reader);
                    break;
                case "randomOrder":
                    if (reader.TokenType is not (JsonTokenType.True or JsonTokenType.False))
                        throw new JsonException(
                            $"Expected boolean for 'randomOrder', got {reader.TokenType}.");
                    randomOrder = reader.GetBoolean();
                    break;
                case "quizLength":
                    quizLength = ReadQuizLength(ref reader);
                    quizLengthSeen = true;
                    break;
                case "entries":
                    entries = ReadEntries(ref reader);
                    break;
                default:
                    throw new JsonException($"Unknown QuizMix property '{name}'.");
            }
        }

        if (schemaVersion is null)
            throw new JsonException("Missing required property 'schemaVersion'.");
        if (randomOrder is null)
            throw new JsonException("Missing required property 'randomOrder'.");
        if (!quizLengthSeen)
            throw new JsonException("Missing required property 'quizLength'.");
        if (entries is null)
            throw new JsonException("Missing required property 'entries'.");

        try
        {
            return new QuizMix(entries, quizLength, randomOrder.Value);
        }
        catch (ArgumentException ex)
        {
            // Set-level rule violations (duplicate category, sum ≠ 100,
            // length bounds, length-without-entries) — same single-sourced
            // rules as in-memory construction, surfaced as read failures.
            throw new JsonException(ex.Message, ex);
        }
    }

    private static int ReadSchemaVersion(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out int version))
            throw new JsonException(
                $"Expected integer for 'schemaVersion', got {reader.TokenType}.");

        if (version > QuizMix.CurrentSchemaVersion)
            throw new JsonException(
                $"Quiz mix has schema version {version}, newer than the highest " +
                $"version this library supports ({QuizMix.CurrentSchemaVersion}).");
        if (version != QuizMix.CurrentSchemaVersion)
            throw new JsonException(
                $"Quiz mix has unsupported schema version {version}; " +
                $"expected {QuizMix.CurrentSchemaVersion}.");

        return version;
    }

    private static int? ReadQuizLength(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out int length))
            throw new JsonException(
                $"Expected integer or null for 'quizLength', got {reader.TokenType}.");

        return length;
    }

    private static List<QuizMixEntry> ReadEntries(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException($"Expected array for 'entries', got {reader.TokenType}.");

        var entries = new List<QuizMixEntry>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            entries.Add(ReadEntry(ref reader));

        return entries;
    }

    private static QuizMixEntry ReadEntry(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException(
                $"Expected object for an entries element, got {reader.TokenType}.");

        QuizCategory? category = null;
        int? percent = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "category":
                    category = ReadCategory(ref reader);
                    break;
                case "percent":
                    if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out int p))
                        throw new JsonException(
                            $"Expected integer for 'percent', got {reader.TokenType}.");
                    percent = p;
                    break;
                default:
                    throw new JsonException($"Unknown entry property '{name}'.");
            }
        }

        if (category is null)
            throw new JsonException("Entry is missing required property 'category'.");
        if (percent is null)
            throw new JsonException("Entry is missing required property 'percent'.");

        try
        {
            return new QuizMixEntry(category, percent.Value);
        }
        catch (ArgumentException ex)
        {
            // Per-entry bound (percent 1–100) — single-sourced in the entry ctor.
            throw new JsonException(ex.Message, ex);
        }
    }

    private static QuizCategory ReadCategory(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException(
                $"Expected object for 'category', got {reader.TokenType}.");

        QuizCategoryKind? kind = null;
        double? value = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "kind":
                    kind = ReadKind(ref reader);
                    break;
                case "value":
                    if (reader.TokenType != JsonTokenType.Number)
                        throw new JsonException(
                            $"Expected number for 'value', got {reader.TokenType}.");
                    value = reader.GetDouble();
                    break;
                default:
                    throw new JsonException($"Unknown category property '{name}'.");
            }
        }

        if (kind is null)
            throw new JsonException("Category is missing required property 'kind'.");

        try
        {
            return QuizCategory.Create(kind.Value, value);
        }
        catch (ArgumentException ex)
        {
            // Kind/value pairing and bounds — single-sourced in the factories.
            throw new JsonException(ex.Message, ex);
        }
    }

    private static QuizCategoryKind ReadKind(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected string for 'kind', got {reader.TokenType}.");

        var s = reader.GetString();

        // Exact declaration-name match only — the inverse of what Write emits
        // (Kind.ToString()). Resolved by searching the declared members rather
        // than by Enum.TryParse, which would also accept numeric strings and
        // case variants (halheinrich/backgammon#164).
        //
        // The previous spelling guarded TryParse with `kind.ToString() != s`,
        // which claimed to be an exact-name match but was not: measured on
        // net10.0, it rejected the DEFINED ordinals ("1" parses to NeverSeen,
        // whose name is not "1") while admitting every UNDEFINED one — "0",
        // "-1" and "99" all parse, and ToString() on an undefined value returns
        // the number back, so the comparison succeeded. Those reached
        // QuizCategory.Create and died on its unknown-kind arm, so no bad mix
        // was ever built; the hole was masked by a downstream guard rather than
        // closed here. A name search has no such round-trip quirk to reason
        // about.
        if (s is not null)
        {
            foreach (QuizCategoryKind candidate in Enum.GetValues<QuizCategoryKind>())
            {
                // Ordinal, not IgnoreCase: this format is case-exact by
                // contract — "neverSeen" is a rejected token, pinned in
                // QuizMixSerializationTests.
                if (string.Equals(s, candidate.ToString(), StringComparison.Ordinal))
                {
                    return candidate;
                }
            }
        }

        throw new JsonException($"Unknown quiz-category kind '{s}'.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        QuizMix value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", QuizMix.CurrentSchemaVersion);
        writer.WriteBoolean("randomOrder", value.RandomOrder);
        if (value.QuizLength is int length)
            writer.WriteNumber("quizLength", length);
        else
            writer.WriteNull("quizLength");

        writer.WriteStartArray("entries");
        foreach (var entry in value.Entries)   // declared order — contractual
        {
            writer.WriteStartObject();
            writer.WriteStartObject("category");
            writer.WriteString("kind", entry.Category.Kind.ToString());
            if (entry.Category.Value is double parameter)
                writer.WriteNumber("value", parameter);
            writer.WriteEndObject();
            writer.WriteNumber("percent", entry.Percent);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }
}
