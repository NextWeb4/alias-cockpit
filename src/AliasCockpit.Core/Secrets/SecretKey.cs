using System.Text.RegularExpressions;

namespace AliasCockpit.Core.Secrets;

public static partial class SecretKey
{
    public static string ForProviderToken(Guid providerAccountId)
    {
        return $"provider-token/{providerAccountId:D}";
    }

    public static void Validate(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Secret key is required.", nameof(key));
        }

        if (key.Length > 180 || !SecretKeyRegex().IsMatch(key))
        {
            throw new ArgumentException("Secret key contains unsupported characters.", nameof(key));
        }
    }

    [GeneratedRegex(@"^[a-z0-9][a-z0-9/_\-.]{0,179}$", RegexOptions.CultureInvariant)]
    private static partial Regex SecretKeyRegex();
}

