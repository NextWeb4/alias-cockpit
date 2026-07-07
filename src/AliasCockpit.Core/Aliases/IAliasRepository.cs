namespace AliasCockpit.Core.Aliases;

public interface IAliasRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(AliasRecord alias, CancellationToken cancellationToken = default);

    Task UpsertManyAsync(IEnumerable<AliasRecord> aliases, CancellationToken cancellationToken = default);

    Task<AliasRecord?> GetByAddressAsync(string address, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AliasRecord>> SearchAsync(AliasSearchQuery query, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
