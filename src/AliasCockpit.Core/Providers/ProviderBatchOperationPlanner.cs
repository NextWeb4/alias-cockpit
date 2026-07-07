namespace AliasCockpit.Core.Providers;

public sealed class ProviderBatchOperationPlanner
{
    public ProviderBatchOperationPlan Plan(
        IProviderAdapter adapter,
        ProviderBatchOperationKind kind,
        IEnumerable<ProviderAliasReference> aliases)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(aliases);

        var aliasList = aliases.ToList();
        var items = aliasList.Select(alias => PlanItem(adapter, kind, alias)).ToList();
        var warnings = BuildPlanWarnings(kind, aliasList).ToList();

        return new ProviderBatchOperationPlan(
            Guid.NewGuid(),
            kind,
            ProviderTypes.Normalize(adapter.Profile.ProviderType),
            RequiresNetwork: items.Any(item => item.OperationPlan.RequiresNetwork),
            RequiresExplicitConfirmation: kind is ProviderBatchOperationKind.DeleteAliases,
            Reversible: kind is not ProviderBatchOperationKind.DeleteAliases && items.All(item => item.OperationPlan.Reversible),
            items,
            warnings);
    }

    private static ProviderBatchOperationItemPlan PlanItem(
        IProviderAdapter adapter,
        ProviderBatchOperationKind kind,
        ProviderAliasReference alias)
    {
        var operationPlan = kind switch
        {
            ProviderBatchOperationKind.DisableAliases => adapter.PlanDisableAlias(alias),
            ProviderBatchOperationKind.DeleteAliases => adapter.PlanDeleteAlias(alias),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported provider operation kind."),
        };

        var warnings = BuildItemWarnings(kind, alias, operationPlan).ToList();
        return new ProviderBatchOperationItemPlan(alias, operationPlan, warnings);
    }

    private static IEnumerable<string> BuildPlanWarnings(
        ProviderBatchOperationKind kind,
        IReadOnlyList<ProviderAliasReference> aliases)
    {
        if (aliases.Count == 0)
        {
            yield return "No aliases were selected.";
        }

        if (kind is ProviderBatchOperationKind.DeleteAliases)
        {
            yield return "Delete is destructive and requires explicit confirmation before execution.";
        }
    }

    private static IEnumerable<string> BuildItemWarnings(
        ProviderBatchOperationKind kind,
        ProviderAliasReference alias,
        ProviderAliasOperationPlan operationPlan)
    {
        foreach (var warning in operationPlan.Warnings)
        {
            yield return warning;
        }

        if (string.IsNullOrWhiteSpace(alias.Address))
        {
            yield return "Alias address is missing; UI must show the remote id before execution.";
        }

        if (kind is ProviderBatchOperationKind.DeleteAliases && !operationPlan.RequiresNetwork)
        {
            yield return "Delete plan must require network access.";
        }
    }
}
