namespace AliasCockpit.Core.Providers;

public sealed record ProviderBatchOperationPlan(
    Guid OperationId,
    ProviderBatchOperationKind Kind,
    string ProviderType,
    bool RequiresNetwork,
    bool RequiresExplicitConfirmation,
    bool Reversible,
    IReadOnlyList<ProviderBatchOperationItemPlan> Items,
    IReadOnlyList<string> Warnings)
{
    public int TotalCount => Items.Count;

    public int ExecutableCount => Items.Count(item => item.CanExecute);

    public int BlockedCount => TotalCount - ExecutableCount;

    public bool ItemsCanExecute => TotalCount > 0 && BlockedCount == 0;

    public bool CanExecute => TotalCount > 0 && BlockedCount == 0 && Warnings.Count == 0;
}
