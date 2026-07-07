using AliasCockpit.Core.Aliases;
using AliasCockpit.Infrastructure.Storage;
using Microsoft.Data.Sqlite;

namespace AliasCockpit.Infrastructure.Tests.Storage;

public sealed class SqliteAliasRepositoryTests
{
    [Fact]
    public async Task RepositoryInitializesUpsertsAndSearchesAliases()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"alias-cockpit-{Guid.NewGuid():N}.sqlite");
        try
        {
            var repository = new SqliteAliasRepository(dbPath);
            await repository.InitializeAsync();

            var now = DateTimeOffset.Parse("2026-07-05T00:00:00Z");
            await repository.UpsertAsync(AliasRecord.Create(
                "github-abc123@example.com",
                AliasStatus.Active,
                "SimpleLogin",
                "github.com",
                "development",
                "work,code",
                50,
                now,
                AliasColor.Blue));

            await repository.UpsertAsync(AliasRecord.Create(
                "bank-9q8w7e@example.com",
                AliasStatus.Active,
                "addy.io",
                "bank.example",
                "finance",
                "finance",
                60,
                now));

            Assert.Equal(2, await repository.CountAsync());

            var github = await repository.SearchAsync(new AliasSearchQuery("github code"));
            Assert.Single(github);
            Assert.Equal("github-abc123@example.com", github[0].Address);
            Assert.Equal(AliasColor.Blue, github[0].Color);

            var exact = await repository.GetByAddressAsync("GITHUB-ABC123@example.com");
            Assert.NotNull(exact);
            Assert.Equal("github.com", exact.Site);
            Assert.Equal(AliasColor.Blue, exact.Color);

            var all = await repository.SearchAsync(new AliasSearchQuery(Limit: 10));
            Assert.Equal(2, all.Count);

            await repository.UpsertManyAsync([
                AliasRecord.Create("one@example.com", AliasStatus.Active, "Manual", "one.example", null, "batch", 40, now),
                AliasRecord.Create("two@example.com", AliasStatus.Disabled, "Manual", "two.example", null, "batch", 40, now),
            ]);

            Assert.Equal(4, await repository.CountAsync());
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

    [Fact]
    public async Task InitializeAddsColorColumnToExistingAliasTable()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"alias-cockpit-migration-{Guid.NewGuid():N}.sqlite");
        try
        {
            await using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE aliases (
                        id TEXT PRIMARY KEY,
                        address TEXT NOT NULL UNIQUE,
                        local_part TEXT NOT NULL,
                        domain TEXT NOT NULL,
                        status TEXT NOT NULL,
                        provider TEXT NOT NULL,
                        site TEXT NULL,
                        purpose TEXT NULL,
                        tags TEXT NOT NULL,
                        entropy_bits REAL NOT NULL,
                        created_at TEXT NOT NULL,
                        updated_at TEXT NOT NULL
                    );
                    """;
                await command.ExecuteNonQueryAsync();
            }

            var repository = new SqliteAliasRepository(dbPath);
            await repository.InitializeAsync();

            var now = DateTimeOffset.Parse("2026-07-05T00:00:00Z");
            await repository.UpsertAsync(AliasRecord.Create(
                "marked@example.com",
                AliasStatus.Active,
                "Manual",
                "docs.example",
                null,
                "manual",
                10,
                now,
                AliasColor.Purple));

            var alias = await repository.GetByAddressAsync("marked@example.com");

            Assert.NotNull(alias);
            Assert.Equal(AliasColor.Purple, alias.Color);
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
