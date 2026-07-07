using AliasCockpit.Core.Secrets;

namespace AliasCockpit.Core.Tests.Secrets;

public sealed class SecretKeyTests
{
    [Fact]
    public void ProviderTokenKeyUsesStablePrefixAndGuid()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        Assert.Equal("provider-token/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", SecretKey.ForProviderToken(id));
    }

    [Theory]
    [InlineData("provider-token/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")]
    [InlineData("sync/key-01")]
    public void ValidKeysAreAccepted(string key)
    {
        SecretKey.Validate(key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../escape")]
    [InlineData("Bad/Upper")]
    [InlineData("token with spaces")]
    public void InvalidKeysAreRejected(string key)
    {
        Assert.Throws<ArgumentException>(() => SecretKey.Validate(key));
    }
}

