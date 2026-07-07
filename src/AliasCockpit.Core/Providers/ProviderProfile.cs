namespace AliasCockpit.Core.Providers;

public sealed record ProviderProfile(
    string ProviderType,
    string DisplayName,
    ProviderSecurityState SecurityState,
    IReadOnlyList<ProviderCapabilityDescriptor> Capabilities)
{
    public CapabilitySupport SupportFor(ProviderCapability capability)
    {
        return Capabilities.FirstOrDefault(item => item.Capability == capability)?.Support ?? CapabilitySupport.No;
    }
}

