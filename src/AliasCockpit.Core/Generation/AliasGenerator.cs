using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AliasCockpit.Core.Generation;

public sealed partial class AliasGenerator
{
    private const int MaxBatchSize = 10_000;
    private const int MaxLocalPartLength = 64;
    private const string Base32Alphabet = "abcdefghijkmnpqrstuvwxyz23456789";

    private static readonly string[] Adjectives =
    [
        "amber", "brisk", "calm", "cedar", "clear", "cobalt", "coral", "cosmic",
        "crisp", "dawn", "ember", "fable", "frost", "gentle", "harbor", "hazel",
        "ivory", "jade", "linen", "lunar", "maple", "meadow", "mist", "north",
        "onyx", "quiet", "river", "sage", "solar", "tidal", "velvet", "wild"
    ];

    private static readonly string[] Nouns =
    [
        "anchor", "atlas", "beacon", "branch", "brook", "canvas", "cipher", "cloud",
        "comet", "copper", "delta", "ember", "field", "forge", "garden", "glade",
        "harbor", "kernel", "lantern", "ledger", "matrix", "needle", "orbit", "parcel",
        "quartz", "signal", "silver", "summit", "thread", "valley", "vector", "window"
    ];

    public IReadOnlyList<AliasCandidate> Generate(AliasGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var domain = NormalizeDomain(request.Domain);
        ValidateRequest(request);

        var candidates = new List<AliasCandidate>(request.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (candidates.Count < request.Count)
        {
            var candidate = request.Strategy switch
            {
                AliasGenerationStrategy.StrongRandom => GenerateStrongRandom(domain, request),
                AliasGenerationStrategy.ReadableRandom => GenerateReadable(domain, request),
                AliasGenerationStrategy.SiteAware => GenerateSiteAware(domain, request),
                AliasGenerationStrategy.RuleTemplate => GenerateFromTemplate(domain, request),
                _ => throw new ArgumentOutOfRangeException(nameof(request), request.Strategy, "Unsupported generation strategy."),
            };

            if (seen.Add(candidate.LocalPart))
            {
                candidates.Add(candidate);
            }
        }

        return candidates;
    }

    private static AliasCandidate GenerateStrongRandom(string domain, AliasGenerationRequest request)
    {
        var length = Math.Max(8, EntropyMath.RequiredCharacters(request.MinEntropyBits, Base32Alphabet.Length));
        var localPart = RandomToken(length);
        var entropy = EntropyMath.FromAlphabet(length, Base32Alphabet.Length);

        return new AliasCandidate(localPart, domain, request.Strategy, entropy, PrivacyLevel.High, []);
    }

    private static AliasCandidate GenerateReadable(string domain, AliasGenerationRequest request)
    {
        var wordEntropy = EntropyMath.FromProduct(Adjectives.Length, Nouns.Length);
        var suffixLength = Math.Max(6, EntropyMath.RequiredCharacters(
            Math.Max(1, request.MinEntropyBits - (int)Math.Floor(wordEntropy)),
            Base32Alphabet.Length));
        var adjective = Pick(Adjectives);
        var noun = Pick(Nouns);
        var suffix = RandomToken(suffixLength);
        var localPart = $"{adjective}-{noun}-{suffix}";
        var entropy = wordEntropy + EntropyMath.FromAlphabet(suffixLength, Base32Alphabet.Length);

        return new AliasCandidate(
            localPart,
            domain,
            request.Strategy,
            entropy,
            PrivacyLevel.Balanced,
            ["Readable aliases trade some privacy for human recognition."]);
    }

    private static AliasCandidate GenerateSiteAware(string domain, AliasGenerationRequest request)
    {
        var site = SlugifySite(request.Site);
        var suffixLength = Math.Max(8, EntropyMath.RequiredCharacters(request.MinEntropyBits, Base32Alphabet.Length));
        var suffix = RandomToken(suffixLength);
        var localPart = TruncateLocalPart($"{site}-{suffix}");
        var entropy = EntropyMath.FromAlphabet(suffixLength, Base32Alphabet.Length);

        return new AliasCandidate(
            localPart,
            domain,
            request.Strategy,
            entropy,
            PrivacyLevel.Balanced,
            ["The site prefix improves recognition but can reveal where the alias is used."]);
    }

    private static AliasCandidate GenerateFromTemplate(string domain, AliasGenerationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Template))
        {
            throw new ArgumentException("RuleTemplate generation requires a template.", nameof(request));
        }

