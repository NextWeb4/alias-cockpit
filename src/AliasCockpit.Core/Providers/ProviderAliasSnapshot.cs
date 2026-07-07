using AliasCockpit.Core.Aliases;

namespace AliasCockpit.Core.Providers;

public sealed record ProviderAliasSnapshot(
    string RemoteId,
    string Address,
    AliasStatus Status,
    string? Description,
    DateTimeOffset CreatedAt);
