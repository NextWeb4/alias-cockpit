using AliasCockpit.Core.Providers;

namespace AliasCockpit.Infrastructure.Providers;

public sealed class SimpleLoginMockProviderAdapter : ProviderAdapterBase
{
    public SimpleLoginMockProviderAdapter()
        : base(SimpleLoginProviderProfile.Create())
    {
    }

    protected override string EndpointFor(ProviderCapability capability)
    {
        return capability switch
        {
            ProviderCapability.AliasCreateRandom => "POST /api/alias/random/new",
            ProviderCapability.AliasCreateCustom => "GET /api/v5/alias/options + POST /api/v3/alias/custom/new",
            ProviderCapability.AliasUpdateMetadata => "PATCH /api/aliases/{alias_id}",
            ProviderCapability.AliasDisable => "POST /api/aliases/{alias_id}/toggle",
            ProviderCapability.AliasDelete => "DELETE /api/aliases/{alias_id}",
            ProviderCapability.StatsRead => "GET /api/stats",
            _ => "SimpleLogin API capability endpoint",
        };
    }

}
