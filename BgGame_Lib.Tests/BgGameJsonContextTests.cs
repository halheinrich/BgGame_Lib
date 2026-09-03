namespace BgGame_Lib.Tests;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using BgDataTypes_Lib;

/// <summary>
/// The source-generation gate (halheinrich/backgammon#129 leg 3): the context
/// changes the mechanism, never the bytes. <c>QuizMixSerializationTests</c>
/// and <c>ProblemStatsDocumentSerializationTests</c> are the outer byte gate —
/// they pin the emitted documents against literal wire strings written before
/// any of this — and pass unchanged. This suite pins the mechanism itself:
/// the same document must come out whichever resolver produces the metadata,
/// both bundled converters must be honoured on the source-generated path, and
/// the context must cover this library's whole wire surface.
/// </summary>
public class BgGameJsonContextTests
{
    // -----------------------------------------------------------------------
    //  The metadata mechanisms. Each options object differs from the others in
    //  exactly one respect — where the JsonTypeInfo comes from — because this
    //  library's wire format is hand-written by its converters and takes
    //  nothing else from options.
    // -----------------------------------------------------------------------

    /// <summary>The pre-change mechanism: runtime reflection.</summary>
    private static readonly JsonSerializerOptions ReflectionOptions =
        new() { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

    /// <summary>What this library ships: its own context, unchained.</summary>
    private static readonly JsonSerializerOptions ContextOnlyOptions =
        new() { TypeInfoResolver = BgGameJsonContext.Default };

    /// <summary>
    /// The consumer shape the arc's composition pattern prescribes, and the
    /// one BgQuiz's leg will build: this library's context chained with
    /// BgDataTypes_Lib's, most-derived-first.
    /// </summary>
    private static readonly JsonSerializerOptions ChainedOptions =
        new()
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(
                BgGameJsonContext.Default,
                BgDataTypesJsonContext.Default),
        };

    // -----------------------------------------------------------------------
    //  Fixtures — fully populated: every category kind on the mix side, every
    //  key polarity the v3 grammar admits on the stats side.
    // -----------------------------------------------------------------------

    private const string Board = "0,-2,0,0,0,0,5,0,3,0,0,0,-5,5,0,0,0,-3,0,-5,0,0,0,0,2,0";

    private static readonly DateTimeOffset T1 = new(2026, 7, 18, 19, 4, 11, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 7, 19, 8, 30, 0, TimeSpan.FromHours(-7));

    /// <summary>Every <see cref="QuizCategoryKind"/>, both parameter shapes, a
    /// length, and the non-default random toggle.</summary>
    private static QuizMix AllKindsMix() => new(
    [
        new QuizMixEntry(QuizCategory.NeverSeen, 10),
        new QuizMixEntry(QuizCategory.GotWrong, 10),
        new QuizMixEntry(QuizCategory.SeenFewerThan(3), 10),
        new QuizMixEntry(QuizCategory.NotSeenInDays(30), 10),
        new QuizMixEntry(QuizCategory.AvgEquityLossOver(0.05), 20),
        new QuizMixEntry(QuizCategory.WrongRateOver(0.25), 20),
        new QuizMixEntry(QuizCategory.EverythingElse, 20),
    ], quizLength: 25, randomOrder: false);

    /// <summary>The capless mix: entries, no length, default toggle — so the
    /// <c>quizLength</c> null branch of the writer is covered too.</summary>
    private static QuizMix CaplessMix() =>
        new([new QuizMixEntry(QuizCategory.NeverSeen, 100)]);

    /// <summary>Every key polarity: match play, match cube (the dice-less
    /// prefix), and both money spellings of the Jacoby rule.</summary>
    private static ProblemStatsDocument PopulatedDocument() =>
        ProblemStatsDocument.FromStats(
        [
            new ProblemStats(ProblemKey.Parse($"{Board}/7a7/1c/31"), new ScoreSegment(3, 2, 0.125), T1),
            new ProblemStats(ProblemKey.Parse($"{Board}/7a7/1c"), new ScoreSegment(2, 1, 0.08), T2),
            new ProblemStats(ProblemKey.Parse($"{Board}/0a0j/1c/31"), new ScoreSegment(1, 1, 0.0), T1),
            new ProblemStats(ProblemKey.Parse($"{Board}/0a0nj/1c/31"), new ScoreSegment(4, 1, 0.5), T2),
        ]);

