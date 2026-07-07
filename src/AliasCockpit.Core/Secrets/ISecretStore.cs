namespace AliasCockpit.Core.Secrets;

public interface ISecretStore
{
    Task SetSecretAsync(string key, string secret, CancellationToken cancellationToken = default);

    Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default);
}

