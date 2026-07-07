using System.Diagnostics;
using AliasCockpit.Core.Aliases;
using AliasCockpit.Core.Generation;
using AliasCockpit.Core.ImportExport;
using AliasCockpit.Infrastructure.Storage;

var generator = new AliasGenerator();
var generated = RunGenerationBenchmark("strong-random-10k", new AliasGenerationRequest("bench.example", AliasGenerationStrategy.StrongRandom)
{
    Count = 10_000,
    MinEntropyBits = 60,
});

RunGenerationBenchmark("site-aware-10k", new AliasGenerationRequest("bench.example", AliasGenerationStrategy.SiteAware)
{
    Count = 10_000,
    MinEntropyBits = 50,
    Site = "https://github.com/openai/codex",
});

RunCsvDryRunBenchmark(generated);
await RunSqliteBenchmarkAsync(generated.Take(2_000).ToArray());

IReadOnlyList<AliasCandidate> RunGenerationBenchmark(string name, AliasGenerationRequest request)
{
    var stopwatch = Stopwatch.StartNew();
    var aliases = generator.Generate(request);
    stopwatch.Stop();

    var distinct = aliases.Select(alias => alias.Address).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    if (distinct != request.Count)
    {
        throw new InvalidOperationException($"{name} produced {request.Count - distinct} duplicate aliases.");
    }

    var aliasesPerSecond = request.Count / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001d);
    Console.WriteLine($"{name}: {request.Count:N0} aliases in {stopwatch.Elapsed.TotalMilliseconds:N1} ms ({aliasesPerSecond:N0}/s)");
    return aliases;
}

void RunCsvDryRunBenchmark(IReadOnlyList<AliasCandidate> candidates)
{
    var now = DateTimeOffset.UtcNow;
    var aliases = candidates.Select(candidate => AliasRecord.Create(
        candidate.Address,
        AliasStatus.Active,
        "Benchmark",
        "bench.example",
        "performance",
        "bench",
        candidate.EntropyBits,
        now));
    var csv = new AliasCsvExporter().Export(aliases);

    var stopwatch = Stopwatch.StartNew();
    var dryRun = new AliasCsvImporter().DryRun(csv, now);
    stopwatch.Stop();

    if (dryRun.ValidCount != candidates.Count || dryRun.ErrorCount != 0)
    {
        throw new InvalidOperationException($"csv-dry-run-10k expected {candidates.Count} valid rows but found {dryRun.ValidCount} valid and {dryRun.ErrorCount} errors.");
    }

    var rowsPerSecond = candidates.Count / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001d);
    Console.WriteLine($"csv-dry-run-10k: {candidates.Count:N0} rows in {stopwatch.Elapsed.TotalMilliseconds:N1} ms ({rowsPerSecond:N0}/s)");
}

async Task RunSqliteBenchmarkAsync(IReadOnlyList<AliasCandidate> candidates)
{
    var dbPath = Path.Combine(Path.GetTempPath(), $"alias-cockpit-bench-{Guid.NewGuid():N}.sqlite");
    try
    {
        var repository = new SqliteAliasRepository(dbPath);
        await repository.InitializeAsync();

        var now = DateTimeOffset.UtcNow;
        var insertStopwatch = Stopwatch.StartNew();
        var records = candidates.Select(candidate => AliasRecord.Create(
                candidate.Address,
                AliasStatus.Active,
                "Benchmark",
                "bench.example",
                "performance",
                "bench",
                candidate.EntropyBits,
                now)).ToArray();
        await repository.UpsertManyAsync(records);
        insertStopwatch.Stop();

        var searchStopwatch = Stopwatch.StartNew();
        var results = await repository.SearchAsync(new AliasSearchQuery("bench", Limit: 100));
        searchStopwatch.Stop();

        if (await repository.CountAsync() != candidates.Count || results.Count == 0)
        {
            throw new InvalidOperationException("sqlite-2k did not persist or search expected aliases.");
        }

        Console.WriteLine($"sqlite-upsert-2k: {candidates.Count:N0} aliases in {insertStopwatch.Elapsed.TotalMilliseconds:N1} ms");
        Console.WriteLine($"sqlite-search-bench: {results.Count:N0} hits in {searchStopwatch.Elapsed.TotalMilliseconds:N1} ms");
    }
    finally
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var path in new[] { dbPath, $"{dbPath}-wal", $"{dbPath}-shm" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}

