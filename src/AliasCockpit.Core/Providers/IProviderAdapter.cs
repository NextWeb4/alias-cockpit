using AliasCockpit.Core.Secrets;

namespace AliasCockpit.Core.Providers;

public interface IProviderAdapter
{
    ProviderProfile Profile { get; }

    ProviderAliasOperationPlan PlanCreateAlias(ProviderAliasCreateRequest request);

    ProviderAliasOperationPlan PlanDisableAlias(ProviderAliasReference alias);

    ProviderAliasOperationPlan PlanDeleteAlias(ProviderAliasReference alias);

    Task<ProviderCredentialValidationResult> ValidateCredentialsAsync(
        ProviderAccount account,
        ISecretStore secretStore,
        CancellationToken cancellationToken = default);

    Task<ProviderAliasOperationResult> CreateAliasAsync(
        ProviderAccount account,
        ProviderAliasCreateRequest request,
        ISecretStore secretStore,
        CancellationToken cancellationToken = default);

    Task<ProviderAliasOperationResult> DisableAliasAsync(
        ProviderAccount account,
        ProviderAliasReference alias,
        ISecretStore secretStore,
        CancellationToken cancellationToken = default);

    Task<ProviderAliasOperationResult> DeleteAliasAsync(
        ProviderAccount account,
        ProviderAliasReference alias,
        ISecretStore secretStore,
        CancellationToken cancellationToken = default);
}
