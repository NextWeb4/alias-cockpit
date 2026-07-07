using AliasCockpit.Core.Providers;

namespace AliasCockpit.Infrastructure.Providers;

public static class SimpleLoginProviderProfile
{
    public static ProviderProfile Create()
    {
        return new ProviderProfile(
            ProviderTypes.SimpleLogin,
            "SimpleLogin",
            ProviderSecurityState.Healthy,
            [
                Capability(ProviderCapability.AliasCreateRandom, CapabilitySupport.Yes, reversible: true, "Random alias creation via official REST API."),
                Capability(ProviderCapability.AliasCreateCustom, CapabilitySupport.Yes, reversible: true, "Custom alias creation requires provider-side option checks."),
                Capability(ProviderCapability.AliasUpdateMetadata, CapabilitySupport.Yes, reversible: true, "Metadata update remains provider-specific."),
                Capability(ProviderCapability.AliasDisable, CapabilitySupport.Yes, reversible: true, "Disable/toggle should be planned before execution."),
                Capability(ProviderCapability.AliasDelete, CapabilitySupport.Yes, reversible: false, "Remote delete is treated as destructive."),
                Capability(ProviderCapability.AliasRestore, CapabilitySupport.Partial, reversible: true, "Restore semantics depend on alias state."),
                Capability(ProviderCapability.RecipientManage, CapabilitySupport.Partial, reversible: true, "SimpleLogin mailbox mapping differs from addy.io recipients."),
                Capability(ProviderCapability.DomainManage, CapabilitySupport.Yes, reversible: true, "Custom domain capability exists but requires DNS/provider checks."),
                Capability(ProviderCapability.DomainCatchAll, CapabilitySupport.Partial, reversible: true, "Catch-all behavior varies by domain setup."),
                Capability(ProviderCapability.ReplyViaAlias, CapabilitySupport.Yes, reversible: false, "Reverse alias/contact behavior is provider-owned."),
                Capability(ProviderCapability.StatsRead, CapabilitySupport.Yes, reversible: true, "Stats read is non-mutating."),
                Capability(ProviderCapability.ExportRemote, CapabilitySupport.Partial, reversible: true, "Export/pull scope must be verified per account."),
            ]);
    }

    private static ProviderCapabilityDescriptor Capability(
        ProviderCapability capability,
        CapabilitySupport support,
        bool reversible,
        string notes)
    {
        return new ProviderCapabilityDescriptor(
            capability,
            support,
            OfflineAvailable: false,
            reversible,
            ScopeRequired: "api_key",
            notes);
    }
}
