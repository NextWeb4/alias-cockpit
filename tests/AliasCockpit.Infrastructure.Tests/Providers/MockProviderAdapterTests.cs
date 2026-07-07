using AliasCockpit.Core.Providers;
using AliasCockpit.Core.Secrets;
using AliasCockpit.Infrastructure.Providers;

namespace AliasCockpit.Infrastructure.Tests.Providers;

public sealed class MockProviderAdapterTests
{
    [Fact]
    public void SimpleLoginCustomAliasPlanUsesOfficialWorkflowShape()
    {
        var adapter = new SimpleLoginMockProviderAdapter();
        var request = ProviderAliasCreateRequest.Custom(
            "shop.example",
            "shop",
            "alias.example",
            "shopping",
            DateTimeOffset.Parse("2026-07-05T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

        var plan = adapter.PlanCreateAlias(request);

        Assert.Equal(ProviderTypes.SimpleLogin, adapter.Profile.ProviderType);
        Assert.Equal(ProviderCapability.AliasCreateCustom, plan.CapabilityRequired);
        Assert.True(plan.RequiresNetwork);
        Assert.Contains("/api/v3/alias/custom/new", plan.EndpointHint, StringComparison.Ordinal);
        Assert.Empty(plan.Warnings);
    }

    [Fact]
    public void AddyIoProfileDeclaresAdvancedProviderCapabilities()
    {
        var profile = new AddyIoMockProviderAdapter().Profile;

        Assert.Equal(CapabilitySupport.Yes, profile.SupportFor(ProviderCapability.AliasRestore));
        Assert.Equal(CapabilitySupport.Yes, profile.SupportFor(ProviderCapability.RecipientManage));
        Assert.Equal(CapabilitySupport.Yes, profile.SupportFor(ProviderCapability.RulesManage));
        Assert.Equal(CapabilitySupport.Yes, profile.SupportFor(ProviderCapability.WebhookReceive));
        Assert.Equal(CapabilitySupport.Partial, profile.SupportFor(ProviderCapability.SendFromAlias));
    }

    [Fact]
    public async Task MissingSecretPreventsMockProviderExecution()
    {
        var adapter = new SimpleLoginMockProviderAdapter();
        var account = ProviderAccount.Create(ProviderTypes.SimpleLogin, "SimpleLogin", DateTimeOffset.UtcNow);
        var request = ProviderAliasCreateRequest.Random(
            "github.com",
            "alias.example",
            null,
            DateTimeOffset.UtcNow);

        var result = await adapter.CreateAliasAsync(account, request, new InMemorySecretStore());

        Assert.False(result.Succeeded);
        Assert.Equal("auth.missing_secret", result.ErrorCode);
    }

    [Fact]
    public async Task StoredSecretAllowsMockProviderAliasCreationWithoutNetwork()
    {
        var adapter = new AddyIoMockProviderAdapter();
        var now = DateTimeOffset.Parse("2026-07-05T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var account = ProviderAccount.Create(ProviderTypes.AddyIo, "addy.io", now).MarkSecretStored(now);
        var secrets = new InMemorySecretStore();
        await secrets.SetSecretAsync(account.SecretRef, "credential-value");
        var request = ProviderAliasCreateRequest.Custom("shop.example", "shop", "alias.example", "shopping", now);

        var result = await adapter.CreateAliasAsync(account, request, secrets);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Alias);
        Assert.Equal("shop@alias.example", result.Alias.Address);
        Assert.Equal("shopping", result.Alias.Description);
        Assert.StartsWith("mock:addyio:", result.Alias.RemoteId, StringComparison.Ordinal);
    }

    private sealed class InMemorySecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);

        public Task SetSecretAsync(string key, string secret, CancellationToken cancellationToken = default)
        {
            SecretKey.Validate(key);
            _secrets[key] = secret;
            return Task.CompletedTask;
        }

        public Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            SecretKey.Validate(key);
            return Task.FromResult(_secrets.TryGetValue(key, out var secret) ? secret : null);
        }

        public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            SecretKey.Validate(key);
            _secrets.Remove(key);
            return Task.CompletedTask;
        }
    }
}
