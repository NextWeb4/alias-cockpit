namespace AliasCockpit.Core.Providers;

public sealed record ProviderBatchOperationExecutionResult(
    Guid OperationId,
    ProviderBatchOperationKind Kind,
    bool Rejected,
    string? RejectionCode,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ProviderBatchOperationExecutionItemResult> Items)
{
    public int SucceededCount => Items.Count(item => item.Succeeded);

    public int FailedCount => Items.Count(item => !item.Succeeded);
}
