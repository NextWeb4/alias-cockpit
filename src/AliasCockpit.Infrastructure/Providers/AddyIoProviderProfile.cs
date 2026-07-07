using AliasCockpit.Core.Providers;

namespace AliasCockpit.Infrastructure.Providers;

public static class AddyIoProviderProfile
{
    public static ProviderProfile Create()
    {
        return new ProviderProfile(
            ProviderTypes.AddyIo,
            "addy.io",
            ProviderSecurityState.Healthy,
            [
                Capability(ProviderCapability.AliasCreateRandom, CapabilitySupport.Yes, reversible: true, "Random alias creation uses the aliases endpoint."),
                Capability(ProviderCapability.AliasCreateCustom, CapabilitySupport.Yes, reversible: true, "Custom alias creation uses the aliases endpoint."),
                Capability(ProviderCapability.AliasCreateOnTheFly, CapabilitySupport.Yes, reversible: true, "On-the-fly alias creation is provider/domain behavior."),
                Capability(ProviderCapability.AliasUpdateMetadata, CapabilitySupport.Yes, reversible: true, "Alias metadata can be patched remotely."),
                Capability(ProviderCapability.AliasDisable, CapabilitySupport.Yes, reversible: true, "Disable state maps to provider update semantics."),
                Capability(ProviderCapability.AliasDelete, CapabilitySupport.Yes, reversible: false, "Remote delete is treated as destructive."),
                Capability(ProviderCapability.AliasRestore, CapabilitySupport.Yes, reversible: true, "Deleted aliases can be restored when provider retains them."),
                Capability(ProviderCapability.RecipientManage, CapabilitySupport.Yes, reversible: true, "Recipients are first-class addy.io resources."),
                Capability(ProviderCapability.DomainManage, CapabilitySupport.Yes, reversible: true, "Domains and usernames affect alias routing."),
                Capability(ProviderCapability.DomainCatchAll, CapabilitySupport.Yes, reversible: true, "Catch-all must be exposed as domain risk."),
                Capability(ProviderCapability.ReplyViaAlias, CapabilitySupport.Yes, reversible: false, "Reply/send behavior relies on provider routing."),
                Capability(ProviderCapability.SendFromAlias, CapabilitySupport.Partial, reversible: false, "Active send support depends on account/domain setup."),
                Capability(ProviderCapability.RulesManage, CapabilitySupport.Yes, reversible: true, "Rules are first-class resources."),
                Capability(ProviderCapability.WebhookReceive, CapabilitySupport.Yes, reversible: true, "Webhooks require explicit user setup."),
                Capability(ProviderCapability.StatsRead, CapabilitySupport.Yes, reversible: true, "Stats read is non-mutating."),
                Capability(ProviderCapability.ExportRemote, CapabilitySupport.Yes, reversible: true, "Remote snapshot can be pulled through the API."),
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
