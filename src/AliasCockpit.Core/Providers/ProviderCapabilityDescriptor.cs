namespace AliasCockpit.Core.Providers;

public sealed record ProviderCapabilityDescriptor(
    ProviderCapability Capability,
    CapabilitySupport Support,
    bool OfflineAvailable,
    bool Reversible,
    string? ScopeRequired,
    string? Notes);

