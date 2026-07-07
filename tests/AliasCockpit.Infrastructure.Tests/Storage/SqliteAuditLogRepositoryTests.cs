using AliasCockpit.Core.Audit;
using AliasCockpit.Infrastructure.Storage;
using Microsoft.Data.Sqlite;

namespace AliasCockpit.Infrastructure.Tests.Storage;

public sealed class SqliteAuditLogRepositoryTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"aliascockpit-audit-{Guid.NewGuid():N}.sqlite");

    [Fact]
    public async Task AppendAndListAuditEventsByOperation()
    {
        var operationId = Guid.NewGuid();
        var createdAt = DateTimeOffset.Parse("2026-07-05T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var repository = new SqliteAuditLogRepository(_databasePath);
        await repository.InitializeAsync();

        var auditEvent = new AuditEvent(
            Guid.NewGuid(),
            operationId,
            "device-1",
            "alias",
            "remote-1",
            "provider.delete",
            "before-hash",
            "after-hash",
            """{"address":"a***e@example.test","token":"[redacted]"}""",
            createdAt);

        await repository.AppendAuditEventAsync(auditEvent);

        var stored = await repository.ListAuditEventsAsync(operationId);
        var unrelated = await repository.ListAuditEventsAsync(Guid.NewGuid());

        Assert.Single(stored);
        Assert.Empty(unrelated);
        Assert.Equal(auditEvent.Id, stored[0].Id);
        Assert.Equal(auditEvent.OperationId, stored[0].OperationId);
        Assert.Equal("provider.delete", stored[0].Operation);
        Assert.Equal("before-hash", stored[0].BeforeHash);
        Assert.Equal("after-hash", stored[0].AfterHash);
        Assert.DoesNotContain("secret-token", stored[0].RedactedSummaryJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AppendAndListTombstonesByEntity()
    {
        var deletedAt = DateTimeOffset.Parse("2026-07-05T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var repository = new SqliteAuditLogRepository(_databasePath);
        await repository.InitializeAsync();

        var tombstone = new Tombstone(
            Guid.NewGuid(),
            "alias",
            "remote-1",
            "provider.delete",
            deletedAt,
            deletedAt.AddDays(30));

        await repository.AppendTombstoneAsync(tombstone);

        var stored = await repository.ListTombstonesAsync(" alias ", " remote-1 ");
        var unrelated = await repository.ListTombstonesAsync("alias", "remote-2");

        Assert.Single(stored);
        Assert.Empty(unrelated);
        Assert.Equal(tombstone.Id, stored[0].Id);
        Assert.Equal("provider.delete", stored[0].Reason);
        Assert.Equal(deletedAt.AddDays(30), stored[0].PurgeAfter);
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
