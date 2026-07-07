namespace AliasCockpit.Core.Tools;

public interface ISavedEmailAddressRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(SavedEmailAddress address, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavedEmailAddress>> ListAsync(int limit = 50, CancellationToken cancellationToken = default);
}
