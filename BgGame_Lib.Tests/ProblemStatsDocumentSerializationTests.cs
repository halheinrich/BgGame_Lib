namespace BgGame_Lib.Tests;

using System.Text.Json;
using BgDataTypes_Lib;

public class ProblemStatsDocumentSerializationTests
{
    // Canonical key fixtures over the standard starting board. The cube key
    // is the play key minus the dice field, i.e. its strict ordinal prefix —
    // which pins the write order below (cube record first).
    private const string Board = "0,-2,0,0,0,0,5,0,3,0,0,0,-5,5,0,0,0,-3,0,-5,0,0,0,0,2,0";
    private const string PlayKeyText = $"{Board}/7a7/1c/31";
    private const string CubeKeyText = $"{Board}/7a7/1c";

    // v3's money keys. The Jacoby rule is a money-only suffix, so a money
    // problem has exactly two spellings — one per polarity — and the v2 money
    // spelling (silent about Jacoby) is not in the v3 grammar at all. Match
    // keys above are byte-identical to what the v2 writer emitted.
    private const string MoneyJacobyKeyText = $"{Board}/0a0j/1c/31";
    private const string MoneyNoJacobyKeyText = $"{Board}/0a0nj/1c/31";
    private const string RetiredV2MoneyKeyText = $"{Board}/0a0/1c/31";

    private static readonly ProblemKey PlayKey = ProblemKey.Parse(PlayKeyText);
    private static readonly ProblemKey CubeKey = ProblemKey.Parse(CubeKeyText);
    private static readonly ProblemKey MoneyJacobyKey = ProblemKey.Parse(MoneyJacobyKeyText);
    private static readonly ProblemKey MoneyNoJacobyKey = ProblemKey.Parse(MoneyNoJacobyKeyText);

    private static readonly DateTimeOffset T1 = new(2026, 7, 18, 19, 4, 11, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 7, 19, 8, 30, 0, TimeSpan.FromHours(-7));

    /// <summary>A well-formed problem-record value, for malformed-document scaffolding.</summary>
    private const string ValidRecord =
        """{"tally":{"submitted":1,"correct":1,"totalEquityLoss":0},"lastQuizzed":"2026-07-18T19:04:11+00:00"}""";

    private static ProblemStatsDocument PopulatedDocument() =>
        ProblemStatsDocument.FromStats(
        [
            new ProblemStats(PlayKey, new ScoreSegment(3, 2, 0.125), T1),
            new ProblemStats(CubeKey, new ScoreSegment(2, 1, 0.08), T2),
            new ProblemStats(MoneyJacobyKey, new ScoreSegment(1, 1, 0.0), T1),
            new ProblemStats(MoneyNoJacobyKey, new ScoreSegment(4, 1, 0.5), T2),
        ]);

    // ------------------------------------------------------------------
    //  Round-trips — plain JsonSerializer, no options: the converter is
    //  bundled on the type, so consumers register nothing.
    // ------------------------------------------------------------------

    [Fact]
    public void RoundTrip_EmptyDocument()
    {
        var json = JsonSerializer.Serialize(ProblemStatsDocument.Empty);
        var back = JsonSerializer.Deserialize<ProblemStatsDocument>(json);

        Assert.NotNull(back);
        Assert.Equal(0, back.Count);
        Assert.Equal("""{"schemaVersion":3,"problems":{}}""", json);
    }

    [Fact]
    public void RoundTrip_PopulatedDocument_PreservesEveryRecord()
    {
        var doc = PopulatedDocument();

        var json = JsonSerializer.Serialize(doc);
        var back = JsonSerializer.Deserialize<ProblemStatsDocument>(json);

        Assert.NotNull(back);
        Assert.Equal(doc.Count, back.Count);
        Assert.Equal(doc.Problems[PlayKey], back.Problems[PlayKey]);   // record equality: key + tally + date
        Assert.Equal(doc.Problems[CubeKey], back.Problems[CubeKey]);   // incl. the non-UTC offset on T2
        Assert.Equal(doc.Problems[MoneyJacobyKey], back.Problems[MoneyJacobyKey]);
        Assert.Equal(doc.Problems[MoneyNoJacobyKey], back.Problems[MoneyNoJacobyKey]);
    }

    [Fact]
    public void Serialize_KeysTheMapByCanonicalProblemKeyStrings()
    {
        var json = JsonSerializer.Serialize(PopulatedDocument());

        Assert.Contains($"\"{PlayKeyText}\":", json);
        Assert.Contains($"\"{CubeKeyText}\":", json);
    }

