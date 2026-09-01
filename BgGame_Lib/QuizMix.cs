namespace BgGame_Lib;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// The stats-weighted quiz composition config: an ordered set of
/// <see cref="QuizMixEntry"/> lines (category + percentage), an optional quiz
/// length, and the random toggle. Immutable;
/// <see cref="MixedProblemSetSource"/> reads it to decide <i>which</i>
/// decisions form a quiz.
///
/// <para>
/// <b>Blank mix = passthrough.</b> <see cref="Empty"/> (no entries) makes the
/// composition layer fully inert: every inner decision, source order,
/// today's behavior. A quiz length without entries is rejected — blank means
/// inert, and "cap at N without weighting" is expressed losslessly as a
/// single <see cref="QuizCategory.EverythingElse"/> entry at 100 percent plus
/// a length, so no second blank-ish state exists.
/// </para>
///
/// <para>
/// <b>Entry order is contractual.</b> When categories overlap, composition
/// draws entries in declared order, so contested decisions go to the earlier
/// entry. Consumers rendering or persisting entry lists must preserve the
/// order; the JSON form does.
/// </para>
///
/// <para>
/// <b>Set-level rules</b> (fail fast at construction): non-empty entries must
/// sum to exactly 100; no duplicate categories (same kind <i>and</i>
/// parameter — the same kind with different parameters, e.g.
/// <c>SeenFewerThan(3)</c> and <c>SeenFewerThan(5)</c>, is two distinct
/// categories and allowed); quiz length, when present, at least 1. The
/// per-entry percent bound lives on <see cref="QuizMixEntry"/>.
/// </para>
///
/// <para>
/// <b>Wire format.</b> JSON via the bundled
/// <see cref="QuizMixJsonConverter"/> (type-level <c>[JsonConverter]</c> —
/// consumers register nothing) with schema version
/// <see cref="CurrentSchemaVersion"/>; fixed property names, fail-loud reads,
/// a version bump as the format's only evolution mechanism. Consumers that
/// persist a mix (e.g. browser localStorage) round-trip through
/// <see cref="ToJson"/> / <see cref="FromJson"/>, with
/// <see cref="TryFromJson"/> as the absent-or-corrupt-restores-to-default
/// path — the <c>FilterConfig</c> persistence trio. All three resolve their
/// metadata from <see cref="BgGameJsonContext"/> rather than by runtime
/// reflection, so the trio is trim-safe from here without a consumer naming
/// anything (halheinrich/backgammon#129).
/// </para>
///
/// <para>
/// <b>Value equality over the full member surface.</b> Two mixes are equal
/// when <see cref="Entries"/> match as an ordered sequence and
/// <see cref="QuizLength"/> and <see cref="RandomOrder"/> match — so "does
/// this draft differ from the committed mix?" is a single <c>==</c>. Order
/// participates because entry order is contractual (see above): the same
/// entries reordered are a different mix. A class with hand-written equality
/// rather than a record: the type wraps a list, and the record-generated form
/// would compare that list by reference — reporting identical configs
/// unequal — where the hand-rolled form compares its elements
/// (<see cref="QuizMixEntry"/> is a record with full value equality).
/// </para>
/// </summary>
[JsonConverter(typeof(QuizMixJsonConverter))]
public sealed class QuizMix : IEquatable<QuizMix>
{
    /// <summary>
    /// The schema version this library reads and writes. Reads reject any
    /// other version (fail-loud); see <see cref="QuizMixJsonConverter"/>.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// The blank mix: no entries, no length — the composition layer is fully
    /// inert (passthrough).
    /// </summary>
    public static QuizMix Empty { get; } = new([]);

    /// <summary>
    /// Create a mix from its entry lines, optional quiz length, and random
    /// toggle.
    /// </summary>
    /// <param name="entries">
    /// The entry lines, in draw order (order is contractual — see the type
    /// remarks). Empty means passthrough.
    /// </param>
    /// <param name="quizLength">
    /// Requested quiz length, or <see langword="null"/> to size the quiz to
    /// every decision the selected categories reach (the deduped union of
    /// their pools).
    /// </param>
    /// <param name="randomOrder">
    /// <see langword="true"/> (the default): randomized draws and shuffled
    /// presentation. <see langword="false"/>: fully deterministic — source
    /// order drives both draws and presentation.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="entries"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="entries"/> contains a <see langword="null"/>
    /// element or a duplicate category, when non-empty entries do not sum to
    /// exactly 100, or when <paramref name="quizLength"/> is given with no
    /// entries (blank means inert — see the type remarks).
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="quizLength"/> is less than 1.
    /// </exception>
    public QuizMix(IEnumerable<QuizMixEntry> entries, int? quizLength = null, bool randomOrder = true)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var list = entries.ToImmutableArray();

        var seen = new HashSet<QuizCategory>();
        var sum = 0;
        foreach (var entry in list)
        {
            if (entry is null)
                throw new ArgumentException("Entries collection contains a null element.", nameof(entries));
            if (!seen.Add(entry.Category))
                throw new ArgumentException(
                    $"Duplicate category {entry.Category}.", nameof(entries));
            sum += entry.Percent;
        }

        if (!list.IsEmpty && sum != 100)
            throw new ArgumentException(
                $"Entry percents must sum to exactly 100 (got {sum}).", nameof(entries));

        if (quizLength is < 1)
            throw new ArgumentOutOfRangeException(
                nameof(quizLength), quizLength, "Quiz length must be at least 1.");

        if (list.IsEmpty && quizLength is not null)
            throw new ArgumentException(
                "A quiz length without entries has no composition to apply — a blank mix is " +
                "fully inert. Use a single EverythingElse entry at 100 percent to cap the " +
                "length without weighting.", nameof(quizLength));

