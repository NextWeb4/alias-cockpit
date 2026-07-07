using AliasCockpit.Core.Aliases;

namespace AliasCockpit.Core.ImportExport;

public sealed class AliasCsvImporter
{
    private static readonly string[] RequiredHeaders = ["address"];

    public AliasCsvDryRun DryRun(string csv, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return new AliasCsvDryRun([new AliasCsvRowResult(1, null, ["CSV content is empty."])]);
        }

        var rows = CsvRows.Parse(csv).ToList();
        if (rows.Count == 0)
        {
            return new AliasCsvDryRun([new AliasCsvRowResult(1, null, ["CSV content is empty."])]);
        }

        var headers = rows[0].Select(header => header.Trim().ToLowerInvariant()).ToArray();
        var missingHeaders = RequiredHeaders.Where(required => !headers.Contains(required, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (missingHeaders.Length > 0)
        {
            return new AliasCsvDryRun([new AliasCsvRowResult(1, null, [$"Missing required header(s): {string.Join(", ", missingHeaders)}."])]);
        }

        var results = new List<AliasCsvRowResult>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < rows.Count; i++)
        {
            var rowNumber = i + 1;
            var values = ToDictionary(headers, rows[i]);
            var errors = new List<string>();
            var address = values.GetValueOrDefault("address", string.Empty);

            AliasRecord? alias = null;
            try
            {
                if (!seen.Add(address.Trim()))
                {
                    errors.Add("Duplicate address in import file.");
                }

                var status = ParseStatus(values.GetValueOrDefault("status", "Active"), errors);
                alias = AliasRecord.Create(
                    address,
                    status,
                    values.GetValueOrDefault("provider", "Manual"),
                    values.GetValueOrDefault("site"),
                    values.GetValueOrDefault("purpose"),
                    values.GetValueOrDefault("tags", string.Empty),
                    ParseEntropy(values.GetValueOrDefault("entropy_bits"), errors),
                    now,
                    ParseColor(values.GetValueOrDefault("color"), errors));
            }
            catch (ArgumentException ex)
            {
                errors.Add(ex.Message);
            }

            results.Add(new AliasCsvRowResult(rowNumber, errors.Count == 0 ? alias : null, errors));
        }

        return new AliasCsvDryRun(results);
    }

    private static Dictionary<string, string> ToDictionary(IReadOnlyList<string> headers, IReadOnlyList<string> row)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            values[headers[i]] = i < row.Count ? row[i] : string.Empty;
        }

        return values;
    }

    private static AliasStatus ParseStatus(string? value, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return AliasStatus.Active;
        }

        if (Enum.TryParse<AliasStatus>(value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal), ignoreCase: true, out var status))
        {
            return status;
        }

        errors.Add($"Unsupported status '{value}'.");
        return AliasStatus.Active;
    }

    private static double ParseEntropy(string? value, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        if (double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var entropy) && entropy >= 0)
        {
            return entropy;
        }

        errors.Add($"Invalid entropy_bits '{value}'.");
        return 0;
    }

    private static AliasColor ParseColor(string? value, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return AliasColor.None;
        }

        if (Enum.TryParse<AliasColor>(value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal), ignoreCase: true, out var color))
        {
            return color;
        }

        errors.Add($"Unsupported color '{value}'.");
        return AliasColor.None;
    }
}

