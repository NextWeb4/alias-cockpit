using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AliasCockpit.Core.Tools;

public sealed partial class EmailAliasExpander
{
    public const int MinCount = 1;
    public const int MaxCount = 256;

    private static readonly string[] RandomTagPool =
    [
        "login", "signup", "verify", "otp", "order", "pay", "invoice", "promo",
        "coupon", "notice", "alert", "trial", "support", "billing", "security", "shipping",
        "return", "account", "profile", "team", "admin", "dev", "qa", "beta",
        "feedback", "download", "upload", "report", "backup", "renew", "cancel", "welcome",
        "invite", "reset", "recovery", "archive", "notify", "receipt", "sale", "event",
    ];

    private static readonly HashSet<string> OutlookLikeDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "outlook.com",
        "hotmail.com",
        "live.com",
        "msn.com",
    };

    public EmailAliasExpansionResult Expand(EmailAliasExpansionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var address = AnalyzeAddress(request.Address);
        var count = Math.Clamp(request.Count, MinCount, MaxCount);
        if (!address.IsValid)
        {
            return EmailAliasExpansionResult.Invalid(
                request.Address?.Trim() ?? string.Empty,
                address.LocalPart,
                address.Domain,
                request.Address?.Trim().Length > 0 ? "请输入 Gmail、Googlemail、Outlook、Hotmail、Live 或 MSN 邮箱。" : string.Empty);
        }

        var dotLocalParts = request.UseDotAliases && address.SupportsDotAliases
            ? GenerateDotLocalParts(address.LocalPart, count)
            : [address.LocalPart];
        var baseAddress = $"{address.LocalPart}@{address.Domain}";
        var dotAliases = request.UseDotAliases && address.SupportsDotAliases
            ? Distinct(dotLocalParts.Select(localPart => $"{localPart}@{address.Domain}")).Take(count).ToArray()
            : [];

        var tags = NormalizeTags(request.Tags);
        var plusBaseCount = Math.Max(1, (int)Math.Ceiling(count / (double)Math.Max(1, tags.Count)));
        var plusLocalParts = request.UseDotAliases && address.SupportsDotAliases
            ? dotLocalParts
            : [address.LocalPart];
        var plusAliases = request.UsePlusAliases && address.SupportsPlusAliases
            ? Distinct(plusLocalParts
                .Take(plusBaseCount)
                .SelectMany(localPart => tags.Select(tag => $"{localPart}+{tag}@{address.Domain}")))
                .Take(count)
                .ToArray()
            : [];

        var allAliases = Distinct([baseAddress, .. Interleave(dotAliases, plusAliases)])
            .ToArray();

        return new EmailAliasExpansionResult(
            true,
            string.Empty,
            address.LocalPart,
            address.Domain,
            baseAddress,
            address.SupportsDotAliases,
            address.SupportsPlusAliases,
            allAliases,
            dotAliases,
            plusAliases);
    }

    public static string DefaultTags()
    {
        return "login,signup,verify,order,pay,invoice,promo,notice,trial,support,billing,security,shipping,return,newsletter,account";
    }

    public static string RandomTags(int count = 16)
    {
        count = Math.Clamp(count, 1, RandomTagPool.Length);
        var values = RandomTagPool.ToArray();
        for (var index = values.Length - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }

        return string.Join(",", values.Take(count));
    }

    private static EmailAddressAnalysis AnalyzeAddress(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        var at = trimmed.LastIndexOf('@');
        if (at <= 0 || at == trimmed.Length - 1 || !EmailShapeRegex().IsMatch(trimmed))
        {
            return new EmailAddressAnalysis(false, string.Empty, string.Empty, false, false);
        }

        var rawLocalPart = trimmed[..at];
        var rawDomain = trimmed[(at + 1)..].ToLowerInvariant();
        var isGmail = rawDomain is "gmail.com" or "googlemail.com";
        var isOutlookLike = OutlookLikeDomains.Contains(rawDomain);
        var domain = isGmail ? "gmail.com" : rawDomain;
        var localPart = rawLocalPart.Split('+')[0].ToLowerInvariant();
        if (isGmail)
        {
            localPart = localPart.Replace(".", string.Empty, StringComparison.Ordinal);
        }

        var localPartValid = isGmail
            ? GmailLocalPartRegex().IsMatch(localPart)
            : OutlookLocalPartRegex().IsMatch(localPart);

        return new EmailAddressAnalysis(
            localPartValid && (isGmail || isOutlookLike),
            localPart,
            domain,
            isGmail,
            isGmail || isOutlookLike);
    }

    private static IReadOnlyList<string> NormalizeTags(string tags)
    {
        return tags
            .Split(['\r', '\n', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => InvalidTagCharacterRegex().Replace(tag.ToLowerInvariant(), string.Empty))
            .Where(tag => tag.Length > 0)
            .ToArray();
    }

    private static IReadOnlyList<string> GenerateDotLocalParts(string localPart, int count)
    {
        if (localPart.Length <= 1)
        {
            return [localPart];
        }

        var possibleDotSlots = Math.Min(localPart.Length - 1, 20);
        var variantCount = Math.Min(count, 1 << possibleDotSlots);
        var variants = new List<string>(variantCount);
        for (var mask = 0; mask < variantCount; mask++)
        {
            variants.Add(ApplyDotMask(localPart, mask));
        }

        return variants;
    }

    private static string ApplyDotMask(string localPart, int mask)
    {
        var builder = new StringBuilder(localPart.Length * 2);
        builder.Append(localPart[0]);
        for (var index = 1; index < localPart.Length; index++)
        {
            if ((mask & (1 << (index - 1))) != 0)
            {
                builder.Append('.');
            }

            builder.Append(localPart[index]);
        }

        return builder.ToString();
    }

    private static IEnumerable<string> Interleave(IReadOnlyList<string> first, IReadOnlyList<string> second)
    {
        var max = Math.Max(first.Count, second.Count);
        for (var index = 0; index < max; index++)
        {
            if (index < first.Count)
            {
                yield return first[index];
            }

            if (index < second.Count)
            {
                yield return second[index];
            }
        }
    }

    private static IEnumerable<string> Distinct(IEnumerable<string> values)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (seen.Add(value))
            {
                yield return value;
            }
        }
    }

    private sealed record EmailAddressAnalysis(
        bool IsValid,
        string LocalPart,
        string Domain,
        bool SupportsDotAliases,
        bool SupportsPlusAliases);

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailShapeRegex();

    [GeneratedRegex("^[a-z0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex GmailLocalPartRegex();

    [GeneratedRegex(@"^[^\s@+]+$", RegexOptions.CultureInvariant)]
    private static partial Regex OutlookLocalPartRegex();

    [GeneratedRegex("[^a-z0-9._-]", RegexOptions.CultureInvariant)]
    private static partial Regex InvalidTagCharacterRegex();
}

public sealed record EmailAliasExpansionRequest(string? Address)
{
    public string Tags { get; init; } = string.Empty;

    public int Count { get; init; } = 32;

    public bool UseDotAliases { get; init; } = true;

    public bool UsePlusAliases { get; init; } = true;
}

public sealed record EmailAliasExpansionResult(
    bool IsValid,
    string ValidationMessage,
    string LocalPart,
    string Domain,
    string CanonicalAddress,
    bool SupportsDotAliases,
    bool SupportsPlusAliases,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> DotAliases,
    IReadOnlyList<string> PlusAliases)
{
    public static EmailAliasExpansionResult Invalid(
        string canonicalAddress,
        string localPart,
        string domain,
        string validationMessage)
    {
        return new EmailAliasExpansionResult(
            false,
            validationMessage,
            localPart,
            domain,
            canonicalAddress,
            false,
            false,
            [],
            [],
            []);
    }
}
