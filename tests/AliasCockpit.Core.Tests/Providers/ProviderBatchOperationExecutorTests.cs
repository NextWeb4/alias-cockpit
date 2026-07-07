using AliasCockpit.Core.Aliases;
using AliasCockpit.Core.Providers;
using AliasCockpit.Core.Secrets;

namespace AliasCockpit.Core.Tests.Providers;

public sealed class ProviderBatchOperationExecutorTests
{
    [Fact]
    public async Task DeleteWithoutConfirmationIsRejectedBeforeAdapterCall()
    {
        var adapter = new ExecutingAdapter();
        var plan = CreatePlan(adapter, ProviderBatchOperationKind.DeleteAliases);
        var executor = new ProviderBatchOperationExecutor();

        var result = await executor.ExecuteAsync(
            adapter,
            ProviderAccount.Create(ProviderTypes.SimpleLogin, "SimpleLogin", DateTimeOffset.UtcNow),
            plan,
            new InMemorySecretStore(),
            explicitlyConfirmed: false);

        Assert.True(result.Rejected);
        Assert.Equal("confirmation.required", result.RejectionCode);
        Assert.Equal(0, adapter.DeleteCalls);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task DeleteWithConfirmationExecutesAllItems()
    {
        var adapter = new ExecutingAdapter();
        var plan = CreatePlan(adapter, ProviderBatchOperationKind.DeleteAliases);
        var executor = new ProviderBatchOperationExecutor();

        var result = await executor.ExecuteAsync(
            adapter,
            ProviderAccount.Create(ProviderTypes.SimpleLogin, "SimpleLogin", DateTimeOffset.UtcNow),
            plan,
            new InMemorySecretStore(),
            explicitlyConfirmed: true);

        Assert.False(result.Rejected);
        Assert.Equal(2, result.SucceededCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(2, adapter.DeleteCalls);
        Assert.All(result.Items, item => Assert.Equal(AliasStatus.Deleted, item.Snapshot?.Status));
    }

    [Fact]
    public async Task DisableExecutesWithoutExplicitConfirmation()
    {
        var adapter = new ExecutingAdapter();
        var plan = CreatePlan(adapter, ProviderBatchOperationKind.DisableAliases);
        var executor = new ProviderBatchOperationExecutor();

        var result = await executor.ExecuteAsync(
            adapter,
            ProviderAccount.Create(ProviderTypes.AddyIo, "addy.io", DateTimeOffset.UtcNow),
            plan,
            new InMemorySecretStore(),
            explicitlyConfirmed: false);

        Assert.False(result.Rejected);
        Assert.Equal(2, result.SucceededCount);
        Assert.Equal(2, adapter.DisableCalls);
        Assert.All(result.Items, item => Assert.Equal(AliasStatus.Disabled, item.Snapshot?.Status));
    }

    [Fact]
    public async Task BlockedPlanIsRejectedBeforeAdapterCall()
    {
        var adapter = new ExecutingAdapter();
        var planner = new ProviderBatchOperationPlanner();
        var plan = planner.Plan(
            adapter,
            ProviderBatchOperationKind.DisableAliases,
            [new ProviderAliasReference("remote-1", null, DateTimeOffset.UtcNow)]);
        var executor = new ProviderBatchOperationExecutor();

        var result = await executor.ExecuteAsync(
            adapter,
            ProviderAccount.Create(ProviderTypes.AddyIo, "addy.io", DateTimeOffset.UtcNow),
            plan,
            new InMemorySecretStore(),
            explicitlyConfirmed: true);

        Assert.True(result.Rejected);
        Assert.Equal("plan.blocked", result.RejectionCode);
        Assert.Equal(0, adapter.DisableCalls);
    }

    private static ProviderBatchOperationPlan CreatePlan(
        IProviderAdapter adapter,
        ProviderBatchOperationKind kind)
    {
        return new ProviderBatchOperationPlanner().Plan(
            adapter,
            kind,
            [
                new ProviderAliasReference("remote-1", "one@example.test", DateTimeOffset.UtcNow),
                new ProviderAliasReference("remote-2", "two@example.test", DateTimeOffset.UtcNow),
            ]);
    }

    private sealed class ExecutingAdapter : IProviderAdapter
    {
        public ProviderProfile Profile { get; } = new(
            ProviderTypes.SimpleLogin,
            "SimpleLogin",
            ProviderSecurityState.Healthy,
            [
                Capability(ProviderCapability.AliasDisable, reversible: true),
                Capability(ProviderCapability.AliasDelete, reversible: false),
            ]);

        public int DisableCalls { get; private set; }

        public int DeleteCalls { get; private set; }

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
            DisableCalls++;
            return Task.FromResult(ProviderAliasOperationResult.Success(
                new ProviderAliasSnapshot(alias.RemoteId, alias.Address!, AliasStatus.Disabled, null, alias.RequestedAt),
                []));
        }

        public Task<ProviderAliasOperationResult> DeleteAliasAsync(
            ProviderAccount account,
            ProviderAliasReference alias,
            ISecretStore secretStore,
            CancellationToken cancellationToken = default)
        {
            DeleteCalls++;
            return Task.FromResult(ProviderAliasOperationResult.Success(
                new ProviderAliasSnapshot(alias.RemoteId, alias.Address!, AliasStatus.Deleted, null, alias.RequestedAt),
                []));
        }

        private static ProviderAliasOperationPlan Plan(ProviderCapability capability, bool reversible)
        {
            return new ProviderAliasOperationPlan(
                ProviderTypes.SimpleLogin,
                capability,
                RequiresNetwork: true,
                reversible,
                EndpointHint: "test",
                Steps: ["Resolve provider credential", "Call provider"],
                Warnings: []);
        }

        private static ProviderCapabilityDescriptor Capability(ProviderCapability capability, bool reversible)
        {
            return new ProviderCapabilityDescriptor(
                capability,
                CapabilitySupport.Yes,
                OfflineAvailable: false,
                reversible,
                ScopeRequired: "api_key",
                Notes: null);
        }
    }

    private sealed class InMemorySecretStore : ISecretStore
    {
        public Task SetSecretAsync(string key, string secret, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>("secret");
        }

        public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
