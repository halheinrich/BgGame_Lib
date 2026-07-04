namespace BgGame_Lib.Tests;

using System.Buffers.Binary;
using System.Security.Cryptography;

public class VerifiableDiceSourceTests
{
    public static IEnumerable<object[]> VectorLabels =>
        VerifiableDiceVectors.All.Select(v => new object[] { v.Label });

    // ── Vector pinning (cross-language contract) ──────────────────

    [Theory]
    [MemberData(nameof(VectorLabels))]
    public void Source_ReproducesPinnedRolls(string label)
    {
        var v = VerifiableDiceVectors.Named(label);
        var source = new VerifiableDiceSource(DiceKey.FromHex(v.KeyHex));

        foreach (var expected in v.RollTuples)
            Assert.Equal(expected, source.Roll());
    }

    [Theory]
    [MemberData(nameof(VectorLabels))]
    public void PinnedBlock0_IsHmacOfBlockIndexZero(string label)
    {
        // Checkpoint for third-party implementers: block0 is the first 32
        // keystream bytes = HMAC-SHA256(key, BE64(0)). Re-derive via the BCL,
        // independent of the source, and confirm the committed value.
        var v = VerifiableDiceVectors.Named(label);
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(counter, 0UL);
        byte[] block0 = HMACSHA256.HashData(Convert.FromHexString(v.KeyHex), counter);
        Assert.Equal(v.Block0Hex, Convert.ToHexStringLower(block0));
    }

    // ── Determinism ───────────────────────────────────────────────

    [Fact]
    public void SameKey_YieldsIdenticalSequences()
    {
        var key = DiceKey.Generate();
        var a = new VerifiableDiceSource(key);
        var b = new VerifiableDiceSource(key);

        for (int i = 0; i < 500; i++)
            Assert.Equal(a.Roll(), b.Roll());
    }

    [Fact]
    public void DifferentKeys_DivergeSomewhere()
    {
        var a = new VerifiableDiceSource(DiceKey.Generate());
        var b = new VerifiableDiceSource(DiceKey.Generate());

        bool diverged = false;
        for (int i = 0; i < 200 && !diverged; i++)
            diverged = a.Roll() != b.Roll();

        Assert.True(diverged);
    }

    // ── Rejection sampling ────────────────────────────────────────

    [Fact]
    public void MapByteToDie_AcceptsBelowThreshold_WithUniformMapping()
    {
        // Every byte 0–251 maps to a face in 1–6 as (b % 6) + 1, and each face
        // is hit exactly 252 / 6 = 42 times — no modulo bias.
        var counts = new int[7];
        for (int b = 0; b < 252; b++)
        {
            Assert.True(VerifiableDiceSource.TryMapByteToDie((byte)b, out int die));
            Assert.Equal((b % 6) + 1, die);
            Assert.InRange(die, 1, 6);
            counts[die]++;
        }
        for (int face = 1; face <= 6; face++)
            Assert.Equal(42, counts[face]);
    }

    [Theory]
    [InlineData(252)]
    [InlineData(253)]
    [InlineData(254)]
    [InlineData(255)]
    public void MapByteToDie_RejectsAtOrAboveThreshold(int b)
    {
        Assert.False(VerifiableDiceSource.TryMapByteToDie((byte)b, out int die));
        Assert.Equal(0, die);
    }

    [Fact]
    public void Source_StaysAlignedAcrossRejections()
    {
        // The rejection-branch vector's keystream contains rejected bytes (>=252)
        // within the pinned span; reproducing its rolls exactly proves the source
        // skips those bytes and stays aligned rather than emitting a biased die or
        // losing sync.
        var v = VerifiableDiceVectors.Named("rejection-branch");
        Assert.NotEmpty(v.RejectedByteOffsets);

        var source = new VerifiableDiceSource(DiceKey.FromHex(v.KeyHex));
        foreach (var expected in v.RollTuples)
            Assert.Equal(expected, source.Roll());
    }

    // ── Distribution sanity ───────────────────────────────────────

    [Fact]
    public void LongStream_AllDiceInRange()
    {
        var source = new VerifiableDiceSource(DiceKey.Generate());
        for (int i = 0; i < 5000; i++)
        {
            var (d1, d2) = source.Roll();
            Assert.InRange(d1, 1, 6);
            Assert.InRange(d2, 1, 6);
        }
    }

    [Fact]
    public void LongStream_AllThirtySixOrderedPairsAppear()
    {
        var source = new VerifiableDiceSource(DiceKey.Generate());
        var seen = new HashSet<(int, int)>();
        for (int i = 0; i < 20000 && seen.Count < 36; i++)
            seen.Add(source.Roll());

        Assert.Equal(36, seen.Count);
    }

    // ── Validation ────────────────────────────────────────────────

    [Fact]
    public void Ctor_NullKey_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new VerifiableDiceSource(null!));
    }
}
