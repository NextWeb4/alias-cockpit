using System.Text;
using AliasCockpit.Core.Aliases;

namespace AliasCockpit.Core.ImportExport;

public sealed class AliasCsvExporter
{
    private static readonly string[] Headers =
    [
        "address", "status", "provider", "site", "purpose", "tags", "color", "entropy_bits", "created_at", "updated_at"
    ];

    public string Export(IEnumerable<AliasRecord> aliases)
    {
        var builder = new StringBuilder();
        WriteRow(builder, Headers);

        foreach (var alias in aliases)
        {
            WriteRow(builder,
            [
                alias.Address,
                alias.Status.ToString(),
                alias.Provider,
                alias.Site ?? string.Empty,
                alias.Purpose ?? string.Empty,
                alias.Tags,
                alias.Color.ToString(),
                alias.EntropyBits.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                alias.CreatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                alias.UpdatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ]);
        }

        return builder.ToString();
    }

    private static void WriteRow(StringBuilder builder, IEnumerable<string> values)
    {
        builder.AppendJoin(',', values.Select(EscapeField));
        builder.AppendLine();
    }

    private static string EscapeField(string value)
    {
        var safeValue = EscapeFormula(value);
        if (safeValue.Contains('"', StringComparison.Ordinal) ||
            safeValue.Contains(',', StringComparison.Ordinal) ||
            safeValue.Contains('\n', StringComparison.Ordinal) ||
            safeValue.Contains('\r', StringComparison.Ordinal))
        {
            return $"\"{safeValue.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return safeValue;
    }

    public static string EscapeFormula(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value[0] is '=' or '+' or '-' or '@' or '\t' or '\r'
            ? $"'{value}"
            : value;
    }
}

