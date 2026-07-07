using AliasCockpit.Core.Providers;

namespace AliasCockpit.Core.Tests.Providers;

public sealed class ProviderProfileTests
{
    [Fact]
    public void MissingCapabilityDefaultsToNoSupport()
    {
        var profile = new ProviderProfile(
            "manual",
            "Manual Provider",
            ProviderSecurityState.ManualOnly,
            [
                new ProviderCapabilityDescriptor(
                    ProviderCapability.ExportRemote,
                    CapabilitySupport.Manual,
                    OfflineAvailable: true,
                    Reversible: true,
                    ScopeRequired: null,
                    Notes: "User-managed export"),
            ]);

        Assert.Equal(CapabilitySupport.Manual, profile.SupportFor(ProviderCapability.ExportRemote));
        Assert.Equal(CapabilitySupport.No, profile.SupportFor(ProviderCapability.AliasCreateRandom));
    }
}
