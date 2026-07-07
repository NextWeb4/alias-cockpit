namespace AliasCockpit.Core.Audit;

public interface IAuditLogRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task AppendAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);

    Task AppendTombstoneAsync(Tombstone tombstone, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditEvent>> ListAuditEventsAsync(Guid operationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tombstone>> ListTombstonesAsync(string entityType, string entityId, CancellationToken cancellationToken = default);
}
