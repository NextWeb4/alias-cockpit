using AliasCockpit.Core.Providers;
using AliasCockpit.Core.Secrets;

namespace AliasCockpit.Core.Tests.Providers;

public sealed class ProviderBatchOperationPlannerTests
{
    [Fact]
    public void DeletePlanRequiresExplicitConfirmation()
    {
        var planner = new ProviderBatchOperationPlanner();
        var adapter = new PlanningAdapter(
            ProviderTypes.SimpleLogin,
            disableSupport: CapabilitySupport.Yes,
            deleteSupport: CapabilitySupport.Yes);

        var plan = planner.Plan(
            adapter,
            ProviderBatchOperationKind.DeleteAliases,
            [new ProviderAliasReference("remote-1", "alias@example.test", DateTimeOffset.UtcNow)]);

        Assert.Equal(ProviderBatchOperationKind.DeleteAliases, plan.Kind);
        Assert.True(plan.RequiresNetwork);
        Assert.True(plan.RequiresExplicitConfirmation);
        Assert.False(plan.Reversible);
        Assert.Equal(1, plan.TotalCount);
        Assert.Equal(1, plan.ExecutableCount);
        Assert.False(plan.CanExecute);
        Assert.Contains(plan.Warnings, warning => warning.Contains("destructive", StringComparison.Ordinal));
    }

    [Fact]
    public void DisablePlanCanExecuteWhenEveryItemIsSafe()
    {
        var planner = new ProviderBatchOperationPlanner();
        var adapter = new PlanningAdapter(
            ProviderTypes.AddyIo,
            disableSupport: CapabilitySupport.Yes,
            deleteSupport: CapabilitySupport.Yes);

        var plan = planner.Plan(
            adapter,
            ProviderBatchOperationKind.DisableAliases,
            [
                new ProviderAliasReference("remote-1", "one@example.test", DateTimeOffset.UtcNow),
                new ProviderAliasReference("remote-2", "two@example.test", DateTimeOffset.UtcNow),
            ]);

        Assert.False(plan.RequiresExplicitConfirmation);
        Assert.True(plan.Reversible);
        Assert.True(plan.CanExecute);
        Assert.Equal(2, plan.ExecutableCount);
        Assert.Equal(0, plan.BlockedCount);
    }

    [Fact]
    public void MissingAddressBlocksItem()
    {
        var planner = new ProviderBatchOperationPlanner();
        var adapter = new PlanningAdapter(
            ProviderTypes.AddyIo,
            disableSupport: CapabilitySupport.Yes,
            deleteSupport: CapabilitySupport.Yes);

        var plan = planner.Plan(
            adapter,
            ProviderBatchOperationKind.DisableAliases,
            [new ProviderAliasReference("remote-1", null, DateTimeOffset.UtcNow)]);

        Assert.False(plan.CanExecute);
        Assert.Equal(0, plan.ExecutableCount);
        Assert.Equal(1, plan.BlockedCount);
        Assert.Contains(plan.Items[0].Warnings, warning => warning.Contains("remote id", StringComparison.Ordinal));
    }

    [Fact]
    public void UnsupportedProviderCapabilityBlocksItem()
    {
        var planner = new ProviderBatchOperationPlanner();
        var adapter = new PlanningAdapter(
            ProviderTypes.Manual,
            disableSupport: CapabilitySupport.No,
            deleteSupport: CapabilitySupport.No);

        var plan = planner.Plan(
            adapter,
            ProviderBatchOperationKind.DisableAliases,
            [new ProviderAliasReference("remote-1", "alias@example.test", DateTimeOffset.UtcNow)]);

        Assert.False(plan.CanExecute);
        Assert.Equal(1, plan.BlockedCount);
        Assert.Contains(plan.Items[0].Warnings, warning => warning.Contains("does not support", StringComparison.Ordinal));
    }

    [Fact]
    public void EmptySelectionProducesPlanWarning()
    {
        var planner = new ProviderBatchOperationPlanner();
        var adapter = new PlanningAdapter(
            ProviderTypes.SimpleLogin,
            disableSupport: CapabilitySupport.Yes,
            deleteSupport: CapabilitySupport.Yes);

        var plan = planner.Plan(adapter, ProviderBatchOperationKind.DisableAliases, []);

        Assert.False(plan.CanExecute);
        Assert.Equal(0, plan.TotalCount);
        Assert.Contains(plan.Warnings, warning => warning.Contains("No aliases", StringComparison.Ordinal));
    }

    private sealed class PlanningAdapter : IProviderAdapter
    {
        public PlanningAdapter(string providerType, CapabilitySupport disableSupport, CapabilitySupport deleteSupport)
        {
            Profile = new ProviderProfile(
                ProviderTypes.Normalize(providerType),
                providerType,
                ProviderSecurityState.Healthy,
                [
                    Capability(ProviderCapability.AliasDisable, disableSupport, reversible: true),
                    Capability(ProviderCapability.AliasDelete, deleteSupport, reversible: false),
                ]);
        }

        public ProviderProfile Profile { get; }

        public ProviderAliasOperationPlan PlanCreateAlias(ProviderAliasCreateRequest request)
        {
            return Plan(ProviderCapability.AliasCreateRandom, reversible: true);
        }

        public ProviderAliasOperationPlan PlanDisableAlias(ProviderAliasReference alias)
        {
            return Plan(ProviderCapability.AliasDisable, reversible: true);
        }

        public ProviderAliasOperationPlan PlanDeleteAlias(ProviderAliasReference alias)
        {
            return Plan(ProviderCapability.AliasDelete, reversible: false);
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

        private ProviderAliasOperationPlan Plan(ProviderCapability capability, bool reversible)
        {
            var support = Profile.SupportFor(capability);
            var warnings = support is CapabilitySupport.No
                ? new[] { $"{Profile.DisplayName} does not support {capability}." }
                : Array.Empty<string>();

            return new ProviderAliasOperationPlan(
                Profile.ProviderType,
                capability,
                RequiresNetwork: true,
                Reversible: reversible,
                EndpointHint: "test",
                Steps: ["Resolve provider credential", "Call provider"],
                warnings);
        }

        private static ProviderCapabilityDescriptor Capability(
            ProviderCapability capability,
            CapabilitySupport support,
            bool reversible)
        {
            return new ProviderCapabilityDescriptor(
                capability,
                support,
                OfflineAvailable: false,
                reversible,
                ScopeRequired: "api_key",
                Notes: null);
        }
    }
}
