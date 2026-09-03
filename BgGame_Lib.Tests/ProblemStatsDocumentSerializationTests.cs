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

    /// <summary>
    /// The bare tally-plus-date record: the whole per-problem value under v3
    /// (current, reinstated by halheinrich/backgammon#187) and, verbatim,
    /// under v2 too — so the genuine retired-document rows reuse it directly
    /// and stay non-vacuous real documents of their era. Under the foldable
    /// v4 it sat inside a problem's answer-kind wrapper, which is what the
    /// fold path unwraps.
    /// </summary>
    private const string TallyRecord =
        """{"tally":{"submitted":1,"correct":1,"totalEquityLoss":0},"lastQuizzed":"2026-07-18T19:04:11+00:00"}""";

    /// <summary>The record <see cref="TallyRecord"/> spells, for read-back assertions.</summary>
    private static ProblemStats TallyRecordAs(ProblemKey key) =>
        new(key, new ScoreSegment(1, 1, 0.0), T1);

    /// <summary>A well-formed v4 checker-play problem value: the record under its kind wrapper.</summary>
    private const string V4PlayValue = $$"""{"checkerPlay":{{TallyRecord}}}""";

    /// <summary>A well-formed v4 cube-pair problem value.</summary>
    private const string V4CubeValue = $$"""{"cubePair":{{TallyRecord}}}""";

    private static ProblemStatsDocument PopulatedDocument() =>
        ProblemStatsDocument.FromStats(
        [
            new ProblemStats(PlayKey, new ScoreSegment(3, 2, 0.125), T1),
            new ProblemStats(CubeKey, new ScoreSegment(2, 1, 0.08), T2),
            new ProblemStats(MoneyJacobyKey, new ScoreSegment(1, 1, 0.0), T1),
            new ProblemStats(MoneyNoJacobyKey, new ScoreSegment(4, 1, 0.5), T2),
        ]);

    /// <summary>
    /// <see cref="PopulatedDocument"/> as the current writer emits it — the
    /// pinned v3 wire form, byte for byte: version first, the map ordered by
    /// canonical key (ordinal: the money keys before the match keys, the cube
    /// key before the play key it prefixes), each value the bare record.
    /// </summary>
    private const string PinnedWireForm =
        $$$$"""{"schemaVersion":3,"problems":{"{{{{MoneyJacobyKeyText}}}}":{"tally":{"submitted":1,"correct":1,"totalEquityLoss":0},"lastQuizzed":"2026-07-18T19:04:11+00:00"},"{{{{MoneyNoJacobyKeyText}}}}":{"tally":{"submitted":4,"correct":1,"totalEquityLoss":0.5},"lastQuizzed":"2026-07-19T08:30:00-07:00"},"{{{{CubeKeyText}}}}":{"tally":{"submitted":2,"correct":1,"totalEquityLoss":0.08},"lastQuizzed":"2026-07-19T08:30:00-07:00"},"{{{{PlayKeyText}}}}":{"tally":{"submitted":3,"correct":2,"totalEquityLoss":0.125},"lastQuizzed":"2026-07-18T19:04:11+00:00"}}}""";

    /// <summary>
    /// <see cref="PopulatedDocument"/> as the interim v4 writer emitted it —
    /// the same records, each wrapped in the kind token its key derives. A
    /// genuine document of that version, for the fold path.
    /// </summary>
    private const string PopulatedV4Document =
        $$$$$"""{"schemaVersion":4,"problems":{"{{{{{MoneyJacobyKeyText}}}}}":{"checkerPlay":{"tally":{"submitted":1,"correct":1,"totalEquityLoss":0},"lastQuizzed":"2026-07-18T19:04:11+00:00"}},"{{{{{MoneyNoJacobyKeyText}}}}}":{"checkerPlay":{"tally":{"submitted":4,"correct":1,"totalEquityLoss":0.5},"lastQuizzed":"2026-07-19T08:30:00-07:00"}},"{{{{{CubeKeyText}}}}}":{"cubePair":{"tally":{"submitted":2,"correct":1,"totalEquityLoss":0.08},"lastQuizzed":"2026-07-19T08:30:00-07:00"}},"{{{{{PlayKeyText}}}}}":{"checkerPlay":{"tally":{"submitted":3,"correct":2,"totalEquityLoss":0.125},"lastQuizzed":"2026-07-18T19:04:11+00:00"}}}}""";

    private static void AssertSameRecords(ProblemStatsDocument expected, ProblemStatsDocument actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var (key, record) in expected.Problems)
            Assert.Equal(record, Assert.Contains(key, actual.Problems));
    }

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
    public void Serialize_WritesThePinnedWireForm()
    {
        // The whole document against a literal: the v3 form v1.9.1 ships,
        // reinstated as current (SPEC-stats-identity.md §3, amended
        // 2026-09-02). A tester's live bgquiz-stats.json is this, byte for
        // byte modulo indentation.
        Assert.Equal(PinnedWireForm, JsonSerializer.Serialize(PopulatedDocument()));
    }

    [Fact]
    public void Serialize_KeysTheMapByCanonicalProblemKeyStrings()
    {
        var json = JsonSerializer.Serialize(PopulatedDocument());

        Assert.Contains($"\"{PlayKeyText}\":", json);
        Assert.Contains($"\"{CubeKeyText}\":", json);
    }

    [Fact]
    public void Serialize_WritesEachRecordBare_NoAnswerKindWrapper()
    {
        // The v4 kind wrapper was a second spelling of a fact the key's own
        // grammar carries (dice ride on play keys and only there);
        // SPEC-scoring.md §4's 2026-09-02 amendment retires it. Each value
        // opens straight on the record, whatever the key's polarity.
        var json = JsonSerializer.Serialize(PopulatedDocument());

        Assert.Contains($"\"{CubeKeyText}\":{{\"tally\":", json);
        Assert.Contains($"\"{PlayKeyText}\":{{\"tally\":", json);
        Assert.Contains($"\"{MoneyJacobyKeyText}\":{{\"tally\":", json);
        Assert.DoesNotContain("\"cubePair\"", json);
        Assert.DoesNotContain("\"checkerPlay\"", json);
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
    //  Genuine v3 documents read as current. These rows were the retired-v3
    //  fixtures of the interim v4 era, promoted: the same bytes a tester's
    //  live bgquiz-stats.json holds, now the current format again.
    // ------------------------------------------------------------------

    [Fact]
    public void Deserialize_GenuineEmptyV3Document_ReadsAsCurrent()
    {
        var doc = JsonSerializer.Deserialize<ProblemStatsDocument>(
            """{"schemaVersion":3,"problems":{}}""");

        Assert.NotNull(doc);
        Assert.Equal(0, doc.Count);
    }

    [Theory]
    [InlineData(PlayKeyText)]
    [InlineData(MoneyJacobyKeyText)]
    public void Deserialize_GenuineV3Record_ReadsAsCurrent(string keyText)
    {
        var doc = JsonSerializer.Deserialize<ProblemStatsDocument>(
            $$$"""{"schemaVersion":3,"problems":{"{{{keyText}}}":{{{TallyRecord}}}}}""");

        Assert.NotNull(doc);
        var key = ProblemKey.Parse(keyText);
        Assert.Equal(TallyRecordAs(key), Assert.Contains(key, doc.Problems));
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
        { 2, $$$"""{"schemaVersion":2,"problems":{"{{{PlayKeyText}}}":{{{TallyRecord}}}}}""" },
        // ... including a v2 money key, whose spelling the v3+ grammar no
        // longer admits — the read side must retire the file, never
        // reinterpret it.
        { 2, $$$"""{"schemaVersion":2,"problems":{"{{{RetiredV2MoneyKeyText}}}":{{{TallyRecord}}}}}""" },
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
        // current" — so this survives the next bump untouched: whatever the
        // current version, its predecessor's problems-map body still reads
        // as retired.
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
    //  Foldable-version recognition — the retired signal's sibling, for the
    //  one version above current that is read and merged rather than set
    //  aside (SPEC-stats-identity.md §3, amended 2026-09-02;
    //  halheinrich/backgammon#187). Same three directions: genuine signals,
    //  misshapen reads as corrupt, and the signal is catchable generically.
    // ------------------------------------------------------------------

    /// <summary>
    /// Genuine v4 documents. Recognition is as shallow as the retired
    /// signal's: the problems map is skipped, so a v4 whose records are
    /// corrupt still signals foldable here — the fold path is where its
    /// content is validated and fails.
    /// </summary>
    public static TheoryData<string> GenuineFoldableDocuments => new()
    {
        """{"schemaVersion":4,"problems":{}}""",
        $$$"""{"schemaVersion":4,"problems":{"{{{PlayKeyText}}}":{{{V4PlayValue}}}}}""",
        $$$"""{"schemaVersion":4,"problems":{"{{{CubeKeyText}}}":{{{V4CubeValue}}}}}""",
        PopulatedV4Document,
        """{"schemaVersion":4,"problems":{"not even a key":42}}""",
    };

    [Theory]
    [MemberData(nameof(GenuineFoldableDocuments))]
    public void Deserialize_GenuineFoldableDocument_SignalsFoldable(string json)
    {
        var ex = Assert.Throws<FoldableStatsSchemaException>(
            () => JsonSerializer.Deserialize<ProblemStatsDocument>(json));

        Assert.Equal(ProblemStatsDocument.FoldableSchemaVersion, ex.SchemaVersion);
        Assert.Equal(4, ex.SchemaVersion);
        Assert.Contains("4", ex.Message, StringComparison.Ordinal);
        Assert.Contains("fold", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ReadFoldable", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FoldableSignal_IsCatchableAsJsonException_SoGenericFailLoudStillHolds()
    {
        var ex = Assert.ThrowsAny<JsonException>(
            () => JsonSerializer.Deserialize<ProblemStatsDocument>(
                """{"schemaVersion":4,"problems":{}}"""));

        Assert.IsType<FoldableStatsSchemaException>(ex);
    }

    [Fact]
    public void FoldableSignal_IsNotTheRetiredSignal()
    {
        // The two dispositions differ (set aside unread vs. read and merged),
        // so a consumer's retired catch must not swallow a v4.
        var ex = Assert.ThrowsAny<JsonException>(
            () => JsonSerializer.Deserialize<ProblemStatsDocument>(
                """{"schemaVersion":4,"problems":{}}"""));

        Assert.IsNotType<RetiredStatsSchemaException>(ex);
    }

    [Theory]
    [InlineData("""{"schemaVersion":4}""")]                            // missing problems
    [InlineData("""{"schemaVersion":4,"problems":[]}""")]              // problems not an object
    [InlineData("""{"schemaVersion":4,"problems":{},"extra":0}""")]    // property beyond problems
    [InlineData("""{"schemaVersion":4,"problems":{},"problems":{}}""")]  // duplicate problems
    [InlineData("""{"problems":{},"schemaVersion":4}""")]              // version not first
    [InlineData("""{"schemaVersion":4,"decisions":[]}""")]             // a v1 body under a v4 version
    public void Deserialize_ClaimsTheFoldableVersionButIsNotShapedLikeIt_ThrowsPlainJsonException(
        string json)
    {
        DeserializeThrows(json);
    }

    // ------------------------------------------------------------------
    //  Fail-loud reads — versions and document shape.
    // ------------------------------------------------------------------

    [Fact]
    public void Deserialize_NewerSchemaVersion_ThrowsNamingBothVersions()
    {
        // 5 is above the foldable 4: newer, refused, plain JsonException —
        // Assert.Throws demands the exact type, so neither signal fires.
        var ex = DeserializeThrows("""{"schemaVersion":5,"problems":{}}""");

        Assert.Contains("newer", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("5", ex.Message, StringComparison.Ordinal);
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
    [InlineData("""{"problems":{},"schemaVersion":3}""")]                 // version not first
    [InlineData("""{"schemaVersion":3,"decisions":[]}""")]                // a v1 body under the current version
    public void Deserialize_MalformedDocumentShape_Throws(string json)
    {
        DeserializeThrows(json);
    }

    [Fact]
    public void Deserialize_DuplicateProblemKey_Throws()
    {
        var ex = DeserializeThrows($$$"""
            {"schemaVersion":3,"problems":{
              "{{{PlayKeyText}}}":{{{TallyRecord}}},
              "{{{PlayKeyText}}}":{{{TallyRecord}}}
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
        DeserializeThrows($$$"""{"schemaVersion":3,"problems":{"{{{key}}}":{{{TallyRecord}}}}}""");
    }

    [Fact]
    public void Deserialize_NonCanonicalKeySpelling_Throws()
    {
        // One spelling per value: low-first dice are valid facts spelled
        // wrongly, so the strict parse — and therefore the document read —
        // must reject them or one problem could split across two map entries.
        DeserializeThrows(
            $$$"""{"schemaVersion":3,"problems":{"{{{Board}}}/7a7/1c/13":{{{TallyRecord}}}}}""");
    }

    [Fact]
    public void Deserialize_V4KindWrapperUnderTheCurrentVersion_Throws()
    {
        // The current value is the bare record: a v4 kind wrapper under a v3
        // version is an unknown record property, not a tolerated alias.
        DeserializeThrows($$$"""{"schemaVersion":3,"problems":{"{{{PlayKeyText}}}":{{{V4PlayValue}}}}}""");
    }

    /// <summary>
    /// Per-problem record values that must be rejected, each labeled by the
    /// rule it violates. The reserved equity-estimate fields
    /// (SPEC-scoring.md §4; halheinrich/backgammon#62) are deliberately
    /// among the unknowns: reserved means not yet in the grammar, so until
    /// their arc lands they fail exactly as any unknown property does — and
    /// when it lands they become additive optionals under this same version,
    /// absent reading zero, and these two rows flip to acceptance pins.
    /// </summary>
    public static TheoryData<string, string> MalformedRecords => new()
    {
        { "unknown record property",
          """{"tally":{"submitted":1,"correct":1,"totalEquityLoss":0},"lastQuizzed":"2026-07-18T19:04:11+00:00","extra":0}""" },
        { "reserved equity-estimate count not yet in the grammar",
          """{"tally":{"submitted":1,"correct":1,"totalEquityLoss":0},"lastQuizzed":"2026-07-18T19:04:11+00:00","equityEstimates":0}""" },
        { "reserved equity-estimate error not yet in the grammar",
          """{"tally":{"submitted":1,"correct":1,"totalEquityLoss":0},"lastQuizzed":"2026-07-18T19:04:11+00:00","totalEquityEstimateError":0}""" },
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
        { "record not an object", "42" },
    };

    [Theory]
    [MemberData(nameof(MalformedRecords))]
#pragma warning disable xUnit1026 // the label parameter exists to name the case in test output
    public void Deserialize_MalformedProblemRecord_Throws(string label, string record)
#pragma warning restore xUnit1026
    {
        DeserializeThrows($$$"""{"schemaVersion":3,"problems":{"{{{PlayKeyText}}}":{{{record}}}}}""");
    }

    // ------------------------------------------------------------------
    //  The fold path — ProblemStatsDocument.ReadFoldable reads a well-formed
    //  v4 into the current shape and nothing else.
    // ------------------------------------------------------------------

    [Fact]
    public void ReadFoldable_EmptyV4_ReadsEmpty()
    {
        var doc = ProblemStatsDocument.ReadFoldable("""{"schemaVersion":4,"problems":{}}""");

        Assert.Equal(0, doc.Count);
    }

    [Fact]
    public void ReadFoldable_PopulatedV4_UnwrapsEveryRecordIntoTheCurrentShape()
    {
        // The v4 writer's own emission of PopulatedDocument reads back as
        // PopulatedDocument: every kind wrapper unwrapped, both key
        // polarities, the non-UTC offset kept.
        var doc = ProblemStatsDocument.ReadFoldable(PopulatedV4Document);

        AssertSameRecords(PopulatedDocument(), doc);
    }

    [Theory]
    [InlineData(PlayKeyText, V4PlayValue)]
    [InlineData(MoneyJacobyKeyText, V4PlayValue)]
    [InlineData(CubeKeyText, V4CubeValue)]
    public void ReadFoldable_SingleRecord_UnwrapsItsKindWrapper(string keyText, string value)
    {
        var doc = ProblemStatsDocument.ReadFoldable(
            $$$"""{"schemaVersion":4,"problems":{"{{{keyText}}}":{{{value}}}}}""");

        var key = ProblemKey.Parse(keyText);
        Assert.Equal(TallyRecordAs(key), Assert.Contains(key, doc.Problems));
    }

    [Fact]
    public void ReadFoldable_ThenMerge_ThenSerialize_WritesTheCurrentForm()
    {
        // The whole fold in this library's terms: a set-aside v3 base, a v4
        // read foldable, merged per key, written as current. The play key is
        // on both sides (tallies summed, the later date kept); the cube key
        // on the v4 side only passes through.
        var v3Base = JsonSerializer.Deserialize<ProblemStatsDocument>(
            $$$"""{"schemaVersion":3,"problems":{"{{{PlayKeyText}}}":{{{TallyRecord}}}}}""")!;
        var v4 = ProblemStatsDocument.ReadFoldable(
            $$$$$"""{"schemaVersion":4,"problems":{"{{{{{CubeKeyText}}}}}":{"cubePair":{"tally":{"submitted":2,"correct":1,"totalEquityLoss":0.08},"lastQuizzed":"2026-07-19T08:30:00-07:00"}},"{{{{{PlayKeyText}}}}}":{"checkerPlay":{"tally":{"submitted":3,"correct":2,"totalEquityLoss":0.125},"lastQuizzed":"2026-07-19T08:30:00-07:00"}}}}""");

        var json = JsonSerializer.Serialize(v3Base.Merge(v4));

        Assert.Equal(
            $$$$"""{"schemaVersion":3,"problems":{"{{{{CubeKeyText}}}}":{"tally":{"submitted":2,"correct":1,"totalEquityLoss":0.08},"lastQuizzed":"2026-07-19T08:30:00-07:00"},"{{{{PlayKeyText}}}}":{"tally":{"submitted":4,"correct":3,"totalEquityLoss":0.125},"lastQuizzed":"2026-07-19T08:30:00-07:00"}}}""",
            json);
    }

    [Fact]
    public void ReadFoldable_NullJson_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ProblemStatsDocument.ReadFoldable(null!));
    }

    private static JsonException ReadFoldableThrows(string json) =>
        Assert.Throws<JsonException>(() => ProblemStatsDocument.ReadFoldable(json));

    [Theory]
    [InlineData("""{"schemaVersion":3,"problems":{}}""")]   // the current version: the ordinary path's
    [InlineData("""{"schemaVersion":2,"problems":{}}""")]   // retired — never a signal from here
    [InlineData("""{"schemaVersion":1,"decisions":[]}""")]
    [InlineData("""{"schemaVersion":5,"problems":{}}""")]   // newer
    [InlineData("""{"schemaVersion":0,"problems":{}}""")]   // unsupported
    [InlineData("""{"schemaVersion":"4","problems":{}}""")] // version not a number
    [InlineData("""{"problems":{},"schemaVersion":4}""")]   // version not first
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("")]
    [InlineData("  ")]
    public void ReadFoldable_NotAVersion4Document_ThrowsPlainJsonException(string json)
    {
        // Assert.Throws demands the exact type: the fold reads, it never
        // signals — a retired or foldable exception here would be a bug.
        ReadFoldableThrows(json);
    }

    [Theory]
    [InlineData("""{"schemaVersion":4}""")]                              // missing problems
    [InlineData("""{"schemaVersion":4,"problems":[]}""")]                // problems not an object
    [InlineData("""{"schemaVersion":4,"problems":{},"extra":0}""")]      // unknown root property
    [InlineData("""{"schemaVersion":4,"problems":{},"problems":{}}""")]  // duplicate problems
    [InlineData("""{"schemaVersion":4,"problems":{}} x""")]              // trailing content
    [InlineData("""{"schemaVersion":4,"problems":{}}{}""")]              // a second document
    public void ReadFoldable_MalformedDocumentShape_Throws(string json)
    {
        ReadFoldableThrows(json);
    }

    [Fact]
    public void ReadFoldable_DuplicateProblemKey_Throws()
    {
        var ex = ReadFoldableThrows($$$"""
            {"schemaVersion":4,"problems":{
              "{{{PlayKeyText}}}":{{{V4PlayValue}}},
              "{{{PlayKeyText}}}":{{{V4PlayValue}}}
            }}
            """);

        Assert.Contains("Duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad key")]
    [InlineData(RetiredV2MoneyKeyText)]
    public void ReadFoldable_InvalidProblemKey_Throws(string key)
    {
        ReadFoldableThrows($$$"""{"schemaVersion":4,"problems":{"{{{key}}}":{{{V4PlayValue}}}}}""");
    }

    /// <summary>
    /// Problem values that violate v4's answer-kind layer, each labeled by
    /// the rule it violates — the interim reader's own strictness, kept on
    /// the fold path: it reads exactly what v4 wrote. The reserved
    /// equity-guess token is among the unknowns because v4 never spelled it.
    /// </summary>
    public static TheoryData<string, string> MalformedKindLayers => new()
    {
        { "problem value not an object", "42" },
        { "no answer-kind record", "{}" },
        { "unknown answer kind", $$"""{"bogus":{{TallyRecord}}}""" },
        { "reserved equity-guess kind v4 never spelled", $$"""{"equityGuess":{{TallyRecord}}}""" },
        { "kind token is case-sensitive", $$"""{"CheckerPlay":{{TallyRecord}}}""" },
        { "kind disagreeing with the key's grammar", $$"""{"cubePair":{{TallyRecord}}}""" },
        { "a second answer-kind record", $$"""{"checkerPlay":{{TallyRecord}},"checkerPlay":{{TallyRecord}}}""" },
        { "v3's bare record shape (no kind wrapper)", TallyRecord },
        { "kind record not an object", """{"checkerPlay":42}""" },
    };

    [Theory]
    [MemberData(nameof(MalformedKindLayers))]
#pragma warning disable xUnit1026 // the label parameter exists to name the case in test output
    public void ReadFoldable_MalformedAnswerKindLayer_Throws(string label, string value)
#pragma warning restore xUnit1026
    {
        ReadFoldableThrows($$$"""{"schemaVersion":4,"problems":{"{{{PlayKeyText}}}":{{{value}}}}}""");
    }

    [Fact]
    public void ReadFoldable_CubeKeyUnderCheckerPlayKind_Throws()
    {
        // The mirror of the play-key mismatch row above: kind-vs-key
        // agreement is checked per polarity, not just one way round.
        ReadFoldableThrows($$$"""{"schemaVersion":4,"problems":{"{{{CubeKeyText}}}":{{{V4PlayValue}}}}}""");
    }

    [Theory]
    [MemberData(nameof(MalformedRecords))]
#pragma warning disable xUnit1026 // the label parameter exists to name the case in test output
    public void ReadFoldable_MalformedProblemRecord_Throws(string label, string record)
#pragma warning restore xUnit1026
    {
        // The record inside a v4 kind wrapper is held to the same rules as a
        // current record — the fold validates, it does not launder.
        var value = $$"""{"checkerPlay":{{record}}}""";

        ReadFoldableThrows($$$"""{"schemaVersion":4,"problems":{"{{{PlayKeyText}}}":{{{value}}}}}""");
    }
}
