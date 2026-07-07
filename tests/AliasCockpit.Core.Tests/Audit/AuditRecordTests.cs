using AliasCockpit.Core.Audit;

namespace AliasCockpit.Core.Tests.Audit;

public sealed class AuditRecordTests
{
    [Fact]
    public void AuditEventNormalizesValuesAndOptionalHashes()
    {
        var operationId = Guid.NewGuid();
        var createdAt = DateTimeOffset.Parse("2026-07-05T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

        var auditEvent = new AuditEvent(
            Guid.NewGuid(),
            operationId,
            " device-1 ",
            " alias ",
            " remote-1 ",
            " delete ",
            " ",
            " after-hash ",
            """{"address":"a***e@example.test"}""",
            createdAt);

        Assert.Equal("device-1", auditEvent.DeviceId);
        Assert.Equal("alias", auditEvent.EntityType);
        Assert.Equal("remote-1", auditEvent.EntityId);
        Assert.Equal("delete", auditEvent.Operation);
        Assert.Null(auditEvent.BeforeHash);
        Assert.Equal("after-hash", auditEvent.AfterHash);
    }

    [Fact]
    public void AuditEventRejectsEmptyRequiredValues()
    {
        var operationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() => new AuditEvent(
            Guid.Empty,
            operationId,
            "device-1",
            "alias",
            "remote-1",
            "delete",
            null,
            null,
            "{}",
            now));

        Assert.Throws<ArgumentException>(() => AuditEvent.Create(
            operationId,
            "",
            "alias",
            "remote-1",
            "delete",
            "{}",
            now));
    }

    [Fact]
    public void TombstoneNormalizesValuesAndRequiresEntity()
    {
        var deletedAt = DateTimeOffset.Parse("2026-07-05T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var purgeAfter = deletedAt.AddDays(30);

        var tombstone = new Tombstone(
            Guid.NewGuid(),
            " alias ",
            " remote-1 ",
            " provider delete ",
            deletedAt,
            purgeAfter);

        Assert.Equal("alias", tombstone.EntityType);
        Assert.Equal("remote-1", tombstone.EntityId);
        Assert.Equal("provider delete", tombstone.Reason);
        Assert.Equal(purgeAfter, tombstone.PurgeAfter);

        Assert.Throws<ArgumentException>(() => Tombstone.Create("alias", "", "delete", deletedAt));
    }
}
