using System.Security.Cryptography;
using System.Text;

namespace BgGame_Lib;

/// <summary>
/// A 256-bit (32-byte) secret used to drive a match's verifiable dice
/// (<see cref="VerifiableDiceSource"/>) under the commit-and-reveal fairness
/// scheme: the server commits to the key before the first roll (via
/// <see cref="Commit"/>) and reveals the key itself at match end, letting either
/// party re-derive every roll and confirm the dice were fixed in advance and
/// never adapted mid-match.
///
/// <para>
/// Immutable value: the 32 bytes are copied in on construction and never handed
/// out by reference, so a key cannot change after it is created. Equality is by
/// value (two keys with the same bytes are equal). Unpredictability lives
/// entirely in these bytes — the derivation algorithm is deliberately public
/// (Kerckhoffs), so a key must be generated from a cryptographic source
/// (<see cref="Generate"/>) and kept secret until reveal.
/// </para>
/// </summary>
public sealed class DiceKey : IEquatable<DiceKey>
{
    /// <summary>The fixed key length in bytes (256 bits).</summary>
    public const int SizeInBytes = 32;

    // Exactly SizeInBytes long, owned by this instance, never exposed by reference.
    private readonly byte[] _bytes;

    // Assumes the array is exactly SizeInBytes long and owned (not aliased elsewhere).
    private DiceKey(byte[] owned) => _bytes = owned;

    /// <summary>
    /// Generate a fresh key from a cryptographically secure random source
    /// (<see cref="RandomNumberGenerator"/>). This is the only construction path
    /// that yields the unpredictability the fairness scheme relies on.
    /// </summary>
    public static DiceKey Generate() => new(RandomNumberGenerator.GetBytes(SizeInBytes));

    /// <summary>
    /// Reconstruct a key from its raw bytes (e.g. a revealed key received for
    /// verification). The bytes are copied.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="bytes"/> is not exactly <see cref="SizeInBytes"/> long.
    /// </exception>
    public static DiceKey FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != SizeInBytes)
            throw new ArgumentException(
                $"A dice key is exactly {SizeInBytes} bytes; got {bytes.Length}.", nameof(bytes));
        return new DiceKey(bytes.ToArray());
    }

    /// <summary>
    /// Reconstruct a key from its lowercase- or uppercase-hex encoding (the
    /// on-the-wire reveal form). Expects <c>2 × <see cref="SizeInBytes"/></c> hex
    /// digits.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="hex"/> is null.</exception>
    /// <exception cref="FormatException"><paramref name="hex"/> is not valid hex.</exception>
    /// <exception cref="ArgumentException">
    /// The decoded length is not exactly <see cref="SizeInBytes"/> bytes.
    /// </exception>
    public static DiceKey FromHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        return FromBytes(Convert.FromHexString(hex));
    }

    /// <summary>The key as a lowercase-hex string (the reveal form).</summary>
    public string ToHex() => Convert.ToHexStringLower(_bytes);

    /// <summary>A fresh copy of the key's raw bytes.</summary>
    public byte[] ToBytes() => (byte[])_bytes.Clone();

    /// <summary>
    /// Compute the commitment for this key bound to <paramref name="context"/>:
    /// <c>SHA-256(key ‖ UTF-8(context))</c>. Because the key length is fixed at
    /// <see cref="SizeInBytes"/>, the concatenation is unambiguous — no separator
    /// or length prefix is needed. Pure: it neither mutates nor reveals the key.
    /// The caller supplies the binding context (the tournament server binds the
    /// match id) so a commitment made for one match cannot be replayed for
    /// another.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public DiceCommitment Commit(string context)
    {
        ArgumentNullException.ThrowIfNull(context);

        int contextByteCount = Encoding.UTF8.GetByteCount(context);
        byte[] message = new byte[SizeInBytes + contextByteCount];
        _bytes.CopyTo(message.AsSpan());
        Encoding.UTF8.GetBytes(context, message.AsSpan(SizeInBytes));

        byte[] digest = SHA256.HashData(message);
        return DiceCommitment.FromOwnedDigest(digest);
    }

    /// <summary>
    /// The key bytes, without copying, for in-assembly consumers
    /// (<see cref="VerifiableDiceSource"/>). Not exposed publicly — external
    /// callers copy via <see cref="ToBytes"/>.
    /// </summary>
    internal ReadOnlySpan<byte> Bytes => _bytes;

    /// <inheritdoc/>
    public bool Equals(DiceKey? other) =>
        other is not null && _bytes.AsSpan().SequenceEqual(other._bytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DiceKey);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(_bytes);
        return hash.ToHashCode();
    }
}
