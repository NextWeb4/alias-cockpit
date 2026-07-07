using AliasCockpit.Core.Aliases;

namespace AliasCockpit.Core.ImportExport;

public sealed record AliasCsvDryRun(IReadOnlyList<AliasCsvRowResult> Rows)
{
    public int ValidCount => Rows.Count(row => row.IsValid);

    public int ErrorCount => Rows.Count(row => !row.IsValid);
}

public sealed record AliasCsvRowResult(int RowNumber, AliasRecord? Alias, IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0 && Alias is not null;
}

