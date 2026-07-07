namespace AliasCockpit.Core.Security;

public static class Redactor
{
    public static string RedactEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var at = value.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at == value.Length - 1)
        {
            return "[redacted]";
        }

        var local = value[..at];
        var domain = value[(at + 1)..];
        var visibleLocal = local.Length <= 2
            ? $"{local[0]}*"
            : $"{local[0]}***{local[^1]}";

        return $"{visibleLocal}@{domain}";
    }
}

