using AliasCockpit.Core.Aliases;
using AliasCockpit.Core.Providers;
using AliasCockpit.Core.Secrets;

namespace AliasCockpit.Infrastructure.Providers;

public abstract class ProviderAdapterBase : IProviderAdapter
{
    protected ProviderAdapterBase(ProviderProfile profile)
    {
        Profile = profile;
    }

    public ProviderProfile Profile { get; }

    public virtual ProviderAliasOperationPlan PlanCreateAlias(ProviderAliasCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var capability = request.PreferRandom
            ? ProviderCapability.AliasCreateRandom
            : ProviderCapability.AliasCreateCustom;
        var warnings = ValidateCreateRequest(request, capability).ToList();

        return new ProviderAliasOperationPlan(
            ProviderTypes.Normalize(Profile.ProviderType),
            capability,
            RequiresNetwork: true,
            Reversible: true,
            EndpointHint: EndpointFor(capability),
            Steps: CreatePlanSteps(capability),
            warnings);
    }

    public virtual ProviderAliasOperationPlan PlanDisableAlias(ProviderAliasReference alias)
    {
        ArgumentNullException.ThrowIfNull(alias);
        return PlanRemoteMutation(alias, ProviderCapability.AliasDisable);
    }

    public virtual ProviderAliasOperationPlan PlanDeleteAlias(ProviderAliasReference alias)
    {
        ArgumentNullException.ThrowIfNull(alias);
        return PlanRemoteMutation(alias, ProviderCapability.AliasDelete);
    }

    public virtual async Task<ProviderCredentialValidationResult> ValidateCredentialsAsync(
        ProviderAccount account,
        ISecretStore secretStore,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(secretStore);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(account.ProviderType, Profile.ProviderType, StringComparison.Ordinal))
        {
            return ProviderCredentialValidationResult.ProviderMismatch();
        }

        var secret = await secretStore.GetSecretAsync(account.SecretRef, cancellationToken);
        return string.IsNullOrWhiteSpace(secret)
            ? ProviderCredentialValidationResult.MissingSecret()
            : ProviderCredentialValidationResult.Ready();
    }

    public virtual async Task<ProviderAliasOperationResult> CreateAliasAsync(
        ProviderAccount account,
        ProviderAliasCreateRequest request,
        ISecretStore secretStore,
        CancellationToken cancellationToken = default)
    {
        var credential = await ValidateCredentialsAsync(account, secretStore, cancellationToken);
        if (!credential.CanAttemptNetwork)
        {
            return ProviderAliasOperationResult.Failure(credential.ErrorCode ?? "auth.failed", []);
        }

        var plan = PlanCreateAlias(request);
        if (!plan.CanExecute)
        {
            return ProviderAliasOperationResult.Failure("validation.failed", plan.Warnings);
        }

        var address = BuildAddress(account, request);
        var snapshot = new ProviderAliasSnapshot(
            $"mock:{ProviderTypes.Normalize(Profile.ProviderType)}:{Guid.NewGuid():N}",
            address,
            AliasStatus.Active,
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            request.RequestedAt);

        return ProviderAliasOperationResult.Success(snapshot, plan.Warnings);
    }

    public virtual async Task<ProviderAliasOperationResult> DisableAliasAsync(
        ProviderAccount account,
        ProviderAliasReference alias,
        ISecretStore secretStore,
        CancellationToken cancellationToken = default)
    {
        var credential = await ValidateCredentialsAsync(account, secretStore, cancellationToken);
        if (!credential.CanAttemptNetwork)
        {
            return ProviderAliasOperationResult.Failure(credential.ErrorCode ?? "auth.failed", []);
        }

        var plan = PlanDisableAlias(alias);
        if (!plan.CanExecute)
        {
            return ProviderAliasOperationResult.Failure("validation.failed", plan.Warnings);
        }

        return ProviderAliasOperationResult.Success(CreateMutationSnapshot(alias, AliasStatus.Disabled), plan.Warnings);
    }

    public virtual async Task<ProviderAliasOperationResult> DeleteAliasAsync(
        ProviderAccount account,
        ProviderAliasReference alias,
        ISecretStore secretStore,
        CancellationToken cancellationToken = default)
    {
        var credential = await ValidateCredentialsAsync(account, secretStore, cancellationToken);
        if (!credential.CanAttemptNetwork)
        {
            return ProviderAliasOperationResult.Failure(credential.ErrorCode ?? "auth.failed", []);
        }

        var plan = PlanDeleteAlias(alias);
        if (!plan.CanExecute)
        {
            return ProviderAliasOperationResult.Failure("validation.failed", plan.Warnings);
        }

        return ProviderAliasOperationResult.Success(CreateMutationSnapshot(alias, AliasStatus.Deleted), plan.Warnings);
    }

    protected abstract string EndpointFor(ProviderCapability capability);

    protected virtual IReadOnlyList<string> CreatePlanSteps(ProviderCapability capability)
    {
        return
        [
            "Resolve provider credential from secret_ref",
            $"Call {EndpointFor(capability)}",
            "Map remote alias snapshot back into local alias metadata",
        ];
    }

    private ProviderAliasOperationPlan PlanRemoteMutation(ProviderAliasReference alias, ProviderCapability capability)
    {
        var warnings = ValidateRemoteMutation(capability).ToList();
        return new ProviderAliasOperationPlan(
            ProviderTypes.Normalize(Profile.ProviderType),
            capability,
            RequiresNetwork: true,
            Reversible: capability is ProviderCapability.AliasDisable,
            EndpointHint: EndpointFor(capability),
            Steps: CreatePlanSteps(capability),
            warnings);
    }

    private IEnumerable<string> ValidateRemoteMutation(ProviderCapability capability)
    {
        if (Profile.SupportFor(capability) is CapabilitySupport.No)
        {
            yield return $"{Profile.DisplayName} does not support {capability}.";
        }
    }

    protected virtual IEnumerable<string> ValidateCreateRequest(
        ProviderAliasCreateRequest request,
        ProviderCapability capability)
    {
        if (Profile.SupportFor(capability) is CapabilitySupport.No)
        {
            yield return $"{Profile.DisplayName} does not support {capability}.";
        }

        if (string.IsNullOrWhiteSpace(request.Domain))
        {
            yield return "Alias domain is required.";
        }

        if (!request.PreferRandom && string.IsNullOrWhiteSpace(request.LocalPart))
        {
            yield return "Custom alias local part is required.";
        }
    }

    protected virtual string BuildAddress(ProviderAccount account, ProviderAliasCreateRequest request)
    {
        var domain = NormalizeDomain(request.Domain);
        if (!request.PreferRandom)
        {
            return $"{NormalizeLocalPart(request.LocalPart ?? "alias")}@{domain}";
        }

        var host = NormalizeLocalPart(string.IsNullOrWhiteSpace(request.Hostname) ? "private" : request.Hostname);
        var suffix = account.Id.ToString("N")[..6];
        return $"{host}-{suffix}@{domain}";
    }

    private static ProviderAliasSnapshot CreateMutationSnapshot(ProviderAliasReference alias, AliasStatus status)
    {
        return new ProviderAliasSnapshot(
            alias.RemoteId,
            alias.Address ?? alias.RemoteId,
            status,
            Description: null,
            alias.RequestedAt);
    }

    protected static string NormalizeDomain(string domain)
    {
        return domain.Trim().TrimStart('@').ToLowerInvariant();
    }

    protected static string NormalizeLocalPart(string value)
    {
        var chars = value.Trim().ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var normalized = new string(chars).Trim('-');
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(normalized) ? "alias" : normalized;
    }
}
