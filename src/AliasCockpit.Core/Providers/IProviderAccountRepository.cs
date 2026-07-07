namespace AliasCockpit.Core.Providers;

public interface IProviderAccountRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(ProviderAccount account, CancellationToken cancellationToken = default);

    Task<ProviderAccount?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProviderAccount>> ListAsync(CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
