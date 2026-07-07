using AliasCockpit.Core.Aliases;
using Microsoft.Data.Sqlite;

namespace AliasCockpit.Infrastructure.Storage;

public sealed class SqliteAliasRepository(string databasePath) : IAliasRepository
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = databasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
    }.ToString();

    static SqliteAliasRepository()
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

            CREATE TABLE IF NOT EXISTS aliases (
                id TEXT PRIMARY KEY,
                address TEXT NOT NULL UNIQUE,
                local_part TEXT NOT NULL,
                domain TEXT NOT NULL,
                status TEXT NOT NULL,
                provider TEXT NOT NULL,
                site TEXT NULL,
                purpose TEXT NULL,
                tags TEXT NOT NULL,
                color TEXT NOT NULL DEFAULT 'None',
                entropy_bits REAL NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE VIRTUAL TABLE IF NOT EXISTS alias_search USING fts5(
                alias_id UNINDEXED,
                address,
                local_part,
                domain,
                site,
                purpose,
                provider,
                tags
            );
            """, cancellationToken);

        await EnsureColumnAsync(connection, "aliases", "color", "TEXT NOT NULL DEFAULT 'None'", cancellationToken);
    }

    public async Task UpsertAsync(AliasRecord alias, CancellationToken cancellationToken = default)
    {
        await UpsertManyAsync([alias], cancellationToken);
    }

    public async Task UpsertManyAsync(IEnumerable<AliasRecord> aliases, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var upsertAlias = CreateUpsertAliasCommand(connection, (SqliteTransaction)transaction);
        await using var deleteSearch = CreateDeleteSearchCommand(connection, (SqliteTransaction)transaction);
        await using var insertSearch = CreateInsertSearchCommand(connection, (SqliteTransaction)transaction);

        foreach (var alias in aliases)
        {
            SetAliasParameterValues(upsertAlias, alias);
            await upsertAlias.ExecuteNonQueryAsync(cancellationToken);

            SetDeleteSearchParameterValues(deleteSearch, alias);
            await deleteSearch.ExecuteNonQueryAsync(cancellationToken);

            SetSearchParameterValues(insertSearch, alias);
            await insertSearch.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<AliasRecord?> GetByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        var parsed = AliasAddress.Parse(address);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, address, local_part, domain, status, provider, site, purpose, tags, color, entropy_bits, created_at, updated_at
            FROM aliases
            WHERE address = $address COLLATE NOCASE
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$address", parsed.Address);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadAlias(reader) : null;
    }

    public async Task<IReadOnlyList<AliasRecord>> SearchAsync(AliasSearchQuery query, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var limit = Math.Clamp(query.Limit, 1, 10_000);
        var ftsQuery = BuildFtsQuery(query.Text);

        await using var command = connection.CreateCommand();
        if (string.IsNullOrWhiteSpace(ftsQuery))
        {
            command.CommandText = """
                SELECT id, address, local_part, domain, status, provider, site, purpose, tags, color, entropy_bits, created_at, updated_at
                FROM aliases
                ORDER BY updated_at DESC
                LIMIT $limit;
                """;
        }
        else
        {
            command.CommandText = """
                SELECT a.id, a.address, a.local_part, a.domain, a.status, a.provider, a.site, a.purpose, a.tags, a.color, a.entropy_bits, a.created_at, a.updated_at
                FROM alias_search s
                JOIN aliases a ON a.id = s.alias_id
                WHERE alias_search MATCH $query
                ORDER BY rank
                LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$query", ftsQuery);
        }

        command.Parameters.AddWithValue("$limit", limit);

        var aliases = new List<AliasRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            aliases.Add(ReadAlias(reader));
        }

        return aliases;
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM aliases;";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
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

    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string table,
        string column,
        string definition,
        CancellationToken cancellationToken)
    {
        await using var inspect = connection.CreateCommand();
        inspect.CommandText = $"PRAGMA table_info({table});";
        await using (var reader = await inspect.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }

        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }

    private static SqliteCommand CreateUpsertAliasCommand(SqliteConnection connection, SqliteTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO aliases (
                id, address, local_part, domain, status, provider, site, purpose, tags, color, entropy_bits, created_at, updated_at
            )
            VALUES (
                $id, $address, $local_part, $domain, $status, $provider, $site, $purpose, $tags, $color, $entropy_bits, $created_at, $updated_at
            )
            ON CONFLICT(address) DO UPDATE SET
                id = excluded.id,
                local_part = excluded.local_part,
                domain = excluded.domain,
                status = excluded.status,
                provider = excluded.provider,
                site = excluded.site,
                purpose = excluded.purpose,
                tags = excluded.tags,
                color = excluded.color,
                entropy_bits = excluded.entropy_bits,
                created_at = excluded.created_at,
                updated_at = excluded.updated_at;
            """;
        AddAliasParameters(command);
        command.Prepare();
        return command;
    }

    private static SqliteCommand CreateDeleteSearchCommand(SqliteConnection connection, SqliteTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM alias_search WHERE alias_id = $id OR address = $address;";
        command.Parameters.Add("$id", SqliteType.Text);
        command.Parameters.Add("$address", SqliteType.Text);
        command.Prepare();
        return command;
    }

    private static SqliteCommand CreateInsertSearchCommand(SqliteConnection connection, SqliteTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO alias_search(alias_id, address, local_part, domain, site, purpose, provider, tags)
            VALUES ($id, $address, $local_part, $domain, $site, $purpose, $provider, $tags);
            """;
        AddSearchParameters(command);
        command.Prepare();
        return command;
    }

    private static void AddAliasParameters(SqliteCommand command)
    {
        command.Parameters.Add("$id", SqliteType.Text);
        command.Parameters.Add("$address", SqliteType.Text);
        command.Parameters.Add("$local_part", SqliteType.Text);
        command.Parameters.Add("$domain", SqliteType.Text);
        command.Parameters.Add("$status", SqliteType.Text);
        command.Parameters.Add("$provider", SqliteType.Text);
        command.Parameters.Add("$site", SqliteType.Text);
        command.Parameters.Add("$purpose", SqliteType.Text);
        command.Parameters.Add("$tags", SqliteType.Text);
        command.Parameters.Add("$color", SqliteType.Text);
        command.Parameters.Add("$entropy_bits", SqliteType.Real);
        command.Parameters.Add("$created_at", SqliteType.Text);
        command.Parameters.Add("$updated_at", SqliteType.Text);
    }

    private static void AddSearchParameters(SqliteCommand command)
    {
        command.Parameters.Add("$id", SqliteType.Text);
        command.Parameters.Add("$address", SqliteType.Text);
        command.Parameters.Add("$local_part", SqliteType.Text);
        command.Parameters.Add("$domain", SqliteType.Text);
        command.Parameters.Add("$site", SqliteType.Text);
        command.Parameters.Add("$purpose", SqliteType.Text);
        command.Parameters.Add("$provider", SqliteType.Text);
        command.Parameters.Add("$tags", SqliteType.Text);
    }

    private static void SetAliasParameterValues(SqliteCommand command, AliasRecord alias)
    {
        command.Parameters["$id"].Value = alias.Id.ToString("D");
        command.Parameters["$address"].Value = alias.Address;
        command.Parameters["$local_part"].Value = alias.LocalPart;
        command.Parameters["$domain"].Value = alias.Domain;
        command.Parameters["$status"].Value = alias.Status.ToString();
        command.Parameters["$provider"].Value = alias.Provider;
        command.Parameters["$site"].Value = (object?)alias.Site ?? DBNull.Value;
        command.Parameters["$purpose"].Value = (object?)alias.Purpose ?? DBNull.Value;
        command.Parameters["$tags"].Value = alias.Tags;
        command.Parameters["$color"].Value = alias.Color.ToString();
        command.Parameters["$entropy_bits"].Value = alias.EntropyBits;
        command.Parameters["$created_at"].Value = alias.CreatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        command.Parameters["$updated_at"].Value = alias.UpdatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void SetDeleteSearchParameterValues(SqliteCommand command, AliasRecord alias)
    {
        command.Parameters["$id"].Value = alias.Id.ToString("D");
        command.Parameters["$address"].Value = alias.Address;
    }

    private static void SetSearchParameterValues(SqliteCommand command, AliasRecord alias)
    {
        command.Parameters["$id"].Value = alias.Id.ToString("D");
        command.Parameters["$address"].Value = alias.Address;
        command.Parameters["$local_part"].Value = alias.LocalPart;
        command.Parameters["$domain"].Value = alias.Domain;
        command.Parameters["$site"].Value = (object?)alias.Site ?? DBNull.Value;
        command.Parameters["$purpose"].Value = (object?)alias.Purpose ?? DBNull.Value;
        command.Parameters["$provider"].Value = alias.Provider;
        command.Parameters["$tags"].Value = alias.Tags;
    }

    private static AliasRecord ReadAlias(SqliteDataReader reader)
    {
        return new AliasRecord(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            Enum.Parse<AliasStatus>(reader.GetString(4)),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.GetString(8),
            ParseColor(reader.GetString(9)),
            reader.GetDouble(10),
            DateTimeOffset.Parse(reader.GetString(11), System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(reader.GetString(12), System.Globalization.CultureInfo.InvariantCulture));
    }

    private static AliasColor ParseColor(string value)
    {
        return Enum.TryParse<AliasColor>(value, ignoreCase: true, out var color)
            ? color
            : AliasColor.None;
    }

    private static string BuildFtsQuery(string text)
    {
        var tokens = text.Split([' ', '\t', '\r', '\n', ':', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => new string(token.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant())
            .Where(token => token.Length > 0)
            .Take(8)
            .Select(token => $"{token}*");

        return string.Join(' ', tokens);
    }
}
