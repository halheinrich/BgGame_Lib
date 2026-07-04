using System.Security.Cryptography;

namespace BgGame_Lib;

/// <summary>
/// A 256-bit SHA-256 commitment to a <see cref="DiceKey"/> bound to a context,
/// as produced by <see cref="DiceKey.Commit"/>. Under commit-and-reveal the
/// server publishes this before the first roll; after the match it reveals the
/// key, and either party confirms the reveal with <see cref="Verifies"/>.
///
/// <para>
/// A distinct type from <see cref="DiceKey"/> on purpose: a commitment is safe
/// to publish, a key is not, and the two must never be interchanged (publishing
/// a key where a commitment is expected would reveal the dice in advance). Both
/// happen to be 32 bytes, but they carry independent meanings, so the type
/// system keeps them apart. Immutable value with by-value equality.
/// </para>
/// </summary>
public sealed class DiceCommitment : IEquatable<DiceCommitment>
{
    /// <summary>The commitment length in bytes (a SHA-256 digest, 256 bits).</summary>
    public const int SizeInBytes = 32;

    // Exactly SizeInBytes long, owned by this instance, never exposed by reference.
    private readonly byte[] _bytes;

    // Assumes the array is exactly SizeInBytes long and owned (not aliased elsewhere).
    private DiceCommitment(byte[] owned) => _bytes = owned;

    // In-assembly ctor for DiceKey.Commit, which hands over a freshly allocated
    // 32-byte digest it does not retain — avoids a redundant defensive copy.
    internal static DiceCommitment FromOwnedDigest(byte[] digest) => new(digest);

    /// <summary>Reconstruct a commitment from its raw bytes. The bytes are copied.</summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="bytes"/> is not exactly <see cref="SizeInBytes"/> long.
    /// </exception>
    public static DiceCommitment FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != SizeInBytes)
            throw new ArgumentException(
                $"A dice commitment is exactly {SizeInBytes} bytes; got {bytes.Length}.", nameof(bytes));
        return new DiceCommitment(bytes.ToArray());
    }

    /// <summary>
    /// Reconstruct a commitment from its lowercase- or uppercase-hex encoding
    /// (the on-the-wire form). Expects <c>2 × <see cref="SizeInBytes"/></c> hex digits.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="hex"/> is null.</exception>
    /// <exception cref="FormatException"><paramref name="hex"/> is not valid hex.</exception>
    /// <exception cref="ArgumentException">
    /// The decoded length is not exactly <see cref="SizeInBytes"/> bytes.
    /// </exception>
    public static DiceCommitment FromHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        return FromBytes(Convert.FromHexString(hex));
    }

    /// <summary>The commitment as a lowercase-hex string (the wire form).</summary>
    public string ToHex() => Convert.ToHexStringLower(_bytes);

    /// <summary>A fresh copy of the commitment's raw bytes.</summary>
    public byte[] ToBytes() => (byte[])_bytes.Clone();

    /// <summary>
    /// Verify a revealed key against this commitment under the same
    /// <paramref name="context"/>: true iff <c>key.Commit(context)</c> reproduces
    /// this commitment. Single-sources the recompute-and-compare step so verifiers
    /// do not re-encode the commitment rule. The comparison is fixed-time to avoid
    /// leaking match information through timing, though the commitment is public.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="key"/> or <paramref name="context"/> is null.
    /// </exception>
    public bool Verifies(DiceKey key, string context)
    {
        ArgumentNullException.ThrowIfNull(key);
        // context null-check is enforced by Commit.
        DiceCommitment recomputed = key.Commit(context);
        return CryptographicOperations.FixedTimeEquals(_bytes, recomputed._bytes);
    }

    /// <inheritdoc/>
    public bool Equals(DiceCommitment? other) =>
        other is not null && _bytes.AsSpan().SequenceEqual(other._bytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DiceCommitment);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(_bytes);
        return hash.ToHashCode();
    }
}
