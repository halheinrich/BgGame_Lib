namespace BgGame_Lib;

/// <summary>
/// Dice source that replays a fixed pre-recorded sequence of rolls, in order.
///
/// Uses: fully deterministic tests (script the exact dice a scenario needs)
/// and, in a later arc, duplicate-dice tournament pairings (two mirrored
/// matches consuming the same recorded sequence).
///
/// The sequence is materialised and validated eagerly at construction — a bad
/// pair fails fast at the call site that recorded it, not mid-match. When the
/// sequence is exhausted, <see cref="Roll"/> throws rather than wrapping around
/// or repeating: silently recycled dice would corrupt the very determinism the
/// source exists to provide (parallel to the <see cref="IProblemSetSource"/>
/// stance that a consumed one-shot resource must throw, not yield empty).
///
/// Not thread-safe — use one instance per driver.
/// </summary>
public sealed class RecordedDiceSource : IDiceSource
{
    private readonly (int Die1, int Die2)[] _rolls;
    private int _next;

    /// <summary>
    /// Construct from the sequence of rolls to replay. The sequence is copied;
    /// later changes to <paramref name="rolls"/> do not affect this source.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="rolls"/> is null.</exception>
    /// <exception cref="ArgumentException">A pair contains a die outside 1–6.</exception>
    public RecordedDiceSource(IEnumerable<(int Die1, int Die2)> rolls)
    {
        ArgumentNullException.ThrowIfNull(rolls);

        _rolls = rolls.ToArray();
        for (int i = 0; i < _rolls.Length; i++)
        {
            var (die1, die2) = _rolls[i];
            if (die1 < 1 || die1 > 6 || die2 < 1 || die2 > 6)
                throw new ArgumentException(
                    $"Roll at index {i} is ({die1}, {die2}); each die must be in 1–6.",
                    nameof(rolls));
        }
    }

    /// <summary>Number of rolls remaining before the source is exhausted.</summary>
    public int Remaining => _rolls.Length - _next;

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The recorded sequence is exhausted.</exception>
    public (int Die1, int Die2) Roll()
    {
        if (_next >= _rolls.Length)
            throw new InvalidOperationException(
                $"Recorded dice sequence exhausted after {_rolls.Length} roll(s).");
        return _rolls[_next++];
    }
}