        var entropy = 0d;
        var localPart = TemplateTokenRegex().Replace(request.Template, match =>
        {
            var name = match.Groups["name"].Value.ToLowerInvariant();
            var argument = match.Groups["arg"].Success ? int.Parse(match.Groups["arg"].Value) : 0;

            return name switch
            {
                "site" => SlugifySite(request.Site),
                "purpose" => Slugify(request.Purpose ?? "alias"),
                "year" => DateTimeOffset.UtcNow.Year.ToStringInvariant(),
                "word" => AddEntropy($"{Pick(Adjectives)}-{Pick(Nouns)}", EntropyMath.FromProduct(Adjectives.Length, Nouns.Length)),
                "rand" => AddEntropy(RandomToken(Math.Clamp(argument == 0 ? 8 : argument, 1, 32)), EntropyMath.FromAlphabet(Math.Clamp(argument == 0 ? 8 : argument, 1, 32), Base32Alphabet.Length)),
                _ => throw new ArgumentException($"Unknown alias template token '{name}'.", nameof(request)),
            };
        });

        localPart = Slugify(localPart);
        if (entropy < request.MinEntropyBits)
        {
            var suffixLength = EntropyMath.RequiredCharacters((int)Math.Ceiling(request.MinEntropyBits - entropy), Base32Alphabet.Length);
            localPart = $"{localPart}-{RandomToken(suffixLength)}";
            entropy += EntropyMath.FromAlphabet(suffixLength, Base32Alphabet.Length);
        }

        localPart = TruncateLocalPart(localPart);
        return new AliasCandidate(
            localPart,
            domain,
            request.Strategy,
            entropy,
            request.PrivacyLevel,
            entropy < 60 ? ["Template-based aliases should keep enough random suffix entropy for important accounts."] : []);

        string AddEntropy(string value, double bits)
        {
            entropy += bits;
            return value;
        }
    }

    private static void ValidateRequest(AliasGenerationRequest request)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.Count, MaxBatchSize);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.MinEntropyBits, 20);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.MinEntropyBits, 128);
    }

    private static string NormalizeDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new ArgumentException("Domain is required.", nameof(domain));
        }

        var normalized = domain.Trim().TrimStart('@').ToLowerInvariant();
        if (!DomainRegex().IsMatch(normalized))
        {
            throw new ArgumentException("Domain must be a hostname such as example.com.", nameof(domain));
        }

        return normalized;
    }

    private static string SlugifySite(string? site)
    {
        if (string.IsNullOrWhiteSpace(site))
        {
            return "site";
        }

        var value = site.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            value = uri.Host;
        }

        value = value.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? value[4..] : value;
        var firstLabel = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? value;

        return Slugify(firstLabel);
    }

    private static string Slugify(string value)
    {
        var builder = new StringBuilder(value.Length);
        var lastWasSeparator = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator)
            {
                builder.Append('-');
                lastWasSeparator = true;
            }
        }

        var slug = builder.ToString().Trim('-');
        return string.IsNullOrEmpty(slug) ? "alias" : slug;
    }

    private static string TruncateLocalPart(string localPart)
    {
        return localPart.Length <= MaxLocalPartLength ? localPart : localPart[..MaxLocalPartLength].TrimEnd('-');
    }

    private static string RandomToken(int length)
    {
        var builder = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            builder.Append(Base32Alphabet[RandomNumberGenerator.GetInt32(Base32Alphabet.Length)]);
        }

        return builder.ToString();
    }

    private static string Pick(IReadOnlyList<string> values)
    {
        return values[RandomNumberGenerator.GetInt32(values.Count)];
    }

    [GeneratedRegex(@"^(?!-)(?:[a-z0-9-]{1,63}\.)+[a-z]{2,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex DomainRegex();

    [GeneratedRegex(@"\{\{\s*(?<name>[A-Za-z]+)(?::(?<arg>\d+))?\s*\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex TemplateTokenRegex();
}

file static class InvariantFormatting
{
    public static string ToStringInvariant(this int value)
    {
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
