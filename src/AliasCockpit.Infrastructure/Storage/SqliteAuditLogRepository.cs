using AliasCockpit.Core.Audit;
using Microsoft.Data.Sqlite;

namespace AliasCockpit.Infrastructure.Storage;

public sealed class SqliteAuditLogRepository(string databasePath) : IAuditLogRepository
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = databasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
    }.ToString();

    static SqliteAuditLogRepository()
    {
        SQLitePCL.Batteries_V2.Init();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await ExecuteAsync(connection, """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA temp_store = MEMORY;
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS audit_events (
                id TEXT PRIMARY KEY,
                operation_id TEXT NOT NULL,
                device_id TEXT NOT NULL,
                entity_type TEXT NOT NULL,
                entity_id TEXT NOT NULL,
                operation TEXT NOT NULL,
                before_hash TEXT NULL,
                after_hash TEXT NULL,
                redacted_summary_json TEXT NOT NULL,
                created_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_audit_events_operation
            ON audit_events(operation_id, created_at);

            CREATE INDEX IF NOT EXISTS idx_audit_events_entity
            ON audit_events(entity_type, entity_id, created_at);

            CREATE TABLE IF NOT EXISTS tombstones (
                id TEXT PRIMARY KEY,
                entity_type TEXT NOT NULL,
                entity_id TEXT NOT NULL,
                reason TEXT NOT NULL,
                deleted_at TEXT NOT NULL,
                purge_after TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_tombstones_entity
            ON tombstones(entity_type, entity_id, deleted_at);
            """, cancellationToken);
    }

    public async Task AppendAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO audit_events (
                id, operation_id, device_id, entity_type, entity_id, operation, before_hash, after_hash, redacted_summary_json, created_at
            )
            VALUES (
                $id, $operation_id, $device_id, $entity_type, $entity_id, $operation, $before_hash, $after_hash, $redacted_summary_json, $created_at
            );
            """;
        AddAuditParameters(command);
        SetAuditParameterValues(command, auditEvent);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AppendTombstoneAsync(Tombstone tombstone, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO tombstones (
                id, entity_type, entity_id, reason, deleted_at, purge_after
            )
            VALUES (
                $id, $entity_type, $entity_id, $reason, $deleted_at, $purge_after
            );
            """;
        AddTombstoneParameters(command);
        SetTombstoneParameterValues(command, tombstone);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEvent>> ListAuditEventsAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, operation_id, device_id, entity_type, entity_id, operation, before_hash, after_hash, redacted_summary_json, created_at
            FROM audit_events
            WHERE operation_id = $operation_id
            ORDER BY created_at, id;
            """;
        command.Parameters.AddWithValue("$operation_id", operationId.ToString("D"));

        var events = new List<AuditEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(ReadAuditEvent(reader));
        }

        return events;
    }

    public async Task<IReadOnlyList<Tombstone>> ListTombstonesAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, entity_type, entity_id, reason, deleted_at, purge_after
            FROM tombstones
            WHERE entity_type = $entity_type AND entity_id = $entity_id
            ORDER BY deleted_at, id;
            """;
        command.Parameters.AddWithValue("$entity_type", entityType.Trim());
        command.Parameters.AddWithValue("$entity_id", entityId.Trim());

        var tombstones = new List<Tombstone>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tombstones.Add(ReadTombstone(reader));
        }

        return tombstones;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string commandText, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddAuditParameters(SqliteCommand command)
    {
        command.Parameters.Add("$id", SqliteType.Text);
        command.Parameters.Add("$operation_id", SqliteType.Text);
        command.Parameters.Add("$device_id", SqliteType.Text);
        command.Parameters.Add("$entity_type", SqliteType.Text);
        command.Parameters.Add("$entity_id", SqliteType.Text);
        command.Parameters.Add("$operation", SqliteType.Text);
        command.Parameters.Add("$before_hash", SqliteType.Text);
        command.Parameters.Add("$after_hash", SqliteType.Text);
        command.Parameters.Add("$redacted_summary_json", SqliteType.Text);
        command.Parameters.Add("$created_at", SqliteType.Text);
    }

    private static void SetAuditParameterValues(SqliteCommand command, AuditEvent auditEvent)
    {
        command.Parameters["$id"].Value = auditEvent.Id.ToString("D");
        command.Parameters["$operation_id"].Value = auditEvent.OperationId.ToString("D");
        command.Parameters["$device_id"].Value = auditEvent.DeviceId;
        command.Parameters["$entity_type"].Value = auditEvent.EntityType;
        command.Parameters["$entity_id"].Value = auditEvent.EntityId;
        command.Parameters["$operation"].Value = auditEvent.Operation;
        command.Parameters["$before_hash"].Value = (object?)auditEvent.BeforeHash ?? DBNull.Value;
        command.Parameters["$after_hash"].Value = (object?)auditEvent.AfterHash ?? DBNull.Value;
        command.Parameters["$redacted_summary_json"].Value = auditEvent.RedactedSummaryJson;
        command.Parameters["$created_at"].Value = auditEvent.CreatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AddTombstoneParameters(SqliteCommand command)
    {
        command.Parameters.Add("$id", SqliteType.Text);
        command.Parameters.Add("$entity_type", SqliteType.Text);
        command.Parameters.Add("$entity_id", SqliteType.Text);
        command.Parameters.Add("$reason", SqliteType.Text);
        command.Parameters.Add("$deleted_at", SqliteType.Text);
        command.Parameters.Add("$purge_after", SqliteType.Text);
    }

    private static void SetTombstoneParameterValues(SqliteCommand command, Tombstone tombstone)
    {
        command.Parameters["$id"].Value = tombstone.Id.ToString("D");
        command.Parameters["$entity_type"].Value = tombstone.EntityType;
        command.Parameters["$entity_id"].Value = tombstone.EntityId;
        command.Parameters["$reason"].Value = tombstone.Reason;
        command.Parameters["$deleted_at"].Value = tombstone.DeletedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        command.Parameters["$purge_after"].Value = tombstone.PurgeAfter is null
            ? DBNull.Value
            : tombstone.PurgeAfter.Value.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static AuditEvent ReadAuditEvent(SqliteDataReader reader)
    {
        return new AuditEvent(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.GetString(8),
            DateTimeOffset.Parse(reader.GetString(9), System.Globalization.CultureInfo.InvariantCulture));
    }

    private static Tombstone ReadTombstone(SqliteDataReader reader)
    {
        return new Tombstone(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            DateTimeOffset.Parse(reader.GetString(4), System.Globalization.CultureInfo.InvariantCulture),
            reader.IsDBNull(5) ? null : DateTimeOffset.Parse(reader.GetString(5), System.Globalization.CultureInfo.InvariantCulture));
    }
}
