using AliasCockpit.Core.Secrets;

namespace AliasCockpit.Core.Providers;

public sealed record ProviderAccount
{
    public ProviderAccount(
        Guid id,
        string providerType,
        string displayName,
        string secretRef,
        ProviderAuthState authState,
        ProviderSecurityState securityState,
        DateTimeOffset? lastSyncAt,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Provider account id is required.", nameof(id));
        }

        SecretKey.Validate(secretRef);

        Id = id;
        ProviderType = ProviderTypes.Normalize(providerType);
        DisplayName = NormalizeDisplayName(displayName);
        SecretRef = secretRef;
        AuthState = authState;
        SecurityState = securityState;
        LastSyncAt = lastSyncAt;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; init; }

    public string ProviderType { get; init; }

    public string DisplayName { get; init; }

    public string SecretRef { get; init; }

    public ProviderAuthState AuthState { get; init; }

    public ProviderSecurityState SecurityState { get; init; }

    public DateTimeOffset? LastSyncAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public static ProviderAccount Create(string providerType, string displayName, DateTimeOffset now)
    {
        var id = Guid.NewGuid();
        return new ProviderAccount(
            id,
            providerType,
            displayName,
            SecretKey.ForProviderToken(id),
            ProviderAuthState.NotConfigured,
            ProviderSecurityState.Healthy,
            lastSyncAt: null,
            createdAt: now,
            updatedAt: now);
    }

    public ProviderAccount MarkSecretStored(DateTimeOffset now)
    {
        return this with
        {
            AuthState = ProviderAuthState.SecretStored,
            UpdatedAt = now,
        };
    }

    public ProviderAccount MarkValidated(DateTimeOffset now)
    {
        return this with
        {
            AuthState = ProviderAuthState.Validated,
            LastSyncAt = now,
            SecurityState = ProviderSecurityState.Healthy,
            UpdatedAt = now,
        };
    }

    public ProviderAccount MarkAuthFailure(ProviderAuthState authState, DateTimeOffset now)
    {
        if (authState is ProviderAuthState.NotConfigured or ProviderAuthState.SecretStored or ProviderAuthState.Validated)
        {
            throw new ArgumentException("Auth failure state must be Expired, Revoked, or Error.", nameof(authState));
        }

        return this with
        {
            AuthState = authState,
            UpdatedAt = now,
        };
    }

    private static string NormalizeDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Provider display name is required.", nameof(displayName));
        }

        return displayName.Trim();
    }
}
