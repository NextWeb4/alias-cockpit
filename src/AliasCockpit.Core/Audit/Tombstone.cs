namespace AliasCockpit.Core.Audit;

public sealed record Tombstone
{
    public Tombstone(
        Guid id,
        string entityType,
        string entityId,
        string reason,
        DateTimeOffset deletedAt,
        DateTimeOffset? purgeAfter)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Tombstone id is required.", nameof(id));
        }

        Id = id;
        EntityType = NormalizeRequired(entityType, nameof(entityType));
        EntityId = NormalizeRequired(entityId, nameof(entityId));
        Reason = NormalizeRequired(reason, nameof(reason));
        DeletedAt = deletedAt;
        PurgeAfter = purgeAfter;
    }

    public Guid Id { get; init; }

    public string EntityType { get; init; }

    public string EntityId { get; init; }

    public string Reason { get; init; }

    public DateTimeOffset DeletedAt { get; init; }

    public DateTimeOffset? PurgeAfter { get; init; }

    public static Tombstone Create(
        string entityType,
        string entityId,
        string reason,
        DateTimeOffset deletedAt,
        DateTimeOffset? purgeAfter = null)
    {
        return new Tombstone(Guid.NewGuid(), entityType, entityId, reason, deletedAt, purgeAfter);
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        return value.Trim();
    }
}