        Entries = list;
        QuizLength = quizLength;
        RandomOrder = randomOrder;
    }

    /// <summary>
    /// The entry lines, in draw order. Order is contractual: under
    /// overlapping categories, earlier entries draw first.
    /// </summary>
    public IReadOnlyList<QuizMixEntry> Entries { get; }

    /// <summary>
    /// Requested quiz length; <see langword="null"/> sizes the quiz to the
    /// deduped union of the selected categories' pools.
    /// </summary>
    public int? QuizLength { get; }

    /// <summary>
    /// <see langword="true"/>: randomized draws and shuffled presentation.
    /// <see langword="false"/>: fully deterministic — source order drives
    /// both draws and presentation.
    /// </summary>
    public bool RandomOrder { get; }

    /// <summary>
    /// Whether this mix is blank (no entries) and therefore makes the
    /// composition layer fully inert.
    /// </summary>
    public bool IsPassthrough => Entries.Count == 0;

    /// <summary>
    /// Value equality over the full member surface: <see cref="Entries"/> as
    /// an ordered sequence (order is contractual — the same entries reordered
    /// are a different mix), plus <see cref="QuizLength"/> and
    /// <see cref="RandomOrder"/>. See the type remarks.
    /// </summary>
    /// <param name="other">The mix to compare with, or <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="other"/> has equal content;
    /// <see langword="false"/> otherwise, including when <paramref name="other"/>
    /// is <see langword="null"/>.
    /// </returns>
    public bool Equals(QuizMix? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        return other is not null
            && QuizLength == other.QuizLength
            && RandomOrder == other.RandomOrder
            && Entries.SequenceEqual(other.Entries);
    }

    /// <inheritdoc cref="Equals(QuizMix?)"/>
    public override bool Equals(object? obj) => Equals(obj as QuizMix);

    /// <summary>
    /// Hash code consistent with <see cref="Equals(QuizMix?)"/>: folds the
    /// entry sequence in order, then <see cref="QuizLength"/> and
    /// <see cref="RandomOrder"/>. Safe to hash — the type is immutable.
    /// </summary>
    /// <returns>The content-based hash code.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var entry in Entries)
            hash.Add(entry);
        hash.Add(QuizLength);
        hash.Add(RandomOrder);
        return hash.ToHashCode();
    }

    /// <summary>
    /// Content equality per <see cref="Equals(QuizMix?)"/>; two
    /// <see langword="null"/> operands are equal.
    /// </summary>
    /// <param name="left">The first mix, or <see langword="null"/>.</param>
    /// <param name="right">The second mix, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if both are <see langword="null"/> or have equal content.</returns>
    public static bool operator ==(QuizMix? left, QuizMix? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>
    /// Negation of <see cref="operator ==(QuizMix?, QuizMix?)"/>.
    /// </summary>
    /// <param name="left">The first mix, or <see langword="null"/>.</param>
    /// <param name="right">The second mix, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the operands differ in content or nullness.</returns>
    public static bool operator !=(QuizMix? left, QuizMix? right) => !(left == right);

    /// <summary>
    /// Serializes this mix to its canonical JSON representation — the inverse
    /// of <see cref="FromJson"/>. See <see cref="QuizMixJsonConverter"/> for
    /// the pinned wire format.
    /// </summary>
    /// <returns>A JSON object string carrying every property of this instance.</returns>
    public string ToJson() => JsonSerializer.Serialize(this, BgGameJsonContext.Default.QuizMix);

    /// <summary>
    /// Deserializes a <see cref="QuizMix"/> from its canonical JSON
    /// representation — the inverse of <see cref="ToJson"/>. Fail-loud: see
    /// <see cref="QuizMixJsonConverter"/> for everything a read rejects.
    /// </summary>
    /// <param name="json">A JSON object string, typically produced by <see cref="ToJson"/>.</param>
    /// <returns>The materialized mix.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="json"/> is the literal <c>null</c> token, which yields
    /// no mix.
    /// </exception>
    /// <exception cref="JsonException"><paramref name="json"/> is malformed or violates the wire contract.</exception>
    public static QuizMix FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        return JsonSerializer.Deserialize(json, BgGameJsonContext.Default.QuizMix)
            ?? throw new ArgumentException(
                "JSON deserialized to a null mix; expected a QuizMix object.",
                nameof(json));
    }

    /// <summary>
    /// Non-throwing counterpart to <see cref="FromJson"/>. Absorbs the three
    /// ways a restore can fail — a null <paramref name="json"/> (e.g. a
    /// storage key never written), the literal <c>null</c> token, or
    /// malformed/contract-violating JSON — and yields <see cref="Empty"/> in
    /// each case. This single-sources the "absent or corrupt input restores
    /// to the inert default" policy so consumers need no knowledge of the
    /// JSON representation or its exception taxonomy.
    /// </summary>
    /// <param name="json">
    /// The candidate JSON, or null. Typically read straight from a
    /// persistence store whose contents the caller does not control.
    /// </param>
    /// <param name="mix">
    /// On return, always a usable mix: the restored instance on success, or
    /// <see cref="Empty"/> on failure.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="json"/> was successfully
    /// deserialized; <see langword="false"/> otherwise, letting a caller
    /// react to a failed restore without catching exceptions.
    /// </returns>
    public static bool TryFromJson(string? json, out QuizMix mix)
    {
        if (json is not null)
        {
            try
            {
                if (JsonSerializer.Deserialize(json, BgGameJsonContext.Default.QuizMix) is { } parsed)
                {
                    mix = parsed;
                    return true;
                }
            }
            catch (JsonException)
            {
                // Malformed or contract-violating JSON falls through to the
                // inert default below; any other (unexpected) exception is
                // intentionally left to propagate.
            }
        }

        mix = Empty;
        return false;
    }
}
