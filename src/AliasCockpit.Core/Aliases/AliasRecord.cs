namespace AliasCockpit.Core.Aliases;

public sealed record AliasRecord(
    Guid Id,
    string Address,
    string LocalPart,
    string Domain,
    AliasStatus Status,
    string Provider,
    string? Site,
    string? Purpose,
    string Tags,
    AliasColor Color,
    double EntropyBits,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static AliasRecord Create(
        string address,
        AliasStatus status,
        string provider,
        string? site,
        string? purpose,
        string tags,
        double entropyBits,
        DateTimeOffset now,
        AliasColor color = AliasColor.None)
    {
        var parsed = AliasAddress.Parse(address);
        return new AliasRecord(
            Guid.NewGuid(),
            parsed.Address,
            parsed.LocalPart,
            parsed.Domain,
            status,
            string.IsNullOrWhiteSpace(provider) ? "Manual" : provider.Trim(),
            string.IsNullOrWhiteSpace(site) ? null : site.Trim(),
            string.IsNullOrWhiteSpace(purpose) ? null : purpose.Trim(),
            tags.Trim(),
            color,
            entropyBits,
            now,
            now);
    }
}

