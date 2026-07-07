using AliasCockpit.Core.Providers;

namespace AliasCockpit.Infrastructure.Providers;

public sealed class AddyIoMockProviderAdapter : ProviderAdapterBase
{
    public AddyIoMockProviderAdapter()
        : base(AddyIoProviderProfile.Create())
    {
    }

    protected override string EndpointFor(ProviderCapability capability)
    {
        return capability switch
        {
            ProviderCapability.AliasCreateRandom or ProviderCapability.AliasCreateCustom => "POST /api/v1/aliases",
            ProviderCapability.AliasUpdateMetadata => "PATCH /api/v1/aliases/{id}",
            ProviderCapability.AliasDisable => "PATCH /api/v1/aliases/{id}",
            ProviderCapability.AliasDelete => "DELETE /api/v1/aliases/{id}",
            ProviderCapability.AliasRestore => "PATCH /api/v1/aliases/{id}/restore",
            ProviderCapability.RecipientManage => "GET/POST /api/v1/recipients",
            ProviderCapability.RulesManage => "GET/POST /api/v1/rules",
            ProviderCapability.WebhookReceive => "GET/POST /api/v1/webhooks",
            _ => "addy.io API capability endpoint",
        };
    }

}
