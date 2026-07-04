namespace BgGame_Lib.Tests;

public class DiceCommitmentTests
{
    // ── Construction / validation ─────────────────────────────────

    [Fact]
    public void FromHex_RoundTripsThroughToHex()
    {
        var v = VerifiableDiceVectors.Named("zero-key");
        Assert.Equal(v.CommitmentHex, DiceCommitment.FromHex(v.CommitmentHex).ToHex());
    }

    [Fact]
    public void FromBytes_RoundTripsThroughToBytes()
    {
        var raw = Enumerable.Range(0, DiceCommitment.SizeInBytes).Select(i => (byte)(i * 7)).ToArray();
        Assert.Equal(raw, DiceCommitment.FromBytes(raw).ToBytes());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(33)]
    public void FromBytes_WrongLength_Throws(int length)
    {
        Assert.Throws<ArgumentException>(() => DiceCommitment.FromBytes(new byte[length]));
    }

    [Fact]
    public void FromHex_WrongDecodedLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => DiceCommitment.FromHex(new string('a', 30)));
    }

    [Fact]
    public void FromHex_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => DiceCommitment.FromHex(null!));
    }

    [Fact]
    public void ToBytes_ReturnsDefensiveCopy()
    {
        var c = DiceCommitment.FromBytes(new byte[DiceCommitment.SizeInBytes]);
        var first = c.ToBytes();
        first[0] ^= 0xFF;
        Assert.NotEqual(first, c.ToBytes());
    }

    // ── Value semantics ───────────────────────────────────────────

    [Fact]
    public void Equality_IsByValue()
    {
        var v = VerifiableDiceVectors.Named("zero-key");
        var a = DiceCommitment.FromHex(v.CommitmentHex);
        var b = DiceCommitment.FromHex(v.CommitmentHex);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_Null_IsFalse()
    {
        var v = VerifiableDiceVectors.Named("zero-key");
        Assert.False(DiceCommitment.FromHex(v.CommitmentHex).Equals(null));
    }

    // ── Verification (the reveal check) ───────────────────────────

    [Fact]
    public void Verifies_TrueForCommittingKeyAndContext()
    {
        var v = VerifiableDiceVectors.Named("counting-key");
        var key = DiceKey.FromHex(v.KeyHex);
        var commitment = DiceCommitment.FromHex(v.CommitmentHex);
        Assert.True(commitment.Verifies(key, v.Context));
    }

    [Fact]
    public void Verifies_FalseForWrongContext()
    {
        var v = VerifiableDiceVectors.Named("counting-key");
        var key = DiceKey.FromHex(v.KeyHex);
        var commitment = DiceCommitment.FromHex(v.CommitmentHex);
        Assert.False(commitment.Verifies(key, v.Context + "-tampered"));
    }

    [Fact]
    public void Verifies_FalseForWrongKey()
    {
        var v = VerifiableDiceVectors.Named("counting-key");
        var commitment = DiceCommitment.FromHex(v.CommitmentHex);
        Assert.False(commitment.Verifies(DiceKey.Generate(), v.Context));
    }

    [Fact]
    public void Verifies_RoundTripsAgainstAFreshKey()
    {
        var key = DiceKey.Generate();
        var commitment = key.Commit("match:99");
        Assert.True(commitment.Verifies(key, "match:99"));
        Assert.False(commitment.Verifies(key, "match:98"));
    }

    [Fact]
    public void Verifies_NullKey_Throws()
    {
        var c = DiceKey.Generate().Commit("c");
        Assert.Throws<ArgumentNullException>(() => c.Verifies(null!, "c"));
    }

    [Fact]
    public void Verifies_NullContext_Throws()
    {
        var c = DiceKey.Generate().Commit("c");
        Assert.Throws<ArgumentNullException>(() => c.Verifies(DiceKey.Generate(), null!));
    }
}
