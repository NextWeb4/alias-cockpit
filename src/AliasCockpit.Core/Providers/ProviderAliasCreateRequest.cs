namespace AliasCockpit.Core.Providers;

public sealed record ProviderAliasCreateRequest(
    string Hostname,
    string? LocalPart,
    string Domain,
    string? Description,
    bool PreferRandom,
    DateTimeOffset RequestedAt)
{
    public static ProviderAliasCreateRequest Random(
        string hostname,
        string domain,
        string? description,
        DateTimeOffset requestedAt)
    {
        return new ProviderAliasCreateRequest(hostname, null, domain, description, PreferRandom: true, requestedAt);
    }

    public static ProviderAliasCreateRequest Custom(
        string hostname,
        string localPart,
        string domain,
        string? description,
        DateTimeOffset requestedAt)
    {
        return new ProviderAliasCreateRequest(hostname, localPart, domain, description, PreferRandom: false, requestedAt);
    }
}
