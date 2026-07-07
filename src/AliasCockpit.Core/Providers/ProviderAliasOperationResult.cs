namespace AliasCockpit.Core.Providers;

public sealed record ProviderAliasOperationResult(
    bool Succeeded,
    ProviderAliasSnapshot? Alias,
    string? ErrorCode,
    IReadOnlyList<string> Warnings)
{
    public static ProviderAliasOperationResult Success(ProviderAliasSnapshot alias, IReadOnlyList<string> warnings)
    {
        return new ProviderAliasOperationResult(true, alias, null, warnings);
    }

    public static ProviderAliasOperationResult Failure(string errorCode, IReadOnlyList<string> warnings)
    {
        return new ProviderAliasOperationResult(false, null, errorCode, warnings);
    }
}
