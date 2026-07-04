namespace BgGame_Lib.Tests;

public class DiceKeyTests
{
    public static IEnumerable<object[]> VectorLabels =>
        VerifiableDiceVectors.All.Select(v => new object[] { v.Label });

    // ── Generation ────────────────────────────────────────────────

    [Fact]
    public void Generate_ProducesA32ByteKey()
    {
        var key = DiceKey.Generate();
        Assert.Equal(DiceKey.SizeInBytes, key.ToBytes().Length);
    }

    [Fact]
    public void Generate_YieldsDistinctKeys()
    {
        // Two cryptographically random 256-bit keys colliding is negligible;
        // an equal pair would indicate a broken (e.g. constant) generator.
        var a = DiceKey.Generate();
        var b = DiceKey.Generate();
        Assert.NotEqual(a, b);
    }

    // ── Construction / validation ─────────────────────────────────

    [Fact]
    public void FromBytes_RoundTripsThroughToBytes()
    {
        var raw = Enumerable.Range(0, DiceKey.SizeInBytes).Select(i => (byte)i).ToArray();
        var key = DiceKey.FromBytes(raw);
        Assert.Equal(raw, key.ToBytes());
    }

    [Fact]
    public void FromHex_RoundTripsThroughToHex()
    {
        string hex = new string('a', DiceKey.SizeInBytes * 2);
        Assert.Equal(hex, DiceKey.FromHex(hex).ToHex());
    }

    [Fact]
    public void FromHex_IsCaseInsensitive_AndNormalizesToLower()
    {
        string upper = new string('A', DiceKey.SizeInBytes * 2);
        Assert.Equal(new string('a', DiceKey.SizeInBytes * 2), DiceKey.FromHex(upper).ToHex());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(33)]
    [InlineData(64)]
    public void FromBytes_WrongLength_Throws(int length)
    {
        Assert.Throws<ArgumentException>(() => DiceKey.FromBytes(new byte[length]));
    }

    [Fact]
    public void FromHex_WrongDecodedLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => DiceKey.FromHex(new string('a', 30)));
    }

    [Fact]
    public void FromHex_NonHex_ThrowsFormat()
    {
        Assert.Throws<FormatException>(() => DiceKey.FromHex(new string('z', DiceKey.SizeInBytes * 2)));
    }

    [Fact]
    public void FromHex_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => DiceKey.FromHex(null!));
    }

    // ── Immutability / value semantics ────────────────────────────

    [Fact]
    public void ToBytes_ReturnsDefensiveCopy()
    {
        var key = DiceKey.Generate();
        var first = key.ToBytes();
        first[0] ^= 0xFF;                       // mutate the returned array
        Assert.Equal(key.ToBytes(), key.ToBytes()); // subsequent reads are unaffected
        Assert.NotEqual(first, key.ToBytes());
    }

    [Fact]
    public void Equality_IsByValue()
    {
        var raw = RandomBytes();
        var a = DiceKey.FromBytes(raw);
        var b = DiceKey.FromBytes(raw);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equality_DistinctBytes_AreUnequal()
    {
        var a = DiceKey.FromBytes(RandomBytes());
        var b = DiceKey.FromBytes(RandomBytes());
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_Null_IsFalse()
    {
        Assert.False(DiceKey.Generate().Equals(null));
    }

    // ── Commitment (pinned + properties) ──────────────────────────

    [Theory]
    [MemberData(nameof(VectorLabels))]
    public void Commit_MatchesPinnedCommitment(string label)
    {
        var v = VerifiableDiceVectors.Named(label);
        var key = DiceKey.FromHex(v.KeyHex);
        Assert.Equal(v.CommitmentHex, key.Commit(v.Context).ToHex());
    }

    [Fact]
    public void Commit_IsContextSensitive()
    {
        // The two vectors share the all-zero key but differ in context; the
        // committed file pins different commitments for them.
        var a = VerifiableDiceVectors.Named("zero-key");
        var b = VerifiableDiceVectors.Named("context-sensitivity");
        Assert.Equal(a.KeyHex, b.KeyHex);
        Assert.NotEqual(a.Context, b.Context);

        var key = DiceKey.FromHex(a.KeyHex);
        Assert.NotEqual(key.Commit(a.Context), key.Commit(b.Context));
    }

    [Fact]
    public void Commit_EmptyContext_IsValid_AndDiffersFromNonEmpty()
    {
        var key = DiceKey.Generate();
        var empty = key.Commit("");
        Assert.Equal(DiceCommitment.SizeInBytes, empty.ToBytes().Length);
        Assert.NotEqual(empty, key.Commit("x"));
    }

    [Fact]
    public void Commit_IsDeterministic()
    {
        var key = DiceKey.Generate();
        Assert.Equal(key.Commit("ctx"), key.Commit("ctx"));
    }

    [Fact]
    public void Commit_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => DiceKey.Generate().Commit(null!));
    }

    private static byte[] RandomBytes()
    {
        var b = new byte[DiceKey.SizeInBytes];
        // Deterministic-free: content is irrelevant, only distinctness/round-trip matters.
        System.Security.Cryptography.RandomNumberGenerator.Fill(b);
        return b;
    }
}
