namespace BgGame_Lib.Tests;

using System.Text.Json;

public class QuizMixSerializationTests
{
    private static QuizMixEntry Entry(QuizCategory category, int percent) =>
        new(category, percent);

    private static QuizMix AllKindsMix() => new(
    [
        Entry(QuizCategory.NeverSeen, 10),
        Entry(QuizCategory.GotWrong, 10),
        Entry(QuizCategory.SeenFewerThan(3), 10),
        Entry(QuizCategory.NotSeenInDays(30), 10),
        Entry(QuizCategory.AvgEquityLossOver(0.05), 20),
        Entry(QuizCategory.WrongRateOver(0.25), 20),
        Entry(QuizCategory.EverythingElse, 20),
    ], quizLength: 25);

    // -----------------------------------------------------------------------
    //  Round trips
    // -----------------------------------------------------------------------

    [Fact]
    public void RoundTrip_PreservesEverything()
    {
        var mix = AllKindsMix();

        var restored = QuizMix.FromJson(mix.ToJson());

        Assert.Equal(mix.Entries, restored.Entries);   // declared order preserved
        Assert.Equal(mix.QuizLength, restored.QuizLength);
    }

    [Fact]
    public void RoundTrip_PreservesNullQuizLength()
    {
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)]);

        var restored = QuizMix.FromJson(mix.ToJson());

        Assert.Null(restored.QuizLength);
    }

    [Fact]
    public void RoundTrip_PreservesBlankMix()
    {
        var restored = QuizMix.FromJson(QuizMix.Empty.ToJson());

        Assert.True(restored.IsPassthrough);
        Assert.Null(restored.QuizLength);
    }

    [Fact]
    public void Write_PinsTheWireShape()
    {
        var mix = new QuizMix(
        [
            Entry(QuizCategory.NeverSeen, 50),
            Entry(QuizCategory.SeenFewerThan(3), 20),
            Entry(QuizCategory.EverythingElse, 30),
        ], quizLength: 25);

        Assert.Equal(
            """{"schemaVersion":2,"quizLength":25,"entries":[""" +
            """{"category":{"kind":"NeverSeen"},"percent":50},""" +
            """{"category":{"kind":"SeenFewerThan","value":3},"percent":20},""" +
            """{"category":{"kind":"EverythingElse"},"percent":30}]}""",
            mix.ToJson());
    }

    [Fact]
    public void Write_IsImmuneToConsumerNamingPolicy()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseUpper };

        var json = JsonSerializer.Serialize(AllKindsMix(), options);

        Assert.Contains("\"schemaVersion\":2", json);
        Assert.Contains("\"kind\":\"SeenFewerThan\"", json);
    }

    // -----------------------------------------------------------------------
    //  Fail-loud reads
    // -----------------------------------------------------------------------

    private static string Valid() =>
        // A minimal valid document; tests splice mutations in via replacement.
        // Version 1, the retired toggle's version, which still reads under its
        // own contract — so the fail-loud pins below hold it to that contract.
        // The current version's own pins follow them.
        """{"schemaVersion":1,"randomOrder":true,"quizLength":null,"entries":[{"category":{"kind":"NeverSeen"},"percent":100}]}""";

    [Fact]
    public void Read_RejectsNewerSchemaVersion_WithDistinguishedMessage()
    {
        var json = Valid().Replace("\"schemaVersion\":1", "\"schemaVersion\":3");

        var ex = Assert.Throws<JsonException>(() => QuizMix.FromJson(json));

        Assert.Contains("newer", ex.Message);
    }

    [Fact]
    public void Read_RejectsOlderSchemaVersion()
    {
        var json = Valid().Replace("\"schemaVersion\":1", "\"schemaVersion\":0");

        var ex = Assert.Throws<JsonException>(() => QuizMix.FromJson(json));

        Assert.Contains("unsupported", ex.Message);
    }

    [Theory]
    [InlineData("\"schemaVersion\":1,")]
    [InlineData("\"randomOrder\":true,")]
    [InlineData("\"quizLength\":null,")]
    public void Read_RejectsMissingRequiredProperty(string property)
    {
        var json = Valid().Replace(property, "");

        Assert.Throws<JsonException>(() => QuizMix.FromJson(json));
    }

    [Fact]
    public void Read_RejectsMissingEntries()
    {
        var json = """{"schemaVersion":1,"randomOrder":true,"quizLength":null}""";

        Assert.Throws<JsonException>(() => QuizMix.FromJson(json));
    }

    [Fact]
    public void Read_RejectsUnknownTopLevelProperty()
    {
        var json = Valid().Replace("\"randomOrder\":true", "\"randomOrder\":true,\"extra\":1");

        Assert.Throws<JsonException>(() => QuizMix.FromJson(json));
    }

    [Fact]
    public void Read_RejectsUnknownEntryProperty()
    {
        var json = Valid().Replace("\"percent\":100", "\"percent\":100,\"extra\":1");

        Assert.Throws<JsonException>(() => QuizMix.FromJson(json));
    }

    [Fact]
    public void Read_RejectsUnknownCategoryProperty()
    {
        var json = Valid().Replace("\"kind\":\"NeverSeen\"", "\"kind\":\"NeverSeen\",\"extra\":1");

        Assert.Throws<JsonException>(() => QuizMix.FromJson(json));
    }

    // The 'kind' token is read by exact declaration-name search, the inverse of
    // what Write emits (halheinrich/backgammon#164). Every non-name spelling is
    // rejected here — including the undefined ordinals, which the previous
    // TryParse-plus-round-trip guard admitted (they were caught further down by
    // QuizCategory.Create's unknown-kind arm, so this pins the rejection at the
    // reader where the claim is made).
    [Theory]
    [InlineData("\"neverSeen\"")]   // case variant
    [InlineData("\"NEVERSEEN\"")]   // case variant
    [InlineData("\"1\"")]           // defined ordinal, as a string
    [InlineData("\"0\"")]           // undefined ordinal — below the declared range
    [InlineData("\"-1\"")]          // undefined ordinal — negative
    [InlineData("\"99\"")]          // undefined ordinal — above the declared range
    [InlineData("\"NeverSeen, GotWrong\"")]  // comma list: Enum.TryParse accepts these
    [InlineData("\" NeverSeen \"")]  // surrounding whitespace
    [InlineData("\"NoSuchKind\"")]
    [InlineData("\"\"")]            // empty token
    public void Read_RejectsInvalidKind(string kind)
    {
        var json = Valid().Replace("\"kind\":\"NeverSeen\"", $"\"kind\":{kind}");

        Assert.Throws<JsonException>(() => QuizMix.FromJson(json));
    }

    /// <summary>
    /// The other half of the exact-name contract: strictness costs no
    /// legitimate token. Every declared kind — not just the sampled ones — is
    /// accepted under the spelling Write emits, so the name search can never
    /// silently drop a member as it is added
    /// (halheinrich/backgammon#164).
    /// </summary>
    [Fact]
    public void Read_AcceptsEveryDeclaredKind_UnderTheSpellingWriteEmits()
    {
        foreach (QuizCategoryKind declared in Enum.GetValues<QuizCategoryKind>())
        {
            var json = Valid().Replace("\"kind\":\"NeverSeen\"", $"\"kind\":\"{declared}\"");

            // Parameterized kinds need their value; this test is about the kind
            // token alone, so only the parameterless ones round-trip whole.
            // The rest must at minimum get PAST the kind read — which a
            // missing-value failure proves, and an unknown-kind failure would
            // not.
            var ex = Record.Exception(() => QuizMix.FromJson(json));

            Assert.DoesNotContain(
                "Unknown quiz-category kind",
                ex?.Message ?? string.Empty);
        }
    }

    [Fact]
    public void Read_RejectsValueOnParameterlessKind()
    {
        var json = Valid().Replace(
            "{\"kind\":\"NeverSeen\"}", "{\"kind\":\"NeverSeen\",\"value\":1}");

        Assert.Throws<JsonException>(() => QuizMix.FromJson(json));
    }

    [Fact]
    public void Read_RejectsMissingValueOnParameterizedKind()
    {
        var json = Valid().Replace("{\"kind\":\"NeverSeen\"}", "{\"kind\":\"SeenFewerThan\"}");

        Assert.Throws<JsonException>(() => QuizMix.FromJson(json));
    }

    [Theory]
    [InlineData("3.5")]    // non-integral for an integer kind
    [InlineData("0")]      // below the factory bound
    public void Read_RejectsInvalidSeenFewerThanValue(string value)
    {
        var json = Valid().Replace(
            "{\"kind\":\"NeverSeen\"}", $"{{\"kind\":\"SeenFewerThan\",\"value\":{value}}}");

        Assert.Throws<JsonException>(() => QuizMix.FromJson(json));
    }

    [Fact]
    public void Read_RejectsOutOfRangeWrongRateValue()
    {
        var json = Valid().Replace(
            "{\"kind\":\"NeverSeen\"}", "{\"kind\":\"WrongRateOver\",\"value\":1.5}");

        Assert.Throws<JsonException>(() => QuizMix.FromJson(json));
    }

    [Fact]
    public void Read_RejectsOutOfRangePercent()
    {
        var json = Valid().Replace("\"percent\":100", "\"percent\":0");

        Assert.Throws<JsonException>(() => QuizMix.FromJson(json));
    }

    [Fact]
    public void Read_RejectsSumOtherThan100()
    {
        var json = Valid().Replace("\"percent\":100", "\"percent\":99");

        Assert.Throws<JsonException>(() => QuizMix.FromJson(json));
    }

    [Fact]
    public void Read_RejectsDuplicateCategories()
    {
        var json = Valid().Replace(
            "[{\"category\":{\"kind\":\"NeverSeen\"},\"percent\":100}]",
            "[{\"category\":{\"kind\":\"NeverSeen\"},\"percent\":50}," +
            "{\"category\":{\"kind\":\"NeverSeen\"},\"percent\":50}]");

        Assert.Throws<JsonException>(() => QuizMix.FromJson(json));
    }

    [Fact]
    public void Read_RejectsQuizLengthWithoutEntries()
    {
        var json = """{"schemaVersion":1,"randomOrder":true,"quizLength":25,"entries":[]}""";

        Assert.Throws<JsonException>(() => QuizMix.FromJson(json));
    }

    [Fact]
    public void FromJson_RejectsNullToken() =>
        Assert.Throws<ArgumentException>(() => QuizMix.FromJson("null"));

    [Fact]
    public void FromJson_RejectsNullString() =>
        Assert.Throws<ArgumentNullException>(() => QuizMix.FromJson(null!));

    // -----------------------------------------------------------------------
    //  Version 2 — the current version, without the retired toggle
    //  (SPEC-filtering.md §5, 2026-10-14) — and version 1 under its own
    //  contract
    // -----------------------------------------------------------------------

    private const string Entries100 = """[{"category":{"kind":"NeverSeen"},"percent":100}]""";

    private static QuizMix NeverSeen100 => new([Entry(QuizCategory.NeverSeen, 100)]);

    [Fact]
    public void CurrentSchemaVersion_IsTwo() =>
        Assert.Equal(2, QuizMix.CurrentSchemaVersion);

    [Fact]
    public void Version2_Reads()
    {
        var mix = QuizMix.FromJson($$"""{"schemaVersion":2,"quizLength":7,"entries":{{Entries100}}}""");

        Assert.Equal(new QuizMix([Entry(QuizCategory.NeverSeen, 100)], quizLength: 7), mix);
    }

    [Theory]
    [InlineData($$"""{"schemaVersion":2,"randomOrder":true,"quizLength":null,"entries":{{Entries100}}}""")]
    [InlineData($$"""{"schemaVersion":2,"randomOrder":false,"quizLength":null,"entries":{{Entries100}}}""")]
    [InlineData($$"""{"randomOrder":true,"schemaVersion":2,"quizLength":null,"entries":{{Entries100}}}""")]
    [InlineData($$"""{"schemaVersion":2,"randomOrder":42,"quizLength":null,"entries":{{Entries100}}}""")]
    public void Version2_RefusesTheRetiredToggle_AsAnUnknownProperty(string json)
    {
        var ex = Assert.Throws<JsonException>(() => QuizMix.FromJson(json));

        Assert.Contains("Unknown QuizMix property 'randomOrder'", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData($$"""{"quizLength":null,"entries":{{Entries100}}}""")]          // no schemaVersion
    [InlineData($$"""{"schemaVersion":2,"entries":{{Entries100}}}""")]          // no quizLength
    [InlineData("""{"schemaVersion":2,"quizLength":null}""")]                   // no entries
    public void Version2_RejectsAMissingRequiredProperty(string json) =>
        Assert.Throws<JsonException>(() => QuizMix.FromJson(json));

    [Theory]
    [InlineData(3)]
    [InlineData(99)]
    public void AVersionAboveTwo_IsRefusedAsNewer(int version)
    {
        var ex = Assert.Throws<JsonException>(() => QuizMix.FromJson(
            $$"""{"schemaVersion":{{version}},"quizLength":null,"entries":{{Entries100}}}"""));

        Assert.Contains("newer", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData($$"""{"schemaVersion":1,"randomOrder":true,"quizLength":null,"entries":{{Entries100}}}""")]
    [InlineData($$"""{"schemaVersion":1,"randomOrder":false,"quizLength":null,"entries":{{Entries100}}}""")]
    [InlineData($$"""{"randomOrder":false,"schemaVersion":1,"quizLength":null,"entries":{{Entries100}}}""")]
    public void Version1_Reads_ItsToggleDropped(string json) =>
        Assert.Equal(NeverSeen100, QuizMix.FromJson(json));

    [Fact]
    public void TwoVersion1Documents_DifferingOnlyInTheToggle_ReadAsEqualMixes()
    {
        var on = QuizMix.FromJson($$"""{"schemaVersion":1,"randomOrder":true,"quizLength":5,"entries":{{Entries100}}}""");
        var off = QuizMix.FromJson($$"""{"schemaVersion":1,"randomOrder":false,"quizLength":5,"entries":{{Entries100}}}""");

        Assert.Equal(on, off);
        Assert.Equal(on.GetHashCode(), off.GetHashCode());
    }

    [Fact]
    public void AVersion1Read_IsWrittenBackAsVersion2()
    {
        var mix = QuizMix.FromJson($$"""{"schemaVersion":1,"randomOrder":true,"quizLength":null,"entries":{{Entries100}}}""");

        Assert.Equal($$"""{"schemaVersion":2,"quizLength":null,"entries":{{Entries100}}}""", mix.ToJson());
    }

    [Fact]
    public void Version1_RequiresTheToggle()
    {
        var ex = Assert.Throws<JsonException>(() => QuizMix.FromJson(
            $$"""{"schemaVersion":1,"quizLength":null,"entries":{{Entries100}}}"""));

        Assert.Contains("Missing required property 'randomOrder'", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("\"true\"")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void Version1_RequiresTheToggleToBeBoolean(string value)
    {
        var ex = Assert.Throws<JsonException>(() => QuizMix.FromJson(
            $$"""{"schemaVersion":1,"randomOrder":{{value}},"quizLength":null,"entries":{{Entries100}}}"""));

        Assert.Contains("Expected boolean for 'randomOrder'", ex.Message, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    //  TryFromJson — the tolerant restore path
    // -----------------------------------------------------------------------

    [Fact]
    public void TryFromJson_RestoresAValidMix()
    {
        var result = QuizMix.TryFromJson(AllKindsMix().ToJson(), out var mix);

        Assert.True(result);
        Assert.Equal(AllKindsMix().Entries, mix.Entries);
    }

    [Theory]
    [InlineData(null)]           // storage key never written
    [InlineData("null")]         // literal null token
    [InlineData("not json")]     // malformed
    [InlineData("{\"schemaVersion\":2}")]   // contract violation
    public void TryFromJson_FallsBackToEmpty(string? json)
    {
        var result = QuizMix.TryFromJson(json, out var mix);

        Assert.False(result);
        Assert.Same(QuizMix.Empty, mix);
    }
}
