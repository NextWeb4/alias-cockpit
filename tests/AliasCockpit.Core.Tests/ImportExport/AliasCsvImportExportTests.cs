using AliasCockpit.Core.Aliases;
using AliasCockpit.Core.ImportExport;

namespace AliasCockpit.Core.Tests.ImportExport;

public sealed class AliasCsvImportExportTests
{
    [Fact]
    public void DryRunImportsValidAliasesWithoutWritingAnything()
    {
        const string csv = """
            address,status,provider,site,purpose,tags,color,entropy_bits
            github-abc123@example.com,Active,SimpleLogin,github.com,dev,"work,code",Blue,50
            """;

        var result = new AliasCsvImporter().DryRun(csv, DateTimeOffset.Parse("2026-07-05T00:00:00Z"));

        Assert.Equal(1, result.ValidCount);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal("github-abc123@example.com", result.Rows[0].Alias?.Address);
        Assert.Equal(AliasColor.Blue, result.Rows[0].Alias?.Color);
    }

    [Fact]
    public void DryRunReportsDuplicateAndInvalidRows()
    {
        const string csv = """
            address,status
            duplicated@example.com,Active
            duplicated@example.com,Active
            bad address,Active
            ok@example.com,Unknown
            """;

        var result = new AliasCsvImporter().DryRun(csv, DateTimeOffset.UtcNow);

        Assert.Equal(1, result.ValidCount);
        Assert.Equal(3, result.ErrorCount);
        Assert.Contains(result.Rows, row => row.Errors.Contains("Duplicate address in import file."));
        Assert.Contains(result.Rows, row => row.Errors.Any(error => error.Contains("must contain", StringComparison.OrdinalIgnoreCase) || error.Contains("unsupported", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(result.Rows, row => row.Errors.Contains("Unsupported status 'Unknown'."));
    }

    [Fact]
    public void ExportEscapesCsvAndSpreadsheetFormulaFields()
    {
        var now = DateTimeOffset.Parse("2026-07-05T00:00:00Z");
        var alias = AliasRecord.Create(
            "safe@example.com",
            AliasStatus.Active,
            "Manual",
            "=cmd|'/C calc'!A0",
            "needs, comma",
            "@sensitive",
            60,
            now,
            AliasColor.Red);

        var csv = new AliasCsvExporter().Export([alias]);

        Assert.Contains("'=cmd", csv, StringComparison.Ordinal);
        Assert.Contains("'@sensitive", csv, StringComparison.Ordinal);
        Assert.Contains("\"needs, comma\"", csv, StringComparison.Ordinal);
        Assert.Contains(",Red,60.0,", csv, StringComparison.Ordinal);
    }
}

