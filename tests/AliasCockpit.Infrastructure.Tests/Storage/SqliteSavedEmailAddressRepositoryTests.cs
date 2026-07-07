using AliasCockpit.Core.Tools;
using AliasCockpit.Infrastructure.Storage;
using Microsoft.Data.Sqlite;

namespace AliasCockpit.Infrastructure.Tests.Storage;

public sealed class SqliteSavedEmailAddressRepositoryTests
{
    [Fact]
    public async Task RepositoryUpsertsAndListsSavedEmailAddressesByRecentUse()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"alias-cockpit-saved-{Guid.NewGuid():N}.sqlite");
        try
        {
            var repository = new SqliteSavedEmailAddressRepository(dbPath);
            await repository.InitializeAsync();

            var older = DateTimeOffset.Parse("2026-07-05T00:00:00Z");
            var newer = older.AddMinutes(10);
            await repository.UpsertAsync(SavedEmailAddress.Create("First@Gmail.com", older));
            await repository.UpsertAsync(SavedEmailAddress.Create("second@outlook.com", newer));
            await repository.UpsertAsync(SavedEmailAddress.Create("first@gmail.com", newer.AddMinutes(10)));

            var saved = await repository.ListAsync();

            Assert.Equal(2, saved.Count);
            Assert.Equal("first@gmail.com", saved[0].Address);
            Assert.Equal("second@outlook.com", saved[1].Address);
            Assert.Equal(newer.AddMinutes(10), saved[0].LastUsedAt);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var path in new[] { dbPath, $"{dbPath}-wal", $"{dbPath}-shm" })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
