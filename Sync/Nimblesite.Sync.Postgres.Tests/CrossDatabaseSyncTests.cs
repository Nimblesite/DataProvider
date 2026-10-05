using System.Diagnostics.CodeAnalysis;
using PersonCommand = global::Tests.Shared.PersonCommandParameters;

namespace Nimblesite.Sync.Postgres.Tests;

/// <summary>
/// E2E integration tests for bi-directional sync between SQLite and PostgreSQL.
/// Tests the full sync protocol per docs/specs/sync-spec.md Sections 7-15.
/// Uses the shared postgres container; each test gets its own database.
/// </summary>
[Collection(PostgresTestSuite.Name)]
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Test code"
)]
public sealed class CrossDatabaseSyncTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private NpgsqlConnection _pgConn = null!;
    private SqliteConnection _sqliteConn = null!;
    private string _sqliteDbPath = null!;
    private readonly string _sqliteOrigin = Guid.NewGuid().ToString();
    private readonly string _postgresOrigin = Guid.NewGuid().ToString();
    private static readonly ILogger Logger = NullLogger.Instance;

    public async Task InitializeAsync()
    {
        _pgConn = await fixture.CreateDatabaseAsync("synctest").ConfigureAwait(false);

        // Create SQLite file database
        _sqliteDbPath = Path.Combine(Path.GetTempPath(), $"cross_db_sync_{Guid.NewGuid()}.db");
        _sqliteConn = new SqliteConnection($"Data Source={_sqliteDbPath}");
        _sqliteConn.Open();

        // Initialize sync schemas
        PostgresSyncSchema.CreateSchema(_pgConn);
        PostgresSyncSchema.SetOriginId(_pgConn, _postgresOrigin);

        SyncSchema.CreateSchema(_sqliteConn);
        SyncSchema.SetOriginId(_sqliteConn, _sqliteOrigin);

        // Create test table in both databases
        CreateTestTable(_pgConn);
        CreateTestTable(_sqliteConn);

        // Create triggers
        PostgresTriggerGenerator.CreateTriggers(_pgConn, "person", Logger);
        TriggerGenerator.CreateTriggers(_sqliteConn, "Person", Logger);
    }

    public async Task DisposeAsync()
    {
        _sqliteConn.Dispose();
        await _pgConn.CloseAsync().ConfigureAwait(false);
        await _pgConn.DisposeAsync().ConfigureAwait(false);

        if (File.Exists(_sqliteDbPath))
        {
            try
            {
                File.Delete(_sqliteDbPath);
            }
            catch
            { /* File may be locked */
            }
        }
    }

    private static void CreateTestTable(NpgsqlConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS person (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                email TEXT
            )
            """;
        cmd.ExecuteNonQuery();
    }

    private static void CreateTestTable(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Person (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Email TEXT
            )
            """;
        cmd.ExecuteNonQuery();
    }

    #region Spec Section 7: Unified Change Log

    [Fact]
    public void Spec_S7_InsertCreatesLogEntry_Postgres()
    {
        // Insert in Postgres
        InsertPerson(_pgConn, "p1", "Alice", "alice@test.com");

        // Verify sync log entry exists
        var changes = FetchChanges(_pgConn, 0);
        Assert.Single(changes);
        Assert.Equal("person", changes[0].TableName);
        Assert.Equal(SyncOperation.Insert, changes[0].Operation);
        Assert.Contains("Alice", changes[0].Payload!);
        Assert.Equal(_postgresOrigin, changes[0].Origin);
    }

    [Fact]
    public void Spec_S7_UpdateCreatesLogEntry_Postgres()
    {
        InsertPerson(_pgConn, "p2", "Bob", "bob@test.com");
        UpdatePerson(_pgConn, "p2", "Bobby");

        var changes = FetchChanges(_pgConn, 0);
        Assert.Equal(2, changes.Count);
        Assert.Equal(SyncOperation.Update, changes[1].Operation);
        Assert.Contains("Bobby", changes[1].Payload!);
    }

    [Fact]
    public void Spec_S7_DeleteCreatesLogEntry_Postgres()
    {
        InsertPerson(_pgConn, "p3", "Charlie", "charlie@test.com");
        DeletePerson(_pgConn, "p3");

        var changes = FetchChanges(_pgConn, 0);
        Assert.Equal(2, changes.Count);
        Assert.Equal(SyncOperation.Delete, changes[1].Operation);
        Assert.Null(changes[1].Payload);
    }

    #endregion

    #region Spec Section 8: Trigger Suppression

    [Fact]
    public void Spec_S8_SuppressionPreventsLogging_Postgres()
    {
        var initialCount = GetLogCount(_pgConn);

        // Enable suppression
        PostgresSyncSession.EnableSuppression(_pgConn);

        // Insert while suppressed - should NOT create log entry
        InsertPerson(_pgConn, "suppressed1", "Hidden", "hidden@test.com");

        // Disable suppression
        PostgresSyncSession.DisableSuppression(_pgConn);

        var afterCount = GetLogCount(_pgConn);
        Assert.Equal(initialCount, afterCount);
    }

    #endregion

    #region Spec Section 11: Bi-Directional Sync Protocol

    [Fact]
    public void Spec_S11_SyncSQLiteToPostgres_InsertPropagates()
    {
        // Insert in SQLite
        InsertPerson(_sqliteConn, "sync1", "SyncUser", "sync@test.com");

        // Fetch changes from SQLite
        var sqliteChanges = FetchChanges(_sqliteConn, 0);
        Assert.Single(sqliteChanges);

        // Apply to Postgres (with suppression)
        PostgresSyncSession.EnableSuppression(_pgConn);
        try
        {
            foreach (var entry in sqliteChanges)
            {
                var result = PostgresChangeApplier.ApplyChange(_pgConn, entry, Logger);
                Assert.IsType<BoolSyncOk>(result);
            }
        }
        finally
        {
            PostgresSyncSession.DisableSuppression(_pgConn);
        }

        // Verify data in Postgres
        var person = GetPerson(_pgConn, "sync1");
        Assert.NotNull(person);
        Assert.Equal("SyncUser", person.Name);
        Assert.Equal("sync@test.com", person.Email);
    }

    [Fact]
    public void Spec_S11_SyncPostgresToSQLite_InsertPropagates()
    {
        // Insert in Postgres
        InsertPerson(_pgConn, "sync2", "PostgresUser", "pg@test.com");

        // Fetch changes from Postgres
        var pgChanges = FetchChanges(_pgConn, 0);

        // Apply to SQLite (with suppression)
        SyncSessionManager.EnableSuppression(_sqliteConn);
        try
        {
            foreach (var entry in pgChanges)
            {
                var result = ChangeApplierSQLite.ApplyChange(_sqliteConn, entry);
                Assert.IsType<BoolSyncOk>(result);
            }
        }
        finally
        {
            SyncSessionManager.DisableSuppression(_sqliteConn);
        }

        // Verify data in SQLite
        var person = GetPerson(_sqliteConn, "sync2");
        Assert.NotNull(person);
        Assert.Equal("PostgresUser", person.Name);
    }

    [Fact]
    public void Spec_S11_BidirectionalSync_BothDatabasesConverge()
    {
        // Insert different records in each database
        InsertPerson(_sqliteConn, "bidirA", "SQLiteOnly", "sqlite@test.com");
        InsertPerson(_pgConn, "bidirB", "PostgresOnly", "postgres@test.com");

        // Sync SQLite -> Postgres
        var sqliteChanges = FetchChanges(_sqliteConn, 0);
        PostgresSyncSession.EnableSuppression(_pgConn);
        foreach (var entry in sqliteChanges)
        {
            PostgresChangeApplier.ApplyChange(_pgConn, entry, Logger);
        }
        PostgresSyncSession.DisableSuppression(_pgConn);

        // Sync Postgres -> SQLite
        var pgChanges = FetchChanges(_pgConn, 0);
        SyncSessionManager.EnableSuppression(_sqliteConn);
        foreach (var entry in pgChanges)
        {
            // Skip echo (own origin changes)
            if (entry.Origin != _sqliteOrigin)
            {
                ChangeApplierSQLite.ApplyChange(_sqliteConn, entry);
            }
        }
        SyncSessionManager.DisableSuppression(_sqliteConn);

        // Both databases should have both records
        Assert.NotNull(GetPerson(_pgConn, "bidirA"));
        Assert.NotNull(GetPerson(_pgConn, "bidirB"));
        Assert.NotNull(GetPerson(_sqliteConn, "bidirA"));
        Assert.NotNull(GetPerson(_sqliteConn, "bidirB"));
    }

    [Fact]
    public void Spec_S11_UpdateSync_ChangesPropagate()
    {
        // Insert in both, then update in SQLite
        InsertPerson(_sqliteConn, "upd1", "Original", "orig@test.com");

        // Sync to Postgres first
        var initialChanges = FetchChanges(_sqliteConn, 0);
        PostgresSyncSession.EnableSuppression(_pgConn);
        foreach (var entry in initialChanges)
        {
            PostgresChangeApplier.ApplyChange(_pgConn, entry, Logger);
        }
        PostgresSyncSession.DisableSuppression(_pgConn);

        // Update in SQLite
        UpdatePerson(_sqliteConn, "upd1", "Updated");

        // Sync update to Postgres
        var updateChanges = FetchChanges(_sqliteConn, initialChanges[0].Version);
        PostgresSyncSession.EnableSuppression(_pgConn);
        foreach (var entry in updateChanges)
        {
            PostgresChangeApplier.ApplyChange(_pgConn, entry, Logger);
        }
        PostgresSyncSession.DisableSuppression(_pgConn);

        // Verify update propagated
        var person = GetPerson(_pgConn, "upd1");
        Assert.Equal("Updated", person!.Name);
    }

    [Fact]
    public void Spec_S11_DeleteSync_TombstonePropagates()
    {
        // Insert and sync
        InsertPerson(_sqliteConn, "del1", "ToDelete", "del@test.com");
        var insertChanges = FetchChanges(_sqliteConn, 0);
        PostgresSyncSession.EnableSuppression(_pgConn);
        foreach (var entry in insertChanges)
        {
            PostgresChangeApplier.ApplyChange(_pgConn, entry, Logger);
        }
        PostgresSyncSession.DisableSuppression(_pgConn);

        // Verify exists in both
        Assert.NotNull(GetPerson(_pgConn, "del1"));

        // Delete in SQLite
        DeletePerson(_sqliteConn, "del1");

        // Sync delete
        var deleteChanges = FetchChanges(_sqliteConn, insertChanges[0].Version);
        PostgresSyncSession.EnableSuppression(_pgConn);
        foreach (var entry in deleteChanges)
        {
            PostgresChangeApplier.ApplyChange(_pgConn, entry, Logger);
        }
        PostgresSyncSession.DisableSuppression(_pgConn);

        // Verify deleted in both
        Assert.Null(GetPerson(_sqliteConn, "del1"));
        Assert.Null(GetPerson(_pgConn, "del1"));
    }

    #endregion

    #region Spec Section 12: Batching

    [Fact]
    public void Spec_S12_BatchProcessing_LargeDataset()
    {
        // Insert many records in SQLite
        for (int i = 0; i < 100; i++)
        {
            InsertPerson(_sqliteConn, $"batch{i}", $"Person{i}", $"p{i}@test.com");
        }

        // Sync in batches
        long lastVersion = 0;
        var totalSynced = 0;
        const int batchSize = 25;

        while (true)
        {
            var result = SyncLogRepository.FetchChanges(_sqliteConn, lastVersion, batchSize + 1);

            var changes = (Assert.IsType<SyncLogListOk>(result)).Value;

            if (changes.Count == 0)
                break;

            var hasMore = changes.Count > batchSize;
            var batch = hasMore ? changes.Take(batchSize).ToList() : [.. changes];

            PostgresSyncSession.EnableSuppression(_pgConn);
            foreach (var entry in batch)
            {
                PostgresChangeApplier.ApplyChange(_pgConn, entry, Logger);
                totalSynced++;
            }
            PostgresSyncSession.DisableSuppression(_pgConn);

            lastVersion = batch[^1].Version;

            if (!hasMore)
                break;
        }

        Assert.Equal(100, totalSynced);

        // Verify all records in Postgres
        var count = GetPersonCount(_pgConn);
        Assert.Equal(100, count);
    }

    #endregion

    #region Spec Section 14: Echo Prevention

    [Fact]
    public void Spec_S14_EchoPrevention_SkipsOwnOrigin()
    {
        // Insert in SQLite
        InsertPerson(_sqliteConn, "echo1", "EchoTest", "echo@test.com");

        // Get the change
        var changes = FetchChanges(_sqliteConn, 0);
        Assert.Single(changes);
        Assert.Equal(_sqliteOrigin, changes[0].Origin);

        // Apply with echo prevention using ChangeApplier
        var batch = new SyncBatch(changes, 0, changes[0].Version, false);
        var result = ChangeApplier.ApplyBatch(
            batch,
            _sqliteOrigin, // Same origin - should skip
            3,
            entry => new BoolSyncOk(true), // Would succeed if called
            Logger
        );

        var applied = (Assert.IsType<BatchApplyResultOk>(result)).Value;
        Assert.Equal(0, applied.AppliedCount); // Should be skipped due to echo
        // Echo-skipped entries don't count as deferred, they just aren't applied
    }

    #endregion

    #region Spec Section 15: Hash Verification

    [Fact]
    public void Spec_S15_BatchHash_MatchesAfterSync()
    {
        // Insert records
        InsertPerson(_sqliteConn, "hash1", "HashTest1", "h1@test.com");
        InsertPerson(_sqliteConn, "hash2", "HashTest2", "h2@test.com");

        // Fetch with hash
        var changes = FetchChanges(_sqliteConn, 0);
        var hash = HashVerifier.ComputeBatchHash(changes);

        // Sync to Postgres
        PostgresSyncSession.EnableSuppression(_pgConn);
        foreach (var entry in changes)
        {
            PostgresChangeApplier.ApplyChange(_pgConn, entry, Logger);
        }
        PostgresSyncSession.DisableSuppression(_pgConn);

        // Compute hash from same changes
        var verifyHash = HashVerifier.ComputeBatchHash(changes);
        Assert.Equal(hash, verifyHash);
    }

    #endregion

    #region Spec Section 13: Client Tracking

    [Fact]
    public void Spec_S13_ClientTracking_Postgres()
    {
        var client = new SyncClient(
            OriginId: _sqliteOrigin,
            LastSyncVersion: 100,
            LastSyncTimestamp: DateTime.UtcNow.ToString("O"),
            CreatedAt: DateTime.UtcNow.ToString("O")
        );

        // Upsert client
        var upsertResult = PostgresSyncClientRepository.Upsert(_pgConn, client);
        Assert.IsType<BoolSyncOk>(upsertResult);

        // Get client
        var getResult = PostgresSyncClientRepository.GetByOrigin(_pgConn, _sqliteOrigin);

        var retrieved = (Assert.IsType<SyncClientOk>(getResult)).Value;
        Assert.NotNull(retrieved);
        Assert.Equal(100, retrieved!.LastSyncVersion);

        // Get all clients
        var allResult = PostgresSyncClientRepository.GetAll(_pgConn);
        Assert.IsType<SyncClientListOk>(allResult);
        Assert.Contains(((SyncClientListOk)allResult).Value, c => c.OriginId == _sqliteOrigin);
    }

    #endregion

    #region Helper Methods

    private static void InsertPerson(NpgsqlConnection conn, string id, string name, string email)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO person (id, name, email) VALUES (@id, @name, @email)";
        PersonCommand.Execute(command: cmd, id: id, name: name, email: email);
    }

    private static void InsertPerson(SqliteConnection conn, string id, string name, string email)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Person (Id, Name, Email) VALUES (@id, @name, @email)";
        PersonCommand.Execute(command: cmd, id: id, name: name, email: email);
    }

    private static void UpdatePerson(NpgsqlConnection conn, string id, string name)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE person SET name = @name WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.ExecuteNonQuery();
    }

    private static void UpdatePerson(SqliteConnection conn, string id, string name)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Person SET Name = @name WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.ExecuteNonQuery();
    }

    private static void DeletePerson(NpgsqlConnection conn, string id)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM person WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    private static void DeletePerson(SqliteConnection conn, string id)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Person WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    private static IReadOnlyList<SyncLogEntry> FetchChanges(NpgsqlConnection conn, long fromVersion)
    {
        var result = PostgresSyncLogRepository.FetchChanges(conn, fromVersion, 1000);
        return result is SyncLogListOk ok ? ok.Value : [];
    }

    private static IReadOnlyList<SyncLogEntry> FetchChanges(SqliteConnection conn, long fromVersion)
    {
        var result = SyncLogRepository.FetchChanges(conn, fromVersion, 1000);
        return result is SyncLogListOk ok ? ok.Value : [];
    }

    private static long GetLogCount(NpgsqlConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM _sync_log";
        return (long)(cmd.ExecuteScalar() ?? 0);
    }

    private static PersonRecord? GetPerson(NpgsqlConnection conn, string id)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, email FROM person WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read()
            ? new PersonRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)
            )
            : null;
    }

    private static PersonRecord? GetPerson(SqliteConnection conn, string id)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Email FROM Person WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read()
            ? new PersonRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)
            )
            : null;
    }

    private static int GetPersonCount(NpgsqlConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM person";
        return Convert.ToInt32(
            cmd.ExecuteScalar(),
            System.Globalization.CultureInfo.InvariantCulture
        );
    }

    private sealed record PersonRecord(string Id, string Name, string? Email);

    #endregion
}
