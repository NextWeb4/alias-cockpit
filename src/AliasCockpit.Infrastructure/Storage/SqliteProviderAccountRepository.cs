using AliasCockpit.Core.Providers;
using Microsoft.Data.Sqlite;

namespace AliasCockpit.Infrastructure.Storage;

public sealed class SqliteProviderAccountRepository(string databasePath) : IProviderAccountRepository
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = databasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
    }.ToString();

    static SqliteProviderAccountRepository()
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

            CREATE TABLE IF NOT EXISTS provider_accounts (
                id TEXT PRIMARY KEY,
                provider_type TEXT NOT NULL,
                display_name TEXT NOT NULL,
                secret_ref TEXT NOT NULL UNIQUE,
                auth_state TEXT NOT NULL,
                security_state TEXT NOT NULL,
                last_sync_at TEXT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_provider_accounts_provider_type
            ON provider_accounts(provider_type, display_name);
            """, cancellationToken);
    }

    public async Task UpsertAsync(ProviderAccount account, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO provider_accounts (
                id, provider_type, display_name, secret_ref, auth_state, security_state, last_sync_at, created_at, updated_at
            )
            VALUES (
                $id, $provider_type, $display_name, $secret_ref, $auth_state, $security_state, $last_sync_at, $created_at, $updated_at
            )
            ON CONFLICT(id) DO UPDATE SET
                provider_type = excluded.provider_type,
                display_name = excluded.display_name,
                secret_ref = excluded.secret_ref,
                auth_state = excluded.auth_state,
                security_state = excluded.security_state,
                last_sync_at = excluded.last_sync_at,
                updated_at = excluded.updated_at;
            """;
        AddAccountParameters(command);
        SetAccountParameterValues(command, account);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ProviderAccount?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, provider_type, display_name, secret_ref, auth_state, security_state, last_sync_at, created_at, updated_at
            FROM provider_accounts
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadAccount(reader) : null;
    }

    public async Task<IReadOnlyList<ProviderAccount>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, provider_type, display_name, secret_ref, auth_state, security_state, last_sync_at, created_at, updated_at
            FROM provider_accounts
            ORDER BY display_name COLLATE NOCASE, provider_type;
            """;

        var accounts = new List<ProviderAccount>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            accounts.Add(ReadAccount(reader));
        }

        return accounts;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM provider_accounts WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
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

    private static void AddAccountParameters(SqliteCommand command)
    {
        command.Parameters.Add("$id", SqliteType.Text);
        command.Parameters.Add("$provider_type", SqliteType.Text);
        command.Parameters.Add("$display_name", SqliteType.Text);
        command.Parameters.Add("$secret_ref", SqliteType.Text);
        command.Parameters.Add("$auth_state", SqliteType.Text);
        command.Parameters.Add("$security_state", SqliteType.Text);
        command.Parameters.Add("$last_sync_at", SqliteType.Text);
        command.Parameters.Add("$created_at", SqliteType.Text);
        command.Parameters.Add("$updated_at", SqliteType.Text);
    }

    private static void SetAccountParameterValues(SqliteCommand command, ProviderAccount account)
    {
        command.Parameters["$id"].Value = account.Id.ToString("D");
        command.Parameters["$provider_type"].Value = account.ProviderType;
        command.Parameters["$display_name"].Value = account.DisplayName;
        command.Parameters["$secret_ref"].Value = account.SecretRef;
        command.Parameters["$auth_state"].Value = account.AuthState.ToString();
        command.Parameters["$security_state"].Value = account.SecurityState.ToString();
        command.Parameters["$last_sync_at"].Value = account.LastSyncAt is null
            ? DBNull.Value
            : account.LastSyncAt.Value.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        command.Parameters["$created_at"].Value = account.CreatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        command.Parameters["$updated_at"].Value = account.UpdatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static ProviderAccount ReadAccount(SqliteDataReader reader)
    {
        return new ProviderAccount(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            Enum.Parse<ProviderAuthState>(reader.GetString(4)),
            Enum.Parse<ProviderSecurityState>(reader.GetString(5)),
            reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6), System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(reader.GetString(7), System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(reader.GetString(8), System.Globalization.CultureInfo.InvariantCulture));
    }
}
