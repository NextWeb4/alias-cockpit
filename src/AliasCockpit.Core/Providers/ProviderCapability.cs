namespace AliasCockpit.Core.Providers;

public enum ProviderCapability
{
    AliasCreateRandom,
    AliasCreateCustom,
    AliasCreateOnTheFly,
    AliasUpdateMetadata,
    AliasDisable,
    AliasDelete,
    AliasRestore,
    RecipientManage,
    DomainManage,
    DomainCatchAll,
    ReplyViaAlias,
    SendFromAlias,
    RulesManage,
    WebhookReceive,
    StatsRead,
    ExportRemote,
    ImportRemote,
}

