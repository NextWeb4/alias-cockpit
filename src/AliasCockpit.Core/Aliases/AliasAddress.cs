using System.Text.RegularExpressions;

namespace AliasCockpit.Core.Aliases;

public sealed partial record AliasAddress(string Address, string LocalPart, string Domain)
{
    public static AliasAddress Parse(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("Alias address is required.", nameof(address));
        }

        var normalized = address.Trim().ToLowerInvariant();
        var at = normalized.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at != normalized.LastIndexOf('@') || at == normalized.Length - 1)
        {
            throw new ArgumentException("Alias address must contain one local part and one domain.", nameof(address));
        }

        var localPart = normalized[..at];
        var domain = normalized[(at + 1)..];
        if (!LocalPartRegex().IsMatch(localPart) || !DomainRegex().IsMatch(domain))
        {
            throw new ArgumentException("Alias address contains unsupported characters.", nameof(address));
        }

        return new AliasAddress(normalized, localPart, domain);
    }

    [GeneratedRegex(@"^[a-z0-9.!#$%&'*+/=?^_`{|}~-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex LocalPartRegex();

    [GeneratedRegex(@"^(?!-)(?:[a-z0-9-]{1,63}\.)+[a-z]{2,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex DomainRegex();
}