    public static TheoryData<string> Mixes => new()
    {
        nameof(AllKindsMix), nameof(CaplessMix), nameof(QuizMix.Empty),
    };

    private static QuizMix Mix(string name) => name switch
    {
        nameof(AllKindsMix) => AllKindsMix(),
        nameof(CaplessMix) => CaplessMix(),
        _ => QuizMix.Empty,
    };

    public static TheoryData<string> Documents => new()
    {
        nameof(PopulatedDocument), nameof(ProblemStatsDocument.Empty),
    };

    private static ProblemStatsDocument Document(string name) =>
        name == nameof(PopulatedDocument) ? PopulatedDocument() : ProblemStatsDocument.Empty;

    // -----------------------------------------------------------------------
    //  Byte identity — the invariant of the whole halheinrich/backgammon#129
    //  arc: source generation changes the mechanism, never the bytes.
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Mixes))]
    public void EveryResolver_EmitsTheSameMix(string mix)
    {
        var value = Mix(mix);

        var reflection = JsonSerializer.Serialize(value, TypeInfo<QuizMix>(ReflectionOptions));

        // The shipped entry point, the context alone, and the consumer chain.
        Assert.Equal(reflection, value.ToJson());
        Assert.Equal(reflection, JsonSerializer.Serialize(value, TypeInfo<QuizMix>(ContextOnlyOptions)));
        Assert.Equal(reflection, JsonSerializer.Serialize(value, TypeInfo<QuizMix>(ChainedOptions)));
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public void EveryResolver_EmitsTheSameStatsDocument(string document)
    {
        var value = Document(document);

        var reflection = JsonSerializer.Serialize(value, TypeInfo<ProblemStatsDocument>(ReflectionOptions));

        Assert.Equal(reflection, JsonSerializer.Serialize(value, TypeInfo<ProblemStatsDocument>(ContextOnlyOptions)));
        Assert.Equal(reflection, JsonSerializer.Serialize(value, TypeInfo<ProblemStatsDocument>(ChainedOptions)));
    }

    /// <summary>
    /// The read half: a document deserialized through the source-generated
    /// metadata re-emits identically, so the mechanism change is invisible in
    /// both directions.
    /// </summary>
    [Theory]
    [MemberData(nameof(Mixes))]
    public void SourceGeneratedMixRoundTrip_IsStable(string mix)
    {
        var json = JsonSerializer.Serialize(Mix(mix), TypeInfo<QuizMix>(ContextOnlyOptions));

        var restored = JsonSerializer.Deserialize(json, TypeInfo<QuizMix>(ContextOnlyOptions))!;

        Assert.Equal(Mix(mix), restored);   // QuizMix has full value equality
        Assert.Equal(json, JsonSerializer.Serialize(restored, TypeInfo<QuizMix>(ContextOnlyOptions)));
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public void SourceGeneratedStatsRoundTrip_IsStable(string document)
    {
        var json = JsonSerializer.Serialize(
            Document(document), TypeInfo<ProblemStatsDocument>(ContextOnlyOptions));

        var restored = JsonSerializer.Deserialize(
            json, TypeInfo<ProblemStatsDocument>(ContextOnlyOptions))!;

        Assert.Equal(json, JsonSerializer.Serialize(restored, TypeInfo<ProblemStatsDocument>(ContextOnlyOptions)));
    }

    /// <summary>
    /// The tolerant restore path routes through the context too — the one
    /// entry point of the trio whose failure mode is a silent fallback rather
    /// than an exception, so a broken resolver would show up as a mix that
    /// quietly went blank.
    /// </summary>
    [Fact]
    public void TryFromJson_RestoresThroughTheContext()
    {
        Assert.True(QuizMix.TryFromJson(AllKindsMix().ToJson(), out var restored));
        Assert.Equal(AllKindsMix(), restored);

        Assert.False(QuizMix.TryFromJson("""{"schemaVersion":99}""", out var fallback));
        Assert.Same(QuizMix.Empty, fallback);
    }

    // -----------------------------------------------------------------------
    //  Converter respect on the source-generated path. A bundled type-level
    //  converter outranks whatever a resolver supplies, and that must stay
    //  true when the resolver is a source-generated context.
    // -----------------------------------------------------------------------

    [Fact]
    public void ContextPath_MixKeepsItsPinnedWireShape()
    {
        var json = JsonSerializer.Serialize(
            new QuizMix(
            [
                new QuizMixEntry(QuizCategory.SeenFewerThan(3), 40),
                new QuizMixEntry(QuizCategory.EverythingElse, 60),
            ], quizLength: 25, randomOrder: false),
            TypeInfo<QuizMix>(ContextOnlyOptions));

        Assert.Equal(
            """{"schemaVersion":1,"randomOrder":false,"quizLength":25,"entries":[{"category":{"kind":"SeenFewerThan","value":3},"percent":40},{"category":{"kind":"EverythingElse"},"percent":60}]}""",
            json);
    }

    /// <summary>
    /// The stats document's map shape: the canonical <see cref="ProblemKey"/>
    /// string is the JSON property name — written by the converter itself, not
    /// resolved through the options — and writes are ordered by that key.
    /// </summary>
    [Fact]
    public void ContextPath_StatsKeepsItsCanonicalKeyMap()
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(
            PopulatedDocument(), TypeInfo<ProblemStatsDocument>(ContextOnlyOptions)));

        Assert.Equal(3, doc.RootElement.GetProperty("schemaVersion").GetInt32());
        var keys = doc.RootElement.GetProperty("problems")
            .EnumerateObject().Select(p => p.Name).ToList();

        Assert.Equal(keys.Order(StringComparer.Ordinal), keys);
        Assert.All(keys, k => Assert.True(ProblemKey.TryParse(k, provider: null, out _)));
        Assert.Contains($"{Board}/0a0j/1c/31", keys);
        Assert.Contains($"{Board}/0a0nj/1c/31", keys);
    }

    /// <summary>
    /// halheinrich/backgammon#164's name-search reader survives the mechanism
    /// change: the <c>kind</c> token is matched against the declared member
    /// names, ordinally and exactly, so numeric ordinals (defined and
    /// undefined alike) and case variants stay rejected on the
    /// source-generated path.
    /// </summary>
    [Theory]
    [InlineData("neverSeen")]      // case variant
    [InlineData("NEVERSEEN")]
    [InlineData("1")]              // a defined ordinal
    [InlineData("0")]              // undefined ordinals
    [InlineData("-1")]
    [InlineData("99")]
    [InlineData("NotAKind")]
    public void ContextPath_RejectsNonCanonicalCategoryKinds(string kind)
    {
        var json =
            $$"""{"schemaVersion":1,"randomOrder":true,"quizLength":null,"entries":[{"category":{"kind":"{{kind}}"},"percent":100}]}""";

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize(json, TypeInfo<QuizMix>(ContextOnlyOptions)));
    }

    /// <summary>
    /// Every kind's declaration name round-trips on the source-generated path
    /// — the positive half of the name-search contract, derived from the enum
    /// so a new kind is covered the moment it is declared.
    /// </summary>
    [Fact]
    public void ContextPath_AcceptsEveryDeclaredCategoryKindName()
    {
        foreach (var kind in Enum.GetValues<QuizCategoryKind>())
        {
            var parameter = ParameterFor(kind);
            var value = parameter is double v
                ? $""","value":{JsonSerializer.Serialize(v)}"""
                : "";
            var json =
                $$"""{"schemaVersion":1,"randomOrder":true,"quizLength":null,"entries":[{"category":{"kind":"{{kind}}"{{value}}},"percent":100}]}""";

            var mix = JsonSerializer.Deserialize(json, TypeInfo<QuizMix>(ContextOnlyOptions))!;

            Assert.Equal(kind, mix.Entries[0].Category.Kind);
        }
    }

    /// <summary>
    /// A parameter each parameterized kind accepts; <see langword="null"/> for
    /// the parameterless ones — the same pairing
    /// <c>QuizCategory.Create</c> enforces, so an unpaired kind/value would
    /// fail the read exactly as it does on the wire.
    /// </summary>
    private static double? ParameterFor(QuizCategoryKind kind) => kind switch
    {
        QuizCategoryKind.SeenFewerThan => 3,
        QuizCategoryKind.NotSeenInDays => 30,
        QuizCategoryKind.AvgEquityLossOver => 0.05,
        QuizCategoryKind.WrongRateOver => 0.25,
        _ => null,
    };

    /// <summary>
    /// The retired-schema signal is a read-side behaviour of the converter, so
    /// it must reach a consumer identically through the source-generated
    /// metadata — a stats file that retires honestly on the reflection path
    /// must not degrade to a generic load error on the shipped one.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ContextPath_StillSignalsRetiredSchemaVersions(int version)
    {
        var json = version == 1
            ? """{"schemaVersion":1,"decisions":[]}"""
            : $$$"""{"schemaVersion":{{{version}}},"problems":{}}""";

        var thrown = Assert.Throws<RetiredStatsSchemaException>(
            () => JsonSerializer.Deserialize(json, TypeInfo<ProblemStatsDocument>(ContextOnlyOptions)));

        Assert.Equal(version, thrown.SchemaVersion);
    }

    /// <summary>
    /// The retired signal's sibling for the foldable version 4
    /// (halheinrich/backgammon#187) — the same read-side behaviour, so the
    /// same mechanism gate: a v4 that folds on the reflection path must fold
    /// on the shipped one.
    /// </summary>
    [Fact]
    public void ContextPath_StillSignalsTheFoldableSchemaVersion()
    {
        var thrown = Assert.Throws<FoldableStatsSchemaException>(
            () => JsonSerializer.Deserialize(
                """{"schemaVersion":4,"problems":{}}""",
                TypeInfo<ProblemStatsDocument>(ContextOnlyOptions)));

        Assert.Equal(ProblemStatsDocument.FoldableSchemaVersion, thrown.SchemaVersion);
    }

    // -----------------------------------------------------------------------
    //  Completeness — the halheinrich/backgammon#144 intersection pattern: two
    //  independent enumerations of one fact, kept agreeing by a test.
    //
    //  Side A is this library's wire surface, derived from the assembly: every
    //  type carrying a type-level [JsonConverter] is a wire unit by
    //  definition, because that attribute is how this repo bundles a wire
    //  format onto a type. Side B is what the context declares, derived from
    //  the generated JsonTypeInfo<T> properties rather than read off the
    //  [JsonSerializable] list. A third converter-bearing type lands in side A
    //  the moment it is written and fails here until it is declared; a
    //  declaration left behind by a deleted wire type fails the other way.
    // -----------------------------------------------------------------------

    [Fact]
    public void TheContextDeclares_ExactlyThisLibrarysWireSurface()
    {
        Assert.Equal(WireSurface(), DeclaredRoots());
    }

    /// <summary>
    /// The vacuity guard on both derivations: an enumeration that silently
    /// returned nothing would satisfy the equality above. Named explicitly so
    /// the two known wire units are pinned as such, and so this test — not the
    /// one above — is what fails if a derivation stops working.
    /// </summary>
    [Fact]
    public void BothDerivations_FindTheKnownWireUnits()
    {
        Assert.Contains(typeof(QuizMix), WireSurface());
        Assert.Contains(typeof(ProblemStatsDocument), WireSurface());
        Assert.Contains(typeof(QuizMix), DeclaredRoots());
        Assert.Contains(typeof(ProblemStatsDocument), DeclaredRoots());
    }

    /// <summary>
    /// And the whole serialized closure of that surface resolves through this
    /// context alone: this library's documents owe nothing to a chain. Both
    /// converters write and read their trees by hand, so the walk terminates
    /// at the roots today — the check earns its keep the moment a wire unit
    /// grows an ordinary serialized property, or a converter starts resolving
    /// a nested type through the active options the way BgDataTypes_Lib's
    /// <c>PlayJsonConverter</c> does for <c>Move</c>. The byte-identity
    /// theories above are the same guard from the other side: a converter that
    /// began asking the options for a type this context does not declare would
    /// throw there while the reflection path kept working.
    /// </summary>
    [Fact]
    public void ThisReposContextAlone_ResolvesTheFullClosure()
    {
        var unresolved = SerializedClosure()
            .Where(type => BgGameJsonContext.Default.GetTypeInfo(type) is null)
            .Select(type => type.ToString())
            .Order()
            .ToList();

        Assert.Empty(unresolved);
    }

    /// <summary>
    /// This context chains rather than shadows: the BgDataTypes_Lib types that
    /// appear in this library's public surface are that repo's to declare, and
    /// <see cref="ProblemKey"/> reaches the wire here as a raw property name
    /// the converter writes itself, never as a resolved type.
    /// </summary>
    [Fact]
    public void TheContext_DoesNotShadowBgDataTypes()
    {
        Assert.Null(BgGameJsonContext.Default.GetTypeInfo(typeof(ProblemKey)));
        Assert.Null(BgGameJsonContext.Default.GetTypeInfo(typeof(Play)));
    }

    /// <summary>
    /// Side A: every type in this assembly carrying a type-level
    /// <c>[JsonConverter]</c>. Non-public types are included deliberately —
    /// one would be a wire unit a public context cannot declare (CS0053), and
    /// failing here is the honest way to surface that.
    /// </summary>
    private static IReadOnlyList<Type> WireSurface() =>
        [.. typeof(QuizMix).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<JsonConverterAttribute>(inherit: false) is not null)
            .OrderBy(t => t.FullName, StringComparer.Ordinal)];

    /// <summary>
    /// Side B: the roots the generator actually produced metadata for, read
    /// off the context's own <see cref="JsonTypeInfo{T}"/> properties rather
    /// than off its <c>[JsonSerializable]</c> attributes — so a declaration
    /// the generator silently declined (SYSLIB1030) counts as absent here,
    /// which is what it is at runtime.
    /// </summary>
    private static IReadOnlyList<Type> DeclaredRoots() =>
        [.. typeof(BgGameJsonContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.PropertyType)
            .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(JsonTypeInfo<>))
            .Select(t => t.GetGenericArguments()[0])
            .OrderBy(t => t.FullName, StringComparer.Ordinal)];

    /// <summary>
    /// The closure of <see cref="WireSurface"/> under the serializer's own
    /// metadata graph. Asks the serializer what each type serializes as rather
    /// than re-deriving it by reflection; kind <see cref="JsonTypeInfoKind.None"/>
    /// means a converter owns the wire form wholesale and the serializer never
    /// walks it, so neither does this.
    /// </summary>
    private static HashSet<Type> SerializedClosure()
    {
        var closure = new HashSet<Type>();
        var pending = new Queue<Type>(WireSurface());
        while (pending.Count > 0)
        {
            var type = pending.Dequeue();
            if (!closure.Add(type))
                continue;

            if (!ContextOnlyOptions.TryGetTypeInfo(type, out var info))
                continue;

            switch (info.Kind)
            {
                case JsonTypeInfoKind.Object:
                    foreach (var property in info.Properties)
                        pending.Enqueue(property.PropertyType);
                    break;
                case JsonTypeInfoKind.Enumerable:
                case JsonTypeInfoKind.Dictionary:
                    if (info.ElementType is not null)
                        pending.Enqueue(info.ElementType);
                    break;
            }
        }

        return closure;
    }

    // -----------------------------------------------------------------------
    //  Posture — the declarations that make the arc's rules gates rather than
    //  suggestions. Asserted here so flipping any of them off in the csproj or
    //  the context fails a test rather than silently reopening the reflection
    //  path or the fast-path capture.
    // -----------------------------------------------------------------------

    [Fact]
    public void TheLibraryAssembly_DeclaresItselfTrimmable()
    {
        Assert.Contains(
            typeof(QuizMix).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>(),
            a => a.Key == "IsTrimmable" && a.Value == "True");
    }

    /// <summary>
    /// Metadata-only generation, the arc's rule (2): a default-mode fast-path
    /// handler binds nested type resolution to the declaring context's own
    /// private options and bypasses the resolver chain. BgDataTypes_Lib's
    /// chained-consumer test pair demonstrates that failure and owns the rule;
    /// this is the declaration pin that keeps this link of the chain honest.
    /// </summary>
    [Fact]
    public void TheContext_GeneratesMetadataOnly()
    {
        var options = typeof(BgGameJsonContext)
            .GetCustomAttribute<JsonSourceGenerationOptionsAttribute>();

        Assert.NotNull(options);
        Assert.Equal(JsonSourceGenerationMode.Metadata, options.GenerationMode);
    }

    private static JsonTypeInfo<T> TypeInfo<T>(JsonSerializerOptions options)
        => (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
}
