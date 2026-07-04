using System.Buffers.Binary;
using System.Security.Cryptography;

namespace BgGame_Lib;

/// <summary>
/// A deterministic <see cref="IDiceSource"/> whose entire roll sequence is fixed
/// by a <see cref="DiceKey"/>, for the commit-and-reveal fairness scheme: the
/// server commits to the key before the first roll and reveals it at match end,
/// so either party can reconstruct this source from the key and confirm every
/// roll. The derivation below is deliberately public and language-neutral —
/// verification requires third parties to re-implement it — so it is specified
/// precisely rather than optimized.
///
/// <para><b>Derivation.</b></para>
/// <list type="number">
///   <item><description>
///     <b>Keystream.</b> An unbounded byte stream is the concatenation of
///     <c>HMAC-SHA256(key, BE64(blockIndex))</c> for <c>blockIndex = 0, 1, 2, …</c>,
///     where <c>BE64</c> is the 8-byte big-endian encoding of the block index.
///     Each block contributes 32 bytes.
///   </description></item>
///   <item><description>
///     <b>Die extraction (rejection sampling).</b> To produce one die, read the
///     next keystream byte <c>b</c>: if <c>b &gt;= 252</c> reject it and read the
///     next byte; otherwise the die is <c>(b % 6) + 1</c>. 252 is the largest
///     multiple of 6 that is ≤ 256, so bytes 0–251 map uniformly onto the six
///     faces (42 values each) and bytes 252–255 are discarded — this removes the
///     modulo bias a naïve <c>(b % 6)</c> over all 256 values would introduce.
///   </description></item>
///   <item><description>
///     <b>Roll.</b> <see cref="Roll"/> returns the next two accepted dice as
///     <c>(Die1, Die2)</c>. Rejected bytes are consumed but never surface; the
///     stream stays aligned across them.
///   </description></item>
/// </list>
///
/// <para>
/// Contrast with <see cref="SeededDiceSource"/>: that source is reproducible from
/// a seed but its algorithm (framework <see cref="Random"/>) is neither owned nor
/// audit-grade, and observing its rolls can in principle recover its state. Here
/// the algorithm is public and unpredictability lives solely in the secret key;
/// observing rolls yields no predictive power without breaking HMAC-SHA256.
/// </para>
///
/// <para>Not thread-safe — use one instance per driver.</para>
/// </summary>
public sealed class VerifiableDiceSource : IDiceSource
{
    private const int BlockSizeInBytes = 32;   // one HMAC-SHA256 output block
    private const int RejectionThreshold = 252; // largest multiple of 6 ≤ 256

    private readonly byte[] _key;               // copy of the driving key's bytes

    private readonly byte[] _block = new byte[BlockSizeInBytes];
    private ulong _nextBlockIndex;              // index of the block to compute next
    private int _blockPos = BlockSizeInBytes;   // read cursor; == BlockSizeInBytes ⇒ block exhausted

    /// <summary>
    /// Construct a source whose complete roll sequence is determined by
    /// <paramref name="key"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public VerifiableDiceSource(DiceKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        _key = key.Bytes.ToArray();
    }

    /// <inheritdoc/>
    public (int Die1, int Die2) Roll() => (NextDie(), NextDie());

    // The next accepted die, skipping rejected bytes to preserve uniformity.
    private int NextDie()
    {
        int die;
        while (!TryMapByteToDie(NextByte(), out die))
        {
            // b in 252..255: rejected, draw again.
        }
        return die;
    }

    /// <summary>
    /// The single home of the byte→die rule: accepts <c>b</c> in 0–251 as
    /// <c>(b % 6) + 1</c> (a face in 1–6), rejects 252–255. Internal so the
    /// rejection/uniformity contract can be pinned directly over the whole byte
    /// domain without needing a key whose stream happens to hit a rejection.
    /// </summary>
    internal static bool TryMapByteToDie(byte b, out int die)
    {
        if (b < RejectionThreshold)
        {
            die = (b % 6) + 1;
            return true;
        }
        die = 0;
        return false;
    }

    // The next keystream byte, computing the next HMAC block on demand.
    private byte NextByte()
    {
        if (_blockPos >= BlockSizeInBytes)
        {
            Span<byte> counter = stackalloc byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64BigEndian(counter, _nextBlockIndex);
            HMACSHA256.HashData(_key, counter, _block);
            // Fail fast rather than wrap to block 0 and silently repeat the
            // stream — unreachable in practice (2^64 blocks ≈ 5.9e20 dice).
            checked { _nextBlockIndex++; }
            _blockPos = 0;
        }
        return _block[_blockPos++];
    }
}
