namespace AliasCockpit.Core.Providers;

public enum ProviderAuthState
{
    NotConfigured,
    SecretStored,
    Validated,
    Expired,
    Revoked,
    Error,
}
