using Trail.Application.Features.Integrations;
using Trail.Domain.Integrations;

namespace Trail.UnitTests.Integrations;

public sealed class ApiKeyFormatTests
{
    [Fact]
    public void Generated_keys_have_the_documented_shape()
    {
        var (key, prefix, hash) = ApiKeyFormat.Generate();

        key.ShouldMatch("^trl_[0-9A-Za-z]{8}_[0-9A-Za-z]{32}$");
        prefix.Length.ShouldBe(ApiKey.PrefixLength);
        key.ShouldStartWith($"trl_{prefix}_");
        hash.ShouldMatch("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Keys_are_unique_and_the_hash_is_of_the_whole_key()
    {
        var keys = Enumerable.Range(0, 50).Select(_ => ApiKeyFormat.Generate()).ToList();

        keys.Select(k => k.Key).Distinct().Count().ShouldBe(50);
        keys.Select(k => k.Hash).Distinct().Count().ShouldBe(50);
        foreach (var (key, _, hash) in keys) ApiKeyFormat.Hash(key).ShouldBe(hash);
    }

    [Fact]
    public void Hash_is_deterministic_sha256_hex()
    {
        // SHA-256("abc")
        ApiKeyFormat.Hash("abc").ShouldBe("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Fact]
    public void Stored_hash_does_not_contain_the_key()
    {
        var (key, prefix, hash) = ApiKeyFormat.Generate();

        hash.ShouldNotContain(key[(ApiKeyFormat.Marker.Length + ApiKey.PrefixLength + 1)..]);
        hash.ShouldNotContain(prefix);
    }

    [Fact]
    public void Parse_accepts_generated_keys_and_returns_the_prefix()
    {
        var (key, prefix, _) = ApiKeyFormat.Generate();

        ApiKeyFormat.TryParse(key, out var parsed).ShouldBeTrue();
        parsed.ShouldBe(prefix);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("trl_")]
    [InlineData("abc_ABCDEFGH_0123456789abcdef0123456789abcdef")]            // wrong marker
    [InlineData("trl_ABCDEFGH_0123456789abcdef0123456789abcde")]             // secret too short
    [InlineData("trl_ABCDEFGH_0123456789abcdef0123456789abcdef0")]           // secret too long
    [InlineData("trl_ABCDEFG_0123456789abcdef0123456789abcdef")]             // prefix too short
    [InlineData("trl_ABCDEFGH-0123456789abcdef0123456789abcdef")]            // wrong separator
    [InlineData("trl_ABCDEFG!_0123456789abcdef0123456789abcdef")]            // bad prefix character
    [InlineData("trl_ABCDEFGH_0123456789abcdef0123456789abcde+")]            // bad secret character
    public void Parse_rejects_malformed_keys(string? key) =>
        ApiKeyFormat.TryParse(key, out _).ShouldBeFalse();

    [Fact]
    public void Matches_compares_the_presented_key_with_the_stored_hash()
    {
        var (key, _, hash) = ApiKeyFormat.Generate();
        var (other, _, _) = ApiKeyFormat.Generate();

        ApiKeyFormat.Matches(key, hash).ShouldBeTrue();
        ApiKeyFormat.Matches(other, hash).ShouldBeFalse();
        ApiKeyFormat.Matches(key, hash[..^1]).ShouldBeFalse(); // different length: no exception
        ApiKeyFormat.Matches(key, "").ShouldBeFalse();
    }

    [Fact]
    public void Usability_depends_on_revocation_and_expiry()
    {
        var now = new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);
        var key = new ApiKey { Name = "k", Prefix = "ABCDEFGH", KeyHash = "x" };

        key.IsUsable(now).ShouldBeTrue();
        key.ExpiresAt = now.AddMinutes(1);
        key.IsUsable(now).ShouldBeTrue();
        key.IsUsable(now.AddMinutes(1)).ShouldBeFalse();
        key.ExpiresAt = null;
        key.RevokedAt = now.AddDays(-1);
        key.IsUsable(now).ShouldBeFalse();
    }

    [Fact]
    public void Site_restriction_applies_only_when_sites_are_listed()
    {
        var site = Guid.CreateVersion7();
        var key = new ApiKey { Name = "k", Prefix = "ABCDEFGH", KeyHash = "x" };

        key.AllowsSite(site).ShouldBeTrue();
        key.SiteIds = [];
        key.AllowsSite(site).ShouldBeTrue();
        key.SiteIds = [Guid.CreateVersion7()];
        key.AllowsSite(site).ShouldBeFalse();
        key.SiteIds = [site];
        key.AllowsSite(site).ShouldBeTrue();
    }
}
