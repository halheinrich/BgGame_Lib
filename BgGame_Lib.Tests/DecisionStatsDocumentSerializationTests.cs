namespace BgGame_Lib.Tests;

using System.Globalization;
using System.Text.Json;
using BgDataTypes_Lib;

public class DecisionStatsDocumentSerializationTests
{
    private static readonly DecisionId XgpId =
        DecisionId.Parse("problem.xgp", CultureInfo.InvariantCulture);
    private static readonly DecisionId XgId =
        DecisionId.Parse("match.xg:g3:m12:play", CultureInfo.InvariantCulture);

    private static readonly DateTimeOffset T1 = new(2026, 7, 18, 19, 4, 11, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 7, 19, 8, 30, 0, TimeSpan.FromHours(-7));

    private static DecisionStatsDocument PopulatedDocument() =>
        DecisionStatsDocument.FromStats(
        [
            new DecisionStats(XgpId, new ScoreSegment(3, 2, 0.125), T1),
            new DecisionStats(XgId, new ScoreSegment(1, 0, 0.08), T2),
        ]);

    // ------------------------------------------------------------------
    //  Round-trips — plain JsonSerializer, no options: the converter is
    //  bundled on the type, so consumers register nothing.
    // ------------------------------------------------------------------

    [Fact]
    public void RoundTrip_EmptyDocument()
    {
        var json = JsonSerializer.Serialize(DecisionStatsDocument.Empty);
        var back = JsonSerializer.Deserialize<DecisionStatsDocument>(json);

        Assert.NotNull(back);
        Assert.Equal(0, back.Count);
        Assert.Equal("""{"schemaVersion":1,"decisions":[]}""", json);
    }

    [Fact]
    public void RoundTrip_PopulatedDocument_PreservesEveryRecord()
    {
        var doc = PopulatedDocument();

        var json = JsonSerializer.Serialize(doc);
        var back = JsonSerializer.Deserialize<DecisionStatsDocument>(json);

        Assert.NotNull(back);
        Assert.Equal(doc.Count, back.Count);
        Assert.Equal(doc.Decisions[XgpId], back.Decisions[XgpId]);   // record equality: tally + date
        Assert.Equal(doc.Decisions[XgId], back.Decisions[XgId]);     // incl. the non-UTC offset on T2
    }

    [Fact]
    public void Serialize_WritesDecisionIdCanonicalStrings()
    {
        var json = JsonSerializer.Serialize(PopulatedDocument());

        Assert.Contains("\"problem.xgp\"", json);
        Assert.Contains("\"match.xg:g3:m12:play\"", json);
    }

    [Fact]
    public void Serialize_OrdersDecisionsByCanonicalId_RegardlessOfInputOrder()
    {
        var a = new DecisionStats(XgpId, new ScoreSegment(1, 1, 0.0), T1);
        var b = new DecisionStats(XgId, new ScoreSegment(1, 0, 0.05), T1);

        var oneWay = JsonSerializer.Serialize(DecisionStatsDocument.FromStats([a, b]));
        var otherWay = JsonSerializer.Serialize(DecisionStatsDocument.FromStats([b, a]));

        Assert.Equal(oneWay, otherWay);
        // "match.xg..." < "problem.xgp" ordinally, so the .xg record comes first.
        Assert.True(oneWay.IndexOf("match.xg", StringComparison.Ordinal)
            < oneWay.IndexOf("problem.xgp", StringComparison.Ordinal));
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
        Assert.Equal("null", JsonSerializer.Serialize<DecisionStatsDocument?>(null));
        Assert.Null(JsonSerializer.Deserialize<DecisionStatsDocument?>("null"));
    }

    // ------------------------------------------------------------------
    //  Fail-loud reads.
    // ------------------------------------------------------------------

