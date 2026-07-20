namespace BgGame_Lib;

/// <summary>
/// One line of a <see cref="QuizMix"/>: a <see cref="QuizCategory"/> and the
/// percentage of the quiz it should supply. Immutable with value equality.
///
/// <para>
/// The per-entry rule lives here, at the earliest construction point: an
/// entry with an out-of-range percent never exists un-failed. The set-level
/// rules (percents summing to exactly 100, no duplicate categories) belong to
/// <see cref="QuizMix"/>, which owns the entry set.
/// </para>
/// </summary>
public sealed record QuizMixEntry
{
    /// <summary>Create an entry supplying <paramref name="percent"/> percent
    /// of the quiz from <paramref name="category"/>.</summary>
    /// <param name="category">The category this entry draws from.</param>
    /// <param name="percent">This entry's share of the quiz, in 1–100.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="category"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="percent"/> is outside 1–100.
    /// </exception>
    public QuizMixEntry(QuizCategory category, int percent)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentOutOfRangeException.ThrowIfLessThan(percent, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percent, 100);
        Category = category;
        Percent = percent;
    }

    /// <summary>The category this entry draws from.</summary>
    public QuizCategory Category { get; }

    /// <summary>This entry's share of the quiz, in 1–100.</summary>
    public int Percent { get; }
}
