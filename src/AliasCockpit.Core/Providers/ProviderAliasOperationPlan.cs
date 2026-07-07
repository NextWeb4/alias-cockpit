namespace AliasCockpit.Core.Providers;

public sealed record ProviderAliasOperationPlan(
    string ProviderType,
    ProviderCapability CapabilityRequired,
    bool RequiresNetwork,
    bool Reversible,
    string EndpointHint,
    IReadOnlyList<string> Steps,
    IReadOnlyList<string> Warnings)
{
    public bool CanExecute => Warnings.Count == 0;
}
