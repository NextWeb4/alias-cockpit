namespace AliasCockpit.Core.Providers;

public sealed record ProviderAliasReference
{
    public ProviderAliasReference(string remoteId, string? address, DateTimeOffset requestedAt)
    {
        if (string.IsNullOrWhiteSpace(remoteId))
        {
            throw new ArgumentException("Remote alias id is required.", nameof(remoteId));
        }

        RemoteId = remoteId.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        RequestedAt = requestedAt;
    }

    public string RemoteId { get; init; }

    public string? Address { get; init; }

    public DateTimeOffset RequestedAt { get; init; }
}
