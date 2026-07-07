using AliasCockpit.Core.Providers;
using AliasCockpit.Infrastructure.Storage;
using Microsoft.Data.Sqlite;

namespace AliasCockpit.Infrastructure.Tests.Storage;

public sealed class SqliteProviderAccountRepositoryTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"aliascockpit-provider-accounts-{Guid.NewGuid():N}.sqlite");

    [Fact]
    public async Task UpsertAndListProviderAccounts()
    {
        var now = DateTimeOffset.Parse("2026-07-05T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var repository = new SqliteProviderAccountRepository(_databasePath);
        await repository.InitializeAsync();

        var simpleLogin = ProviderAccount.Create(ProviderTypes.SimpleLogin, "SimpleLogin personal", now)
            .MarkSecretStored(now.AddMinutes(1));
        var addy = ProviderAccount.Create(ProviderTypes.AddyIo, "addy.io work", now)
            .MarkValidated(now.AddMinutes(2));

        await repository.UpsertAsync(simpleLogin);
        await repository.UpsertAsync(addy);

        var stored = await repository.GetAsync(simpleLogin.Id);
        var accounts = await repository.ListAsync();

        Assert.NotNull(stored);
        Assert.Equal(simpleLogin.SecretRef, stored.SecretRef);
        Assert.Equal(ProviderAuthState.SecretStored, stored.AuthState);
        Assert.Equal([addy.Id, simpleLogin.Id], accounts.Select(account => account.Id));
    }

    [Fact]
    public async Task DeleteRemovesOnlyProviderAccountMetadata()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new SqliteProviderAccountRepository(_databasePath);
        await repository.InitializeAsync();
        var account = ProviderAccount.Create(ProviderTypes.AddyIo, "addy.io", now);

        await repository.UpsertAsync(account);
        await repository.DeleteAsync(account.Id);

        Assert.Null(await repository.GetAsync(account.Id));
        Assert.Empty(await repository.ListAsync());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _databasePath, $"{_databasePath}-wal", $"{_databasePath}-shm" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
