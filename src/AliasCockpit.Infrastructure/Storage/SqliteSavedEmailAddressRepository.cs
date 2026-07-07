using AliasCockpit.Core.Tools;
using Microsoft.Data.Sqlite;

namespace AliasCockpit.Infrastructure.Storage;

public sealed class SqliteSavedEmailAddressRepository(string databasePath) : ISavedEmailAddressRepository
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = databasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
    }.ToString();

    static SqliteSavedEmailAddressRepository()
    {
        SQLitePCL.Batteries_V2.Init();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA temp_store = MEMORY;
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS saved_email_addresses (
                address TEXT PRIMARY KEY,
                created_at TEXT NOT NULL,
                last_used_at TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpsertAsync(SavedEmailAddress address, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO saved_email_addresses (address, created_at, last_used_at)
            VALUES ($address, $created_at, $last_used_at)
            ON CONFLICT(address) DO UPDATE SET
                last_used_at = excluded.last_used_at;
            """;
        command.Parameters.AddWithValue("$address", address.Address.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("$created_at", address.CreatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$last_used_at", address.LastUsedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SavedEmailAddress>> ListAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT address, created_at, last_used_at
            FROM saved_email_addresses
            ORDER BY last_used_at DESC, address ASC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 500));

        var addresses = new List<SavedEmailAddress>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            addresses.Add(new SavedEmailAddress(
                reader.GetString(0),
                DateTimeOffset.Parse(reader.GetString(1), System.Globalization.CultureInfo.InvariantCulture),
                DateTimeOffset.Parse(reader.GetString(2), System.Globalization.CultureInfo.InvariantCulture)));
        }

        return addresses;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