    [Fact]
    public void Serialize_MoneyKeysCarryTheirJacobySuffix_InBothPolarities()
    {
        var json = JsonSerializer.Serialize(PopulatedDocument());

        Assert.Contains($"\"{MoneyJacobyKeyText}\":", json);
        Assert.Contains($"\"{MoneyNoJacobyKeyText}\":", json);
        // The v2 money spelling is gone from the written format entirely —
        // that persisted-format change is what the v3 bump exists for.
        Assert.DoesNotContain($"\"{RetiredV2MoneyKeyText}\":", json);
    }

    [Fact]
    public void Serialize_MatchKeys_AreSpelledExactlyAsV2SpelledThem()
    {
        // The Jacoby suffix is money-only by ruling (SPEC-stats-identity.md
        // §2), so nothing about a match key moved between v2 and v3. Pinning
        // it here keeps a future "make it a field on every key" refactor from
        // silently respelling the whole match population.
        var json = JsonSerializer.Serialize(PopulatedDocument());

        Assert.Contains($"\"{Board}/7a7/1c/31\":", json);
        Assert.Contains($"\"{Board}/7a7/1c\":", json);
    }

    [Fact]
    public void Serialize_OrdersProblemsByCanonicalKey_RegardlessOfInputOrder()
    {
        var play = new ProblemStats(PlayKey, new ScoreSegment(1, 1, 0.0), T1);
        var cube = new ProblemStats(CubeKey, new ScoreSegment(2, 1, 0.05), T1);

        var oneWay = JsonSerializer.Serialize(ProblemStatsDocument.FromStats([play, cube]));
        var otherWay = JsonSerializer.Serialize(ProblemStatsDocument.FromStats([cube, play]));

        Assert.Equal(oneWay, otherWay);
        // The cube key is the play key's strict prefix, so it sorts first
        // ordinally; its record must be written first.
        Assert.True(oneWay.IndexOf($"\"{CubeKeyText}\":", StringComparison.Ordinal)
            < oneWay.IndexOf($"\"{PlayKeyText}\":", StringComparison.Ordinal));
    }

    [Fact]
    public void Serialize_PropertyNamesArePinnedAgainstConsumerNamingPolicy()
    {
        var doc = PopulatedDocument();
        var withPolicy = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseUpper };

        // The hand-written converter fixes names and ordering; a consumer's
        // naming policy must not change the file format. (Whitespace via
        // WriteIndented is the one knob options still control.)
        Assert.Equal(JsonSerializer.Serialize(doc), JsonSerializer.Serialize(doc, withPolicy));
    }

    [Fact]
    public void RoundTrip_NullDocument()
    {
        Assert.Equal("null", JsonSerializer.Serialize<ProblemStatsDocument?>(null));
        Assert.Null(JsonSerializer.Deserialize<ProblemStatsDocument?>("null"));
    }

    // ------------------------------------------------------------------
    //  Retired-version recognition — the deliberate signal, which covers
    //  EVERY recognised version below the current one, each carrying its own
    //  version number (the consumer names the file it sets aside from it).
    //  Three directions: a genuine retired document signals retirement; a
    //  document that merely claims a retired version reads as corrupt;
    //  foreign JSON reads as corrupt. The corrupt/foreign assertions use
    //  Assert.Throws<JsonException>, which demands the exact type — a
    //  RetiredStatsSchemaException there would fail the test, so the
    //  discrimination is pinned in both directions.
    // ------------------------------------------------------------------

    /// <summary>
    /// Genuine documents in each retired version, labeled by the version they
    /// must report. Retiring v2 must not cost v1 its own signal — a single
    /// retired-version constant would drop every remaining v1 holder into the
    /// generic fail-loud path, which is exactly what the deliberate-
    /// recognition ruling forbids (SPEC-stats-identity.md §3).
    /// </summary>
    public static TheoryData<int, string> GenuineRetiredDocuments => new()
    {
        { 1, """{"schemaVersion":1,"decisions":[]}""" },
        { 1, """{"schemaVersion":1,"decisions":[{"id":"problem.xgp","tally":{"submitted":1,"correct":1,"totalEquityLoss":0},"lastQuizzed":"2026-07-18T19:04:11+00:00"}]}""" },
        // Recognition is shallow: the body's contents are skipped, never
        // parsed, so even garbage inside it still reads as genuine.
        { 1, """{"schemaVersion":1,"decisions":[{"garbage":true},42,"x"]}""" },
        { 2, """{"schemaVersion":2,"problems":{}}""" },
        { 2, $$$"""{"schemaVersion":2,"problems":{"{{{PlayKeyText}}}":{{{ValidRecord}}}}}""" },
        // ... including a v2 money key, whose spelling the v3 grammar no
        // longer admits — the read side must retire the file, never
        // reinterpret it.
        { 2, $$$"""{"schemaVersion":2,"problems":{"{{{RetiredV2MoneyKeyText}}}":{{{ValidRecord}}}}}""" },
        { 2, """{"schemaVersion":2,"problems":{"not even a key":42}}""" },
    };

    [Theory]
    [MemberData(nameof(GenuineRetiredDocuments))]
    public void Deserialize_GenuineRetiredDocument_SignalsItsOwnSchemaVersion(
        int version, string json)
    {
        var ex = Assert.Throws<RetiredStatsSchemaException>(
            () => JsonSerializer.Deserialize<ProblemStatsDocument>(json));

        Assert.Equal(version, ex.SchemaVersion);
        Assert.Contains(
            version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ex.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheVersionJustSuperseded_IsRetired_CarryingItsOwnNumber()
    {
        // The retirement rule is a range — "every recognised version below
        // current" — so this survives the next bump untouched: whatever v3
        // becomes v-previous, its problems-map body still reads as retired.
        int superseded = ProblemStatsDocument.CurrentSchemaVersion - 1;

        var ex = Assert.Throws<RetiredStatsSchemaException>(
            () => JsonSerializer.Deserialize<ProblemStatsDocument>(
                $$$"""{"schemaVersion":{{{superseded}}},"problems":{}}"""));

        Assert.Equal(superseded, ex.SchemaVersion);
    }

    [Theory]
    [InlineData("""{"schemaVersion":1,"decisions":[]}""")]
    [InlineData("""{"schemaVersion":2,"problems":{}}""")]
    public void RetiredSignal_IsCatchableAsJsonException_SoGenericFailLoudStillHolds(string json)
    {
        // A consumer that only knows the general fail-loud contract must
        // still fail loud on a retired file, not load it quietly.
        var ex = Assert.ThrowsAny<JsonException>(
            () => JsonSerializer.Deserialize<ProblemStatsDocument>(json));

        Assert.IsType<RetiredStatsSchemaException>(ex);
    }

    private static JsonException DeserializeThrows(string json) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProblemStatsDocument>(json));

    [Theory]
    [InlineData("""{"schemaVersion":1}""")]                            // missing decisions
    [InlineData("""{"schemaVersion":1,"decisions":{}}""")]             // decisions not an array
    [InlineData("""{"schemaVersion":1,"decisions":[],"extra":0}""")]   // property beyond decisions
    [InlineData("""{"decisions":[],"schemaVersion":1}""")]             // version not first
    [InlineData("""{"schemaVersion":1,"problems":{}}""")]              // a v2+ body under a v1 version
    [InlineData("""{"schemaVersion":2}""")]                            // missing problems
    [InlineData("""{"schemaVersion":2,"problems":[]}""")]              // problems not an object
    [InlineData("""{"schemaVersion":2,"problems":{},"extra":0}""")]    // property beyond problems
    [InlineData("""{"schemaVersion":2,"problems":{},"problems":{}}""")]  // duplicate problems
    [InlineData("""{"problems":{},"schemaVersion":2}""")]              // version not first
    [InlineData("""{"schemaVersion":2,"decisions":[]}""")]             // a v1 body under a v2 version
    public void Deserialize_ClaimsARetiredVersionButIsNotShapedLikeIt_ThrowsPlainJsonException(
        string json)
    {
        DeserializeThrows(json);
    }

    [Theory]
    [InlineData("""{"foo":1}""")]          // first property is not schemaVersion
    [InlineData("{}")]                     // no properties at all
    [InlineData("[]")]                     // root not an object
    [InlineData("42")]                     // root not an object
    [InlineData("\"stats\"")]              // root not an object
    public void Deserialize_ForeignJson_ThrowsPlainJsonException_NotRetired(string json)
    {
        DeserializeThrows(json);
    }

    // ------------------------------------------------------------------
    //  Fail-loud reads — versions and document shape.
    // ------------------------------------------------------------------

    [Fact]
    public void Deserialize_NewerSchemaVersion_ThrowsNamingBothVersions()
    {
        var ex = DeserializeThrows("""{"schemaVersion":4,"problems":{}}""");

        Assert.Contains("newer", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("4", ex.Message, StringComparison.Ordinal);
        Assert.Contains("3", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"schemaVersion":0,"problems":{}}""")]
    [InlineData("""{"schemaVersion":-1,"problems":{}}""")]
    public void Deserialize_UnknownOlderSchemaVersion_Throws(string json)
    {
        var ex = DeserializeThrows(json);

        Assert.Contains("unsupported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("""{"problems":{}}""")]                                   // missing schemaVersion
    [InlineData("""{"schemaVersion":3}""")]                               // missing problems
    [InlineData("""{"schemaVersion":"3","problems":{}}""")]               // version not a number
    [InlineData("""{"schemaVersion":3,"problems":[]}""")]                 // problems not an object
    [InlineData("""{"schemaVersion":3,"problems":{},"extra":0}""")]       // unknown root property
    [InlineData("""{"schemaVersion":3,"problems":{},"problems":{}}""")]   // duplicate problems
    [InlineData("""{"schemaVersion":3,"schemaVersion":3,"problems":{}}""")]   // duplicate version
    public void Deserialize_MalformedDocumentShape_Throws(string json)
    {
        DeserializeThrows(json);
    }

    [Fact]
    public void Deserialize_DuplicateProblemKey_Throws()
    {
        var ex = DeserializeThrows($$$"""
            {"schemaVersion":3,"problems":{
              "{{{PlayKeyText}}}":{{{ValidRecord}}},
              "{{{PlayKeyText}}}":{{{ValidRecord}}}
            }}
            """);

        Assert.Contains("Duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]                    // empty
    [InlineData("bad key")]             // not the canonical grammar
    [InlineData("problem.xgp")]         // a v1 DecisionId is not a key
    [InlineData("XGID=-b----E-C---eE---c-e----B-:0:0:1:31:0:0:0:7:10")]   // raw XGID is an identity nowhere
    [InlineData(RetiredV2MoneyKeyText)] // the v2 money spelling is not in the v3 grammar
    public void Deserialize_InvalidProblemKey_Throws(string key)
    {
        DeserializeThrows($$$"""{"schemaVersion":3,"problems":{"{{{key}}}":{{{ValidRecord}}}}}""");
    }

    [Fact]
    public void Deserialize_NonCanonicalKeySpelling_Throws()
    {
        // One spelling per value: low-first dice are valid facts spelled
        // wrongly, so the strict parse — and therefore the document read —
        // must reject them or one problem could split across two map entries.
        DeserializeThrows(
            $$$"""{"schemaVersion":3,"problems":{"{{{Board}}}/7a7/1c/13":{{{ValidRecord}}}}}""");
    }

    /// <summary>
    /// Problem-record values that must be rejected, each labeled by the rule
    /// it violates.
    /// </summary>
    public static TheoryData<string, string> MalformedRecords => new()
    {
        { "record not an object", "42" },
        { "unknown record property",
          """{"tally":{"submitted":1,"correct":1,"totalEquityLoss":0},"lastQuizzed":"2026-07-18T19:04:11+00:00","extra":0}""" },
        { "unknown tally property",
          """{"tally":{"submitted":1,"correct":1,"totalEquityLoss":0,"extra":0},"lastQuizzed":"2026-07-18T19:04:11+00:00"}""" },
        { "correct greater than submitted",
          """{"tally":{"submitted":1,"correct":2,"totalEquityLoss":0},"lastQuizzed":"2026-07-18T19:04:11+00:00"}""" },
        { "negative submitted count",
          """{"tally":{"submitted":-1,"correct":0,"totalEquityLoss":0},"lastQuizzed":"2026-07-18T19:04:11+00:00"}""" },
        { "negative equity loss",
          """{"tally":{"submitted":1,"correct":0,"totalEquityLoss":-0.5},"lastQuizzed":"2026-07-18T19:04:11+00:00"}""" },
        { "malformed lastQuizzed",
          """{"tally":{"submitted":1,"correct":1,"totalEquityLoss":0},"lastQuizzed":"not-a-date"}""" },
        { "missing tally",
          """{"lastQuizzed":"2026-07-18T19:04:11+00:00"}""" },
        { "missing lastQuizzed",
          """{"tally":{"submitted":1,"correct":1,"totalEquityLoss":0}}""" },
        { "tally missing submitted",
          """{"tally":{"correct":1,"totalEquityLoss":0},"lastQuizzed":"2026-07-18T19:04:11+00:00"}""" },
    };

    [Theory]
    [MemberData(nameof(MalformedRecords))]
#pragma warning disable xUnit1026 // the label parameter exists to name the case in test output
    public void Deserialize_MalformedProblemRecord_Throws(string label, string record)
#pragma warning restore xUnit1026
    {
        DeserializeThrows($$$"""{"schemaVersion":3,"problems":{"{{{PlayKeyText}}}":{{{record}}}}}""");
    }
}
