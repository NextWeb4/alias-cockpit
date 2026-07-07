using AliasCockpit.Core.Secrets;

namespace AliasCockpit.Core.Providers;

public sealed class ProviderBatchOperationExecutor
{
    public async Task<ProviderBatchOperationExecutionResult> ExecuteAsync(
        IProviderAdapter adapter,
        ProviderAccount account,
        ProviderBatchOperationPlan plan,
        ISecretStore secretStore,
        bool explicitlyConfirmed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(secretStore);

        if (!plan.ItemsCanExecute)
        {
            return Rejected(plan, "plan.blocked", ["Batch plan has blocked items and must be regenerated or fixed."]);
        }

        if (plan.RequiresExplicitConfirmation && !explicitlyConfirmed)
        {
            return Rejected(plan, "confirmation.required", ["This batch operation requires explicit confirmation."]);
        }

        var results = new List<ProviderBatchOperationExecutionItemResult>(plan.Items.Count);
        foreach (var item in plan.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = plan.Kind switch
            {
                ProviderBatchOperationKind.DisableAliases => await adapter.DisableAliasAsync(account, item.Alias, secretStore, cancellationToken),
                ProviderBatchOperationKind.DeleteAliases => await adapter.DeleteAliasAsync(account, item.Alias, secretStore, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(plan), plan.Kind, "Unsupported batch operation kind."),
            };

            results.Add(new ProviderBatchOperationExecutionItemResult(
                item.Alias,
                result.Succeeded,
                result.Alias,
                result.ErrorCode,
                result.Warnings));
        }

        return new ProviderBatchOperationExecutionResult(
            plan.OperationId,
            plan.Kind,
            Rejected: false,
            RejectionCode: null,
            Warnings: plan.Warnings,
            results);
    }

    private static ProviderBatchOperationExecutionResult Rejected(
        ProviderBatchOperationPlan plan,
        string rejectionCode,
        IReadOnlyList<string> warnings)
    {
        return new ProviderBatchOperationExecutionResult(
            plan.OperationId,
            plan.Kind,
            Rejected: true,
            rejectionCode,
            warnings,
            Items: []);
    }
}
