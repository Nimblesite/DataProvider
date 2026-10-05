namespace Nimblesite.Sync.Integration.Tests;

// Implements [SYNC-MAPPING-E2E-SETUP].

/// <summary>
/// REAL E2E HTTP tests proving LQL/MappingEngine transforms data between DBs with DIFFERENT SCHEMAS.
/// These tests hit actual HTTP endpoints using WebApplicationFactory.
/// The key proof: Source table "User" with columns (Id, FullName, EmailAddress)
/// maps to Target table "Customer" with columns (CustomerId, Name, Email).
/// THIS IS REAL MAPPING, NOT JUST COPY!
/// Uses the shared postgres container; each test gets its own database.
/// </summary>
[Collection(PostgresTestSuite.Name)]
public sealed partial class HttpMappingSyncTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private string _postgresConnectionString = null!;
    private readonly ILogger _logger = NullLogger.Instance;
    private readonly List<string> _sqliteDbPaths = [];

    public async Task InitializeAsync()
    {
        _postgresConnectionString = await fixture
            .CreateDatabaseConnectionStringAsync("mapping_test")
            .ConfigureAwait(false);
    }

    public Task DisposeAsync()
    {
        foreach (var dbPath in _sqliteDbPaths)
        {
            if (File.Exists(dbPath))
            {
                try
                {
                    File.Delete(dbPath);
                }
                catch
                { /* File may be locked */
                }
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates SQLite source DB with User table (source schema).
    /// Columns: Id, FullName, EmailAddress (DIFFERENT from target!)
    /// </summary>
    private SqliteConnection CreateSourceDb(string originId)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"mapping_source_{Guid.NewGuid()}.db");
        _sqliteDbPaths.Add(dbPath);
        var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();

        SyncSchema.CreateSchema(conn);
        SyncSchema.SetOriginId(conn, originId);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE User (
                Id TEXT PRIMARY KEY,
                FullName TEXT NOT NULL,
                EmailAddress TEXT,
                PasswordHash TEXT,
                SecurityStamp TEXT,
                CreatedAt TEXT
            );
            """;
        cmd.ExecuteNonQuery();

        TriggerGenerator.CreateTriggers(conn, "User", NullLogger.Instance);
        return conn;
    }

    private SqliteConnection CreateDefaultSourceDb(out SyncMappingConfig mappingConfig)
    {
        var source = CreateSourceDb(originId: Guid.NewGuid().ToString());
        mappingConfig = UserToCustomerConfig();
        return source;
    }

    /// <summary>
    /// Creates Postgres target DB with Customer table (target schema).
    /// Columns: CustomerId, Name, Email, Source, RegisteredDate (DIFFERENT from source!)
    /// </summary>
    private NpgsqlConnection CreateTargetDb(string originId)
    {
        var conn = new NpgsqlConnection(_postgresConnectionString);
        conn.Open();

        var schemaResult = PostgresSyncSchema.CreateSchema(conn);
        if (schemaResult is not BoolSyncOk)
        {
            throw new InvalidOperationException(
                $"Failed to create Postgres schema: {schemaResult}"
            );
        }

        var originResult = PostgresSyncSchema.SetOriginId(conn, originId);
        if (originResult is not BoolSyncOk)
        {
            throw new InvalidOperationException($"Failed to set origin ID: {originResult}");
        }

        // Target schema is DIFFERENT from source!
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DROP TABLE IF EXISTS customer CASCADE;
            CREATE TABLE customer (
                customer_id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                email TEXT,
                source TEXT,
                registered_date TEXT
            );
            """;
        cmd.ExecuteNonQuery();

        return conn;
    }

    /// <summary>
    /// Fetches all source changes, maps the first one in the push direction,
    /// asserts the mapping succeeded, and returns the first mapped entry.
    /// </summary>
    private MappedEntry MapFirstChange(SqliteConnection source, SyncMappingConfig mappingConfig)
    {
        var changes = SyncLogRepository.FetchChanges(source, 0, 100);
        var entry = ((SyncLogListOk)changes).Value[0];

        var success = MapSuccessfully(entry: entry, mappingConfig: mappingConfig);
        return success.Entries[0];
    }

    private static SyncMappingConfig UserToCustomerConfig(
        IReadOnlyList<ColumnMapping>? columnMappings = null,
        IReadOnlyList<string>? excludedColumns = null
    ) =>
        new(
            "1.0",
            UnmappedTableBehavior.Strict,
            [
                new TableMapping(
                    Id: "user-to-customer",
                    SourceTable: "User",
                    TargetTable: "customer",
                    Direction: MappingDirection.Push,
                    Enabled: true,
                    PkMapping: new PkMapping("Id", "customer_id"),
                    ColumnMappings: columnMappings
                        ?? [new("FullName", "name"), new("EmailAddress", "email")],
                    ExcludedColumns: excludedColumns ?? [],
                    Filter: null,
                    SyncTracking: new SyncTrackingConfig()
                ),
            ]
        );

    /// <summary>
    /// Helper to apply a mapped entry to PostgreSQL target table.
    /// Note: Table name is from test fixtures, not user input - safe for test code.
    /// </summary>
#pragma warning disable CA2100 // SQL from test fixtures, not user input
    private static void ApplyMappedEntryToPostgres(
        NpgsqlConnection conn,
        SyncOperation operation,
        MappedEntry mapped
    )
    {
        // Parse the mapped payload and PK
        if (operation == SyncOperation.Delete)
        {
            using var pkDoc = JsonDocument.Parse(mapped.TargetPkValue);
            var pkValue = pkDoc.RootElement.EnumerateObject().First().Value.GetString();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DELETE FROM {mapped.TargetTable} WHERE customer_id = @pk";
            cmd.Parameters.AddWithValue("pk", pkValue ?? "");
            cmd.ExecuteNonQuery();
            return;
        }

        if (mapped.MappedPayload is null)
            return;

        using var payloadDoc = JsonDocument.Parse(mapped.MappedPayload);
        using var pkValDoc = JsonDocument.Parse(mapped.TargetPkValue);

        var pkColumnValue = pkValDoc.RootElement.EnumerateObject().First();

        var columns = new List<string> { pkColumnValue.Name };
        var values = new List<string> { pkColumnValue.Value.GetString() ?? "" };

        foreach (var prop in payloadDoc.RootElement.EnumerateObject())
        {
            columns.Add(prop.Name);
            values.Add(
                prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString() ?? ""
                    : prop.Value.GetRawText()
            );
        }

        using var cmd2 = conn.CreateCommand();
        var colList = string.Join(", ", columns);
        var paramList = string.Join(", ", columns.Select((_, i) => $"@p{i}"));

        cmd2.CommandText = $"INSERT INTO {mapped.TargetTable} ({colList}) VALUES ({paramList})";

        for (var i = 0; i < values.Count; i++)
        {
            cmd2.Parameters.AddWithValue($"p{i}", values[i]);
        }

        cmd2.ExecuteNonQuery();
    }
#pragma warning restore CA2100

    private MappingSuccess MapSuccessfully(SyncLogEntry entry, SyncMappingConfig mappingConfig) =>
        Assert.IsType<MappingSuccess>(
            MappingEngine.ApplyMapping(
                entry: entry,
                config: mappingConfig,
                direction: MappingDirection.Push,
                logger: _logger
            )
        );
}
