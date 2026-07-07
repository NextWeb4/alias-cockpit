namespace AliasCockpit.Core.Providers;

public static class ProviderTypes
{
    public const string Manual = "manual";
    public const string SimpleLogin = "simplelogin";
    public const string AddyIo = "addyio";

    public static string Normalize(string providerType)
    {
        if (string.IsNullOrWhiteSpace(providerType))
        {
            throw new ArgumentException("Provider type is required.", nameof(providerType));
        }

        return providerType.Trim().ToLowerInvariant();
    }
}
