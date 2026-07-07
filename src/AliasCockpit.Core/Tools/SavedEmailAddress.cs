namespace AliasCockpit.Core.Tools;

public sealed record SavedEmailAddress(
    string Address,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUsedAt)
{
    public static SavedEmailAddress Create(string address, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("Saved email address is required.", nameof(address));
        }

        return new SavedEmailAddress(address.Trim().ToLowerInvariant(), now, now);
    }
}
