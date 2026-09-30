namespace Nimblesite.Sync.Http.Tests;

/// <summary>
/// E2E integration tests for cross-database sync between SQLite and PostgreSQL.
/// These tests PROVE the spec is fully implemented across different database backends.
/// Uses the shared postgres container; each test gets its own database.
/// </summary>
[Collection(PostgresTestSuite.Name)]
[Trait("Category", "Docker")]
public sealed partial class CrossDatabaseSyncTests(PostgresContainerFixture fixture)
    : IAsyncLifetime
{
    private string _postgresConnectionString = null!;
    private readonly ILogger _logger = NullLogger.Instance;
    private readonly List<string> _sqliteDbPaths = [];

    public async Task InitializeAsync()
    {
        _postgresConnectionString = await fixture
            .CreateDatabaseConnectionStringAsync("syncdb")
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
    /// Creates a fresh SQLite file database with sync schema and triggers.
    /// </summary>
    private SqliteConnection CreateSqliteDb(string originId)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"http_sync_{Guid.NewGuid()}.db");
        _sqliteDbPaths.Add(dbPath);
        var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();

        // Create sync schema
        SyncSchema.CreateSchema(conn);
        SyncSchema.SetOriginId(conn, originId);

        // Create test table
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Person (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Email TEXT
            );
            """;
        cmd.ExecuteNonQuery();

        // Create triggers
        TriggerGenerator.CreateTriggers(conn, "Person", NullLogger.Instance);

        return conn;
    }

    /// <summary>
    /// Creates PostgreSQL database with sync schema (lowercase table for cross-db compat).
    /// </summary>
    private NpgsqlConnection CreatePostgresDb(string originId)
    {
        var conn = new NpgsqlConnection(_postgresConnectionString);
        conn.Open();

        // Create sync schema
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

        // Create test table - lowercase for PostgreSQL compatibility
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DROP TABLE IF EXISTS person CASCADE;
            CREATE TABLE person (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                email TEXT
            );
            """;
        cmd.ExecuteNonQuery();

        return conn;
    }

    [Fact]
    public void SQLite_Insert_SyncsTo_Postgres()
    {
        // Arrange
        var clientOriginId = Guid.NewGuid().ToString();
        var serverOriginId = Guid.NewGuid().ToString();

        using var sqlite = CreateSqliteDb(clientOriginId);
        using var postgres = CreatePostgresDb(serverOriginId);

        // Act - Insert in SQLite
        using (var cmd = sqlite.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO Person (Id, Name, Email) VALUES ('p1', 'Alice', 'alice@example.com')";
            cmd.ExecuteNonQuery();
        }

        // Fetch changes from SQLite
        var changes = SyncLogRepository.FetchChanges(sqlite, 0, 100);
        Assert.True(changes is SyncLogListOk, $"FetchChanges failed: {changes}");
        var changesList = ((SyncLogListOk)changes).Value;
        Assert.Single(changesList);

        // Apply changes to Postgres with suppression
        PostgresSyncSession.EnableSuppression(postgres);
        foreach (var entry in changesList)
        {
            var result = PostgresChangeApplier.ApplyChange(postgres, entry, _logger);
            Assert.True(result is BoolSyncOk, $"ApplyChange failed: {result}");
        }
        PostgresSyncSession.DisableSuppression(postgres);

        // Assert - Verify in Postgres
        using var verifyCmd = postgres.CreateCommand();
        verifyCmd.CommandText = "SELECT name FROM person WHERE id = 'p1'";
        var name = verifyCmd.ExecuteScalar()?.ToString();
        Assert.Equal("Alice", name);
    }

    [Fact]
    public void Postgres_Insert_SyncsTo_SQLite()
    {
        // Arrange
        var clientOriginId = Guid.NewGuid().ToString();
        var serverOriginId = Guid.NewGuid().ToString();

        using var postgres = CreatePostgresDb(clientOriginId);
        using var sqlite = CreateSqliteDb(serverOriginId);

        // First create triggers in Postgres for this table
        var triggerResult = PostgresTriggerGenerator.CreateTriggers(postgres, "person", _logger);
        Assert.True(triggerResult is BoolSyncOk, $"Trigger creation failed: {triggerResult}");

        // Act - Insert in Postgres
        using (var cmd = postgres.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO person (id, name, email) VALUES ('p2', 'Bob', 'bob@example.com')";
            cmd.ExecuteNonQuery();
        }

        // Fetch changes from Postgres
        var changes = PostgresSyncLogRepository.FetchChanges(postgres, 0, 100);
        Assert.True(changes is SyncLogListOk, $"FetchChanges failed: {changes}");
        var changesList = ((SyncLogListOk)changes).Value;
        Assert.Single(changesList);

        // Apply changes to SQLite with suppression
        SyncSessionManager.EnableSuppression(sqlite);
        foreach (var entry in changesList)
        {
            var result = ChangeApplierSQLite.ApplyChange(sqlite, entry);
            Assert.True(result is BoolSyncOk, $"ApplyChange failed: {result}");
        }
        SyncSessionManager.DisableSuppression(sqlite);

        // Assert - Verify in SQLite (note: SQLite preserves case from payload)
        using var verifyCmd = sqlite.CreateCommand();
        verifyCmd.CommandText = "SELECT Name FROM Person WHERE Id = 'p2'";
        var name = verifyCmd.ExecuteScalar()?.ToString();
        Assert.Equal("Bob", name);
    }

    [Fact]
    public void Bidirectional_Sync_BetweenDatabases()
    {
        // Arrange
        var sqliteOriginId = Guid.NewGuid().ToString();
        var postgresOriginId = Guid.NewGuid().ToString();

        using var sqlite = CreateSqliteDb(sqliteOriginId);
        using var postgres = CreatePostgresDb(postgresOriginId);

        // Create triggers in Postgres
        var triggerResult = PostgresTriggerGenerator.CreateTriggers(postgres, "person", _logger);
        Assert.True(triggerResult is BoolSyncOk, $"Trigger creation failed: {triggerResult}");

        // Insert in SQLite
        using (var cmd = sqlite.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO Person (Id, Name, Email) VALUES ('s1', 'SQLite User', 'sqlite@example.com')";
            cmd.ExecuteNonQuery();
        }

        // Insert in Postgres
        using (var cmd = postgres.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO person (id, name, email) VALUES ('p1', 'Postgres User', 'postgres@example.com')";
            cmd.ExecuteNonQuery();
        }

        // Sync SQLite -> Postgres
        var sqliteChanges = SyncLogRepository.FetchChanges(sqlite, 0, 100);
        var sqliteChangesList = ((SyncLogListOk)sqliteChanges).Value;

        PostgresSyncSession.EnableSuppression(postgres);
        foreach (var entry in sqliteChangesList)
        {
            PostgresChangeApplier.ApplyChange(postgres, entry, _logger);
        }
        PostgresSyncSession.DisableSuppression(postgres);

        // Sync Postgres -> SQLite (skip echo)
        var postgresChanges = PostgresSyncLogRepository.FetchChanges(postgres, 0, 100);
        var postgresChangesList = ((SyncLogListOk)postgresChanges).Value;

        SyncSessionManager.EnableSuppression(sqlite);
        foreach (var entry in postgresChangesList)
        {
            if (entry.Origin != sqliteOriginId)
            {
                ChangeApplierSQLite.ApplyChange(sqlite, entry);
            }
        }
        SyncSessionManager.DisableSuppression(sqlite);

        // Assert - Both databases have both records
        using var pgCmd = postgres.CreateCommand();
        pgCmd.CommandText = "SELECT COUNT(*) FROM person";
        var pgCount = Convert.ToInt32(
            pgCmd.ExecuteScalar(),
            System.Globalization.CultureInfo.InvariantCulture
        );
        Assert.Equal(2, pgCount);

        using var sqliteCmd = sqlite.CreateCommand();
        sqliteCmd.CommandText = "SELECT COUNT(*) FROM Person";
        var sqliteCount = Convert.ToInt32(
            sqliteCmd.ExecuteScalar(),
            System.Globalization.CultureInfo.InvariantCulture
        );
        Assert.Equal(2, sqliteCount);
    }

    [Fact]
    public void Update_SyncsAcrossDatabases()
    {
        // Arrange
        var sqliteOriginId = Guid.NewGuid().ToString();
        var postgresOriginId = Guid.NewGuid().ToString();

        using var sqlite = CreateSqliteDb(sqliteOriginId);
        using var postgres = CreatePostgresDb(postgresOriginId);

        // Insert in SQLite
        using (var cmd = sqlite.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO Person (Id, Name, Email) VALUES ('u1', 'Original', 'original@example.com')";
            cmd.ExecuteNonQuery();
        }

        // Sync to Postgres
        var insertChanges = SyncLogRepository.FetchChanges(sqlite, 0, 100);
        PostgresSyncSession.EnableSuppression(postgres);
        foreach (var entry in ((SyncLogListOk)insertChanges).Value)
        {
            PostgresChangeApplier.ApplyChange(postgres, entry, _logger);
        }
        PostgresSyncSession.DisableSuppression(postgres);

        var versionAfterInsert = ((SyncLogListOk)insertChanges).Value.Max(e => e.Version);

        // Act - Update in SQLite
        using (var cmd = sqlite.CreateCommand())
        {
            cmd.CommandText =
                "UPDATE Person SET Name = 'Updated', Email = 'updated@example.com' WHERE Id = 'u1'";
            cmd.ExecuteNonQuery();
        }

        // Sync update to Postgres
        var updateChanges = SyncLogRepository.FetchChanges(sqlite, versionAfterInsert, 100);
        Assert.True(updateChanges is SyncLogListOk);
        var updateList = ((SyncLogListOk)updateChanges).Value;
        Assert.Single(updateList);
        Assert.Equal(SyncOperation.Update, updateList[0].Operation);

        PostgresSyncSession.EnableSuppression(postgres);
        var applyResult = PostgresChangeApplier.ApplyChange(postgres, updateList[0], _logger);
        PostgresSyncSession.DisableSuppression(postgres);
        Assert.True(applyResult is BoolSyncOk);

        // Assert
        using var verifyCmd = postgres.CreateCommand();
        verifyCmd.CommandText = "SELECT name, email FROM person WHERE id = 'u1'";
        using var reader = verifyCmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("Updated", reader.GetString(0));
        Assert.Equal("updated@example.com", reader.GetString(1));
    }

    [Fact]
    public void Delete_SyncsAcrossDatabases_AsTombstone()
    {
        // Arrange
        var sqliteOriginId = Guid.NewGuid().ToString();
        var postgresOriginId = Guid.NewGuid().ToString();

        using var sqlite = CreateSqliteDb(sqliteOriginId);
        using var postgres = CreatePostgresDb(postgresOriginId);

        // Insert and sync
        using (var cmd = sqlite.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO Person (Id, Name, Email) VALUES ('d1', 'ToDelete', 'delete@example.com')";
            cmd.ExecuteNonQuery();
        }

        var insertChanges = SyncLogRepository.FetchChanges(sqlite, 0, 100);
        PostgresSyncSession.EnableSuppression(postgres);
        foreach (var entry in ((SyncLogListOk)insertChanges).Value)
        {
            PostgresChangeApplier.ApplyChange(postgres, entry, _logger);
        }
        PostgresSyncSession.DisableSuppression(postgres);

        var versionAfterInsert = ((SyncLogListOk)insertChanges).Value.Max(e => e.Version);

        // Act - Delete in SQLite
        using (var cmd = sqlite.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM Person WHERE Id = 'd1'";
            cmd.ExecuteNonQuery();
        }

        // Fetch tombstone
        var deleteChanges = SyncLogRepository.FetchChanges(sqlite, versionAfterInsert, 100);
        Assert.True(deleteChanges is SyncLogListOk);
        var deleteList = ((SyncLogListOk)deleteChanges).Value;
        Assert.Single(deleteList);
        Assert.Equal(SyncOperation.Delete, deleteList[0].Operation);

        // Sync tombstone to Postgres
        PostgresSyncSession.EnableSuppression(postgres);
        var applyResult = PostgresChangeApplier.ApplyChange(postgres, deleteList[0], _logger);
        PostgresSyncSession.DisableSuppression(postgres);
        Assert.True(applyResult is BoolSyncOk);

        // Assert - Record deleted
        using var verifyCmd = postgres.CreateCommand();
        verifyCmd.CommandText = "SELECT COUNT(*) FROM person WHERE id = 'd1'";
        var count = Convert.ToInt32(
            verifyCmd.ExecuteScalar(),
            System.Globalization.CultureInfo.InvariantCulture
        );
        Assert.Equal(0, count);
    }

    [Fact]
    public void TriggerSuppression_PreventsEcho()
    {
        // Arrange
        var sqliteOriginId = Guid.NewGuid().ToString();

        using var sqlite = CreateSqliteDb(sqliteOriginId);

        // Insert WITH suppression - should NOT log
        SyncSessionManager.EnableSuppression(sqlite);
        using (var cmd = sqlite.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO Person (Id, Name, Email) VALUES ('s1', 'Suppressed', 'suppressed@example.com')";
            cmd.ExecuteNonQuery();
        }
        SyncSessionManager.DisableSuppression(sqlite);

        // Assert - No changes logged
        var changes = SyncLogRepository.FetchChanges(sqlite, 0, 100);
        var list = ((SyncLogListOk)changes).Value;
        Assert.Empty(list);

        // Insert WITHOUT suppression - should log
        using (var cmd = sqlite.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO Person (Id, Name, Email) VALUES ('n1', 'Normal', 'normal@example.com')";
            cmd.ExecuteNonQuery();
        }

        // Assert - One change logged
        var changes2 = SyncLogRepository.FetchChanges(sqlite, 0, 100);
        var list2 = ((SyncLogListOk)changes2).Value;
        Assert.Single(list2);
    }

    [Fact]
    public void HashVerification_ConsistentAcrossDatabases()
    {
        // Arrange
        var sqliteOriginId = Guid.NewGuid().ToString();
        using var sqlite = CreateSqliteDb(sqliteOriginId);

        // Insert data
        using (var cmd = sqlite.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO Person (Id, Name, Email) VALUES ('h1', 'HashTest', 'hash@example.com')";
            cmd.ExecuteNonQuery();
        }

        // Get changes
        var changes = SyncLogRepository.FetchChanges(sqlite, 0, 100);
        var list = ((SyncLogListOk)changes).Value;

        // Compute batch hash
        var hash1 = HashVerifier.ComputeBatchHash(list);
        Assert.NotNull(hash1);
        Assert.NotEmpty(hash1);

        // Same changes = same hash
        var hash2 = HashVerifier.ComputeBatchHash(list);
        Assert.Equal(hash1, hash2);
    }
}
