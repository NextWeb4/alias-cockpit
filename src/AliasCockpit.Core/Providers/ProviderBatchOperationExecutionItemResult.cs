namespace AliasCockpit.Core.Providers;

public sealed record ProviderBatchOperationExecutionItemResult(
    ProviderAliasReference Alias,
    bool Succeeded,
    ProviderAliasSnapshot? Snapshot,
    string? ErrorCode,
    IReadOnlyList<string> Warnings);
