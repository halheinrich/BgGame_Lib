namespace BgGame_Lib;

using System.Text.Json.Serialization;

/// <summary>
/// The source-generated <see cref="JsonSerializerContext"/> for this
/// library's wire surface — trim-safe <c>System.Text.Json</c> metadata for
/// every type this library puts on a wire, produced at compile time instead
/// of by runtime reflection (halheinrich/backgammon#129 leg 3). The
/// mechanism changes, the bytes do not: serialization through this context
/// is byte-identical to the reflection path, pinned by test, and both
/// bundled <c>[JsonConverter]</c>s are honored unchanged.
///
/// <para>
/// <b>What is declared, and why.</b> This library has exactly two wire
/// units, and both are document roots: <see cref="ProblemStatsDocument"/>
/// (the persistent per-problem stats file behind BgQuiz's stats store) and
/// <see cref="QuizMix"/> (the persisted quiz-composition config). Neither
/// carries composite parts for the generator to walk, and that is a property
/// of their converters rather than an accident: both
/// <see cref="ProblemStatsDocumentJsonConverter"/> and
/// <see cref="QuizMixJsonConverter"/> write and read their whole trees by
/// hand against a <see cref="System.Text.Json.Utf8JsonWriter"/> /
/// <see cref="System.Text.Json.Utf8JsonReader"/>, so the serializer is never
/// asked to resolve a nested type — not <see cref="ProblemStats"/>, not
/// <see cref="ScoreSegment"/>, not <see cref="QuizMixEntry"/>, not
/// <see cref="QuizCategory"/>, and not
/// <see cref="BgDataTypes_Lib.ProblemKey"/>, whose canonical string is
/// written as a raw property name and re-parsed on read. The closure is the
/// two roots and nothing else, which is why this context chains no other:
/// see the "resolves the full closure" pair in
/// <c>BgGameJsonContextTests</c>, which derives that closure from the
/// serializer's own metadata graph and fails the moment a member starts
/// routing through the serializer instead.
/// </para>
///
/// <para>
/// <b>Public, deliberately</b> — the arc's standing shape
/// (<c>BgDataTypesJsonContext</c> is the precedent), and here it is
/// load-bearing rather than merely conventional: BgQuiz's
/// <c>QuizStatsStore</c> names <see cref="ProblemStatsDocument"/> to
/// <see cref="System.Text.Json.JsonSerializer"/> directly, so the consumer
/// is the party that needs this metadata. It composes by chaining type-info
/// resolvers — no consumer-side converter registration, no glue types:
/// <code>
/// var options = new JsonSerializerOptions
/// {
///     TypeInfoResolver = JsonTypeInfoResolver.Combine(
///         TheConsumersOwnContext.Default,
///         BgGameJsonContext.Default)
/// };
/// </code>
/// (equivalently, add each context to
/// <c>JsonSerializerOptions.TypeInfoResolverChain</c>). The chain is
/// searched in order, first resolver claiming a type wins — order contexts
/// most-derived-first. <see cref="QuizMix"/> is declared here even though no
/// consumer names it: its own <see cref="QuizMix.ToJson"/> /
/// <see cref="QuizMix.FromJson"/> / <see cref="QuizMix.TryFromJson"/> trio
/// is what needs the metadata, and those are this library's own trim-safe
/// entry points.
/// </para>
///
/// <para>
/// <b>Metadata-only generation, deliberately — part of the pattern.</b>
/// The default generation mode also emits fast-path serialize handlers, and
/// a fast-path handler binds every nested type resolution to the
/// <em>declaring context's own private options</em>, not the runtime options
/// it was invoked with — silently bypassing the resolver chain. With
/// <see cref="JsonSourceGenerationMode.Metadata"/> on every context in the
/// chain there is no context-private options capture: resolution always
/// flows through the combined options. The failure and the working shape are
/// demonstrated by the chained-consumer test pair in BgDataTypes_Lib, which
/// owns this rule; every downstream context declares the same mode.
/// </para>
///
/// <para>
/// <b>No options-level converter registrations exist to express.</b> Both of
/// this library's converters are bundled by type-level
/// <c>[JsonConverter]</c>, so a caller's plain
/// <see cref="System.Text.Json.JsonSerializerOptions"/> already finds them
/// and this context's own options carry nothing beyond the generation mode.
/// The leg-2 containment problem — a parameterized converter registration
/// that <c>[JsonSourceGenerationOptions]</c> cannot express, making the
/// context's own options a trap — has no instance here.
/// </para>
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ProblemStatsDocument))]
[JsonSerializable(typeof(QuizMix))]
public sealed partial class BgGameJsonContext : JsonSerializerContext
{
}
