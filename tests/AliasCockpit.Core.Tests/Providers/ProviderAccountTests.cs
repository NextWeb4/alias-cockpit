using AliasCockpit.Core.Providers;
using AliasCockpit.Core.Secrets;

namespace AliasCockpit.Core.Tests.Providers;

public sealed class ProviderAccountTests
{
    [Fact]
    public void CreateNormalizesProviderTypeAndBuildsSecretRef()
    {
        var now = DateTimeOffset.Parse("2026-07-05T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

        var account = ProviderAccount.Create(" SimpleLogin ", " Personal aliases ", now);

        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.Equal(ProviderTypes.SimpleLogin, account.ProviderType);
        Assert.Equal("Personal aliases", account.DisplayName);
        Assert.Equal(SecretKey.ForProviderToken(account.Id), account.SecretRef);
        Assert.Equal(ProviderAuthState.NotConfigured, account.AuthState);
        Assert.Equal(ProviderSecurityState.Healthy, account.SecurityState);
        Assert.Null(account.LastSyncAt);
        Assert.Equal(now, account.CreatedAt);
        Assert.Equal(now, account.UpdatedAt);
    }

    [Fact]
    public void InvalidSecretRefIsRejected()
    {
        var now = DateTimeOffset.UtcNow;

        var exception = Assert.Throws<ArgumentException>(() => new ProviderAccount(
            Guid.NewGuid(),
            ProviderTypes.AddyIo,
            "addy.io",
            "secret ref with spaces",
            ProviderAuthState.SecretStored,
            ProviderSecurityState.Healthy,
            lastSyncAt: null,
            createdAt: now,
            updatedAt: now));

        Assert.Contains("unsupported characters", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthStateTransitionsKeepSecretRefStable()
    {
        var now = DateTimeOffset.Parse("2026-07-05T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var account = ProviderAccount.Create(ProviderTypes.AddyIo, "addy.io", now);

        var stored = account.MarkSecretStored(now.AddMinutes(1));
        var validated = stored.MarkValidated(now.AddMinutes(2));

        Assert.Equal(account.SecretRef, stored.SecretRef);
        Assert.Equal(account.SecretRef, validated.SecretRef);
        Assert.Equal(ProviderAuthState.SecretStored, stored.AuthState);
        Assert.Equal(ProviderAuthState.Validated, validated.AuthState);
        Assert.Equal(now.AddMinutes(2), validated.LastSyncAt);
    }
}
