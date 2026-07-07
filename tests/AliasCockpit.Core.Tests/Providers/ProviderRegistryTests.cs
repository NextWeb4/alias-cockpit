using AliasCockpit.Core.Providers;
using AliasCockpit.Core.Secrets;

namespace AliasCockpit.Core.Tests.Providers;

public sealed class ProviderRegistryTests
{
    [Fact]
    public void DuplicateProviderTypeIsRejected()
    {
        var adapters = new[]
        {
            new NoopAdapter(ProviderTypes.SimpleLogin),
            new NoopAdapter(" SIMPLELOGIN "),
        };

        Assert.Throws<ArgumentException>(() => new ProviderRegistry(adapters));
    }

    [Fact]
    public void AdapterCanBeResolvedByNormalizedProviderType()
    {
        var adapter = new NoopAdapter(ProviderTypes.AddyIo);
        var registry = new ProviderRegistry([adapter]);

        Assert.True(registry.TryGet(" ADDYIO ", out var resolved));
        Assert.Same(adapter, resolved);
        Assert.Same(adapter, registry.GetRequired(ProviderTypes.AddyIo));
    }

    private sealed class NoopAdapter(string providerType) : IProviderAdapter
    {
        public ProviderProfile Profile { get; } = new(
            ProviderTypes.Normalize(providerType),
            providerType,
            ProviderSecurityState.Healthy,
            []);

        public ProviderAliasOperationPlan PlanCreateAlias(ProviderAliasCreateRequest request)
        {
            return new ProviderAliasOperationPlan(
                Profile.ProviderType,
                ProviderCapability.AliasCreateRandom,
                RequiresNetwork: false,
                Reversible: true,
                EndpointHint: "noop",
                Steps: [],
                Warnings: []);
        }

        public Task<ProviderCredentialValidationResult> ValidateCredentialsAsync(
            ProviderAccount account,
            ISecretStore secretStore,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ProviderCredentialValidationResult.Ready());
        }

        public Task<ProviderAliasOperationResult> CreateAliasAsync(
            ProviderAccount account,
            ProviderAliasCreateRequest request,
            ISecretStore secretStore,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ProviderAliasOperationResult.Failure("noop", []));
        }

        public ProviderAliasOperationPlan PlanDisableAlias(ProviderAliasReference alias)
        {
            return new ProviderAliasOperationPlan(
                Profile.ProviderType,
                ProviderCapability.AliasDisable,
                RequiresNetwork: false,
                Reversible: true,
                EndpointHint: "noop",
                Steps: [],
                Warnings: []);
        }

        public ProviderAliasOperationPlan PlanDeleteAlias(ProviderAliasReference alias)
        {
            return new ProviderAliasOperationPlan(
                Profile.ProviderType,
                ProviderCapability.AliasDelete,
                RequiresNetwork: false,
                Reversible: false,
                EndpointHint: "noop",
                Steps: [],
                Warnings: []);
        }

        public Task<ProviderAliasOperationResult> DisableAliasAsync(
            ProviderAccount account,
            ProviderAliasReference alias,
            ISecretStore secretStore,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ProviderAliasOperationResult.Failure("noop", []));
        }

        public Task<ProviderAliasOperationResult> DeleteAliasAsync(
            ProviderAccount account,
            ProviderAliasReference alias,
            ISecretStore secretStore,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ProviderAliasOperationResult.Failure("noop", []));
        }
    }
}
