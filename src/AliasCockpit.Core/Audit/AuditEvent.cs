namespace AliasCockpit.Core.Audit;

public sealed record AuditEvent
{
    public AuditEvent(
        Guid id,
        Guid operationId,
        string deviceId,
        string entityType,
        string entityId,
        string operation,
        string? beforeHash,
        string? afterHash,
        string redactedSummaryJson,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Audit event id is required.", nameof(id));
        }

        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("Operation id is required.", nameof(operationId));
        }

        Id = id;
        OperationId = operationId;
        DeviceId = NormalizeRequired(deviceId, nameof(deviceId));
        EntityType = NormalizeRequired(entityType, nameof(entityType));
        EntityId = NormalizeRequired(entityId, nameof(entityId));
        Operation = NormalizeRequired(operation, nameof(operation));
        BeforeHash = NormalizeOptional(beforeHash);
        AfterHash = NormalizeOptional(afterHash);
        RedactedSummaryJson = NormalizeRequired(redactedSummaryJson, nameof(redactedSummaryJson));
        CreatedAt = createdAt;
    }

    public Guid Id { get; init; }

    public Guid OperationId { get; init; }

    public string DeviceId { get; init; }

    public string EntityType { get; init; }

    public string EntityId { get; init; }

    public string Operation { get; init; }

    public string? BeforeHash { get; init; }

    public string? AfterHash { get; init; }

    public string RedactedSummaryJson { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public static AuditEvent Create(
        Guid operationId,
        string deviceId,
        string entityType,
        string entityId,
        string operation,
        string redactedSummaryJson,
        DateTimeOffset now,
        string? beforeHash = null,
        string? afterHash = null)
    {
        return new AuditEvent(
            Guid.NewGuid(),
            operationId,
            deviceId,
            entityType,
            entityId,
            operation,
            beforeHash,
            afterHash,
            redactedSummaryJson,
            now);
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