    private static JsonException DeserializeThrows(string json) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DecisionStatsDocument>(json));

    [Fact]
    public void Deserialize_NewerSchemaVersion_ThrowsNamingBothVersions()
    {
        var ex = DeserializeThrows("""{"schemaVersion":2,"decisions":[]}""");

        Assert.Contains("newer", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2", ex.Message, StringComparison.Ordinal);
        Assert.Contains("1", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_OlderUnknownSchemaVersion_Throws()
    {
        var ex = DeserializeThrows("""{"schemaVersion":0,"decisions":[]}""");

        Assert.Contains("unsupported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("""{"decisions":[]}""")]                       // missing schemaVersion
    [InlineData("""{"schemaVersion":1}""")]                    // missing decisions
    [InlineData("""{"schemaVersion":"1","decisions":[]}""")]   // version not a number
    [InlineData("""{"schemaVersion":1,"decisions":{}}""")]     // decisions not an array
    [InlineData("""{"schemaVersion":1,"decisions":[],"extra":0}""")]   // unknown root property
    [InlineData("42")]                                         // root not an object
    public void Deserialize_MalformedDocumentShape_Throws(string json)
    {
        DeserializeThrows(json);
    }

    [Fact]
    public void Deserialize_DuplicateDecisionId_Throws()
    {
        var ex = DeserializeThrows("""
            {"schemaVersion":1,"decisions":[
              {"id":"problem.xgp","tally":{"submitted":1,"correct":1,"totalEquityLoss":0},"lastQuizzed":"2026-07-18T19:04:11+00:00"},
              {"id":"problem.xgp","tally":{"submitted":2,"correct":0,"totalEquityLoss":0.1},"lastQuizzed":"2026-07-19T19:04:11+00:00"}
            ]}
            """);

        Assert.Contains("Duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("bad:file:name")]      // not a valid canonical form
    [InlineData("")]                   // empty
    public void Deserialize_InvalidDecisionId_Throws(string id)
    {
        DeserializeThrows($$"""
            {"schemaVersion":1,"decisions":[
              {"id":"{{id}}","tally":{"submitted":1,"correct":1,"totalEquityLoss":0},"lastQuizzed":"2026-07-18T19:04:11+00:00"}
            ]}
            """);
    }

    /// <summary>
    /// Full decisions-array elements that must be rejected, each labeled by
    /// the rule it violates.
    /// </summary>
    public static TheoryData<string, string> MalformedElements => new()
    {
        { "unknown decision property",
          """{"id":"problem.xgp","tally":{"submitted":1,"correct":1,"totalEquityLoss":0},"lastQuizzed":"2026-07-18T19:04:11+00:00","extra":0}""" },
        { "unknown tally property",
          """{"id":"problem.xgp","tally":{"submitted":1,"correct":1,"totalEquityLoss":0,"extra":0},"lastQuizzed":"2026-07-18T19:04:11+00:00"}""" },
        { "correct greater than submitted",
          """{"id":"problem.xgp","tally":{"submitted":1,"correct":2,"totalEquityLoss":0},"lastQuizzed":"2026-07-18T19:04:11+00:00"}""" },
        { "negative submitted count",
          """{"id":"problem.xgp","tally":{"submitted":-1,"correct":0,"totalEquityLoss":0},"lastQuizzed":"2026-07-18T19:04:11+00:00"}""" },
        { "negative equity loss",
          """{"id":"problem.xgp","tally":{"submitted":1,"correct":0,"totalEquityLoss":-0.5},"lastQuizzed":"2026-07-18T19:04:11+00:00"}""" },
        { "malformed lastQuizzed",
          """{"id":"problem.xgp","tally":{"submitted":1,"correct":1,"totalEquityLoss":0},"lastQuizzed":"not-a-date"}""" },
        { "missing id",
          """{"tally":{"submitted":1,"correct":1,"totalEquityLoss":0},"lastQuizzed":"2026-07-18T19:04:11+00:00"}""" },
        { "missing tally",
          """{"id":"problem.xgp","lastQuizzed":"2026-07-18T19:04:11+00:00"}""" },
        { "missing lastQuizzed",
          """{"id":"problem.xgp","tally":{"submitted":1,"correct":1,"totalEquityLoss":0}}""" },
        { "tally missing submitted",
          """{"id":"problem.xgp","tally":{"correct":1,"totalEquityLoss":0},"lastQuizzed":"2026-07-18T19:04:11+00:00"}""" },
    };

    [Theory]
    [MemberData(nameof(MalformedElements))]
#pragma warning disable xUnit1026 // the label parameter exists to name the case in test output
    public void Deserialize_MalformedDecisionElement_Throws(string label, string element)
#pragma warning restore xUnit1026
    {
        DeserializeThrows($$"""{"schemaVersion":1,"decisions":[{{element}}]}""");
    }
}
