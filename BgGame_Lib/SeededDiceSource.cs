namespace BgGame_Lib;

/// <summary>
/// Pseudo-random dice source over <see cref="Random"/> with an explicit seed:
/// the same seed always yields the same roll sequence within a given .NET
/// runtime version.
///
/// Reproducibility scope: seeded <see cref="Random"/> uses the framework's
/// compatibility algorithm, which is stable in practice but not contractually
/// guaranteed across major .NET versions. That is sufficient for deterministic
/// tests and replayable in-proc matches; audit-grade dice with an owned,
/// version-independent algorithm are provided by <see cref="VerifiableDiceSource"/>
/// (commit-and-reveal fairness). An explicit seed means the caller already knows
/// the sequence, so seeded mode stays as-is — committing to it would be theater.
///
/// Not thread-safe — use one instance per driver.
/// </summary>
public sealed class SeededDiceSource : IDiceSource
{
    private readonly Random _random;

    /// <summary>Construct a source whose roll sequence is determined by <paramref name="seed"/>.</summary>
    public SeededDiceSource(int seed)
    {
        _random = new Random(seed);
    }

    /// <inheritdoc/>
    public (int Die1, int Die2) Roll() => (_random.Next(1, 7), _random.Next(1, 7));
}
