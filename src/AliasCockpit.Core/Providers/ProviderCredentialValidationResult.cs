namespace AliasCockpit.Core.Providers;

public sealed record ProviderCredentialValidationResult(
    bool IsConfigured,
    bool CanAttemptNetwork,
    string? ErrorCode)
{
    public static ProviderCredentialValidationResult Ready()
    {
        return new ProviderCredentialValidationResult(true, true, null);
    }

    public static ProviderCredentialValidationResult MissingSecret()
    {
        return new ProviderCredentialValidationResult(false, false, "auth.missing_secret");
    }

    public static ProviderCredentialValidationResult ProviderMismatch()
    {
        return new ProviderCredentialValidationResult(false, false, "auth.provider_mismatch");
    }

    public static ProviderCredentialValidationResult Failure(string errorCode, bool canRetry)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            throw new ArgumentException("Error code is required.", nameof(errorCode));
        }

        return new ProviderCredentialValidationResult(false, canRetry, errorCode);
    }
}
