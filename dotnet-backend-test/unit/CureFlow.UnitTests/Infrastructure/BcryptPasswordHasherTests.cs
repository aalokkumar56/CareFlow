using CureFlow.Infrastructure.Identity;
using FluentAssertions;
using Xunit;

namespace CureFlow.UnitTests.Infrastructure;

public class BcryptPasswordHasherTests
{
    private readonly BcryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_then_Verify_succeeds_for_same_password()
    {
        var hash = _hasher.Hash("TestHospital123!");
        _hasher.Verify("TestHospital123!", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_rejects_wrong_password()
    {
        var hash = _hasher.Hash("correct-password");
        _hasher.Verify("wrong-password", hash).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Verify_returns_false_for_blank_hash(string? hash) =>
        _hasher.Verify("any-password", hash!).Should().BeFalse();

    [Fact]
    public void Hash_produces_distinct_salts_for_same_password()
    {
        var a = _hasher.Hash("same-password");
        var b = _hasher.Hash("same-password");
        a.Should().NotBe(b);
        _hasher.Verify("same-password", a).Should().BeTrue();
        _hasher.Verify("same-password", b).Should().BeTrue();
    }

    [Fact]
    public void Hash_and_Verify_support_unicode_passwords()
    {
        const string password = "पASS-🔐-Ångström";
        var hash = _hasher.Hash(password);
        _hasher.Verify(password, hash).Should().BeTrue();
        _hasher.Verify("पASS-🔐-Angstrom", hash).Should().BeFalse();
    }

    [Fact]
    public void Verify_throws_on_malformed_hash()
    {
        var act = () => _hasher.Verify("password", "not-a-bcrypt-hash");
        act.Should().Throw<BCrypt.Net.SaltParseException>();
    }
}
