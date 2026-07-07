namespace AliasCockpit.Core.Providers;

public sealed record ProviderBatchOperationItemPlan(
    ProviderAliasReference Alias,
    ProviderAliasOperationPlan OperationPlan,
    IReadOnlyList<string> Warnings)
{
    public bool CanExecute => OperationPlan.CanExecute && Warnings.Count == 0;
}
