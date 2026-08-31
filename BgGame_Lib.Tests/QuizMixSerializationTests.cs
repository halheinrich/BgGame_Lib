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
    ], quizLength: 25, randomOrder: false);

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
        Assert.Equal(mix.RandomOrder, restored.RandomOrder);
    }

    [Fact]
    public void RoundTrip_PreservesNullQuizLength()
    {
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)]);

        var restored = QuizMix.FromJson(mix.ToJson());

        Assert.Null(restored.QuizLength);
        Assert.True(restored.RandomOrder);
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
            """{"schemaVersion":1,"randomOrder":true,"quizLength":25,"entries":[""" +
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

        Assert.Contains("\"schemaVersion\":1", json);
        Assert.Contains("\"kind\":\"SeenFewerThan\"", json);
    }

    // -----------------------------------------------------------------------
    //  Fail-loud reads
    // -----------------------------------------------------------------------

    private static string Valid() =>
        // A minimal valid document; tests splice mutations in via replacement.
        """{"schemaVersion":1,"randomOrder":true,"quizLength":null,"entries":[{"category":{"kind":"NeverSeen"},"percent":100}]}""";

    [Fact]
    public void Read_RejectsNewerSchemaVersion_WithDistinguishedMessage()
    {
        var json = Valid().Replace("\"schemaVersion\":1", "\"schemaVersion\":2");

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
