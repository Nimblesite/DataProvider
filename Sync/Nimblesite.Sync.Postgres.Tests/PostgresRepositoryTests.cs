namespace Nimblesite.Sync.Postgres.Tests;

/// <summary>
/// Integration tests for Postgres repositories.
/// Uses the shared postgres container; each test gets its own database.
/// </summary>
[Collection(PostgresTestSuite.Name)]
public sealed class PostgresRepositoryTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private NpgsqlConnection _conn = null!;
    private static readonly ILogger Logger = NullLogger.Instance;

    public async Task InitializeAsync()
    {
        _conn = await fixture.CreateDatabaseAsync("sync_repo_test").ConfigureAwait(false);

        // Create schema
        var schemaResult = PostgresSyncSchema.CreateSchema(_conn);
        Assert.IsType<BoolSyncOk>(schemaResult);
    }

    public Task DisposeAsync()
    {
        _conn.Dispose();
        return Task.CompletedTask;
    }

    #region PostgresSyncLogRepository Tests

    [Fact]
    public void FetchChanges_EmptyLog_ReturnsEmptyList()
    {
        var result = PostgresSyncLogRepository.FetchChanges(_conn, 0, 100);

        Assert.IsType<SyncLogListOk>(result);
        var list = ((SyncLogListOk)result).Value;
        Assert.Empty(list);
    }

    [Fact]
    public void Insert_And_FetchChanges_RoundTrips()
    {
        var entry = new SyncLogEntry(
            0,
            "TestTable",
            "{\"Id\":\"t1\"}",
            SyncOperation.Insert,
            "{\"Id\":\"t1\",\"Name\":\"Test\"}",
            "test-origin",
            DateTime.UtcNow.ToString("O")
        );

        var insertResult = PostgresSyncLogRepository.Insert(_conn, entry);
        Assert.IsType<BoolSyncOk>(insertResult);

        var fetchResult = PostgresSyncLogRepository.FetchChanges(_conn, 0, 100);
        Assert.IsType<SyncLogListOk>(fetchResult);
        var list = ((SyncLogListOk)fetchResult).Value;
        Assert.Single(list);
        Assert.Equal("TestTable", list[0].TableName);
        Assert.Equal("{\"Id\":\"t1\"}", list[0].PkValue);
        Assert.Equal(SyncOperation.Insert, list[0].Operation);
    }

    [Fact]
    public void FetchChanges_RespectsFromVersion()
    {
        // Insert 3 entries
        for (var i = 1; i <= 3; i++)
        {
            var entry = new SyncLogEntry(
                0,
                "TestTable",
                $"{{\"Id\":\"t{i}\"}}",
                SyncOperation.Insert,
                $"{{\"Id\":\"t{i}\",\"Name\":\"Test {i}\"}}",
                "test-origin",
                DateTime.UtcNow.ToString("O")
            );
            PostgresSyncLogRepository.Insert(_conn, entry);
        }

        // Fetch from version 0 - should get all 3
        var all = PostgresSyncLogRepository.FetchChanges(_conn, 0, 100);
        var allList = ((SyncLogListOk)all).Value;
        Assert.Equal(3, allList.Count);

        // Fetch from version 1 - should get 2
        var from1 = PostgresSyncLogRepository.FetchChanges(_conn, 1, 100);
        var from1List = ((SyncLogListOk)from1).Value;
        Assert.Equal(2, from1List.Count);

        // Fetch from version 2 - should get 1
        var from2 = PostgresSyncLogRepository.FetchChanges(_conn, 2, 100);
        var from2List = ((SyncLogListOk)from2).Value;
        Assert.Single(from2List);
    }

    [Fact]
    public void FetchChanges_RespectsLimit()
    {
        // Insert 10 entries
        for (var i = 1; i <= 10; i++)
        {
            var entry = new SyncLogEntry(
                0,
                "TestTable",
                $"{{\"Id\":\"t{i}\"}}",
                SyncOperation.Insert,
                $"{{\"Id\":\"t{i}\",\"Name\":\"Test {i}\"}}",
                "test-origin",
                DateTime.UtcNow.ToString("O")
            );
            PostgresSyncLogRepository.Insert(_conn, entry);
        }

        var limited = PostgresSyncLogRepository.FetchChanges(_conn, 0, 5);
        var list = ((SyncLogListOk)limited).Value;
        Assert.Equal(5, list.Count);
    }

    [Fact]
    public void GetMaxVersion_EmptyLog_ReturnsZero()
    {
        var result = PostgresSyncLogRepository.GetMaxVersion(_conn);

        Assert.IsType<LongSyncOk>(result);
        var max = ((LongSyncOk)result).Value;
        Assert.Equal(0, max);
    }

    [Fact]
    public void GetMaxVersion_WithEntries_ReturnsMax()
    {
        // Insert 3 entries
        for (var i = 1; i <= 3; i++)
        {
            var entry = new SyncLogEntry(
                0,
                "TestTable",
                $"{{\"Id\":\"t{i}\"}}",
                SyncOperation.Insert,
                $"{{\"Id\":\"t{i}\",\"Name\":\"Test {i}\"}}",
                "test-origin",
                DateTime.UtcNow.ToString("O")
            );
            PostgresSyncLogRepository.Insert(_conn, entry);
        }

        var result = PostgresSyncLogRepository.GetMaxVersion(_conn);
        var max = ((LongSyncOk)result).Value;
        Assert.Equal(3, max);
    }

    [Fact]
    public void GetLastServerVersion_NoState_ReturnsZero()
    {
        var result = PostgresSyncLogRepository.GetLastServerVersion(_conn);

        Assert.IsType<LongSyncOk>(result);
        var version = ((LongSyncOk)result).Value;
        Assert.Equal(0, version);
    }

    [Fact]
    public void UpdateLastServerVersion_And_Get_RoundTrips()
    {
        var updateResult = PostgresSyncLogRepository.UpdateLastServerVersion(_conn, 42);
        Assert.IsType<BoolSyncOk>(updateResult);

        var getResult = PostgresSyncLogRepository.GetLastServerVersion(_conn);
        Assert.IsType<LongSyncOk>(getResult);
        var version = ((LongSyncOk)getResult).Value;
        Assert.Equal(42, version);
    }

    [Fact]
    public void UpdateLastServerVersion_Upserts()
    {
        PostgresSyncLogRepository.UpdateLastServerVersion(_conn, 10);
        PostgresSyncLogRepository.UpdateLastServerVersion(_conn, 20);
        PostgresSyncLogRepository.UpdateLastServerVersion(_conn, 30);

        var result = PostgresSyncLogRepository.GetLastServerVersion(_conn);
        var version = ((LongSyncOk)result).Value;
        Assert.Equal(30, version);
    }

    [Fact]
    public void Insert_AllOperationTypes()
    {
        var operations = new[] { SyncOperation.Insert, SyncOperation.Update, SyncOperation.Delete };

        foreach (var op in operations)
        {
            var entry = new SyncLogEntry(
                0,
                "TestTable",
                $"{{\"Id\":\"op{op}\"}}",
                op,
                op == SyncOperation.Delete ? null : $"{{\"Id\":\"op{op}\",\"Name\":\"Test\"}}",
                "test-origin",
                DateTime.UtcNow.ToString("O")
            );
            var result = PostgresSyncLogRepository.Insert(_conn, entry);
            Assert.IsType<BoolSyncOk>(result);
        }

        var fetchResult = PostgresSyncLogRepository.FetchChanges(_conn, 0, 100);
        var list = ((SyncLogListOk)fetchResult).Value;
        Assert.Equal(3, list.Count);

        Assert.Contains(list, e => e.Operation == SyncOperation.Insert);
        Assert.Contains(list, e => e.Operation == SyncOperation.Update);
        Assert.Contains(list, e => e.Operation == SyncOperation.Delete);
    }

    #endregion

    #region PostgresSyncClientRepository Tests

    [Fact]
    public void GetAll_Empty_ReturnsEmptyList()
    {
        var result = PostgresSyncClientRepository.GetAll(_conn);

        Assert.IsType<SyncClientListOk>(result);
        var list = ((SyncClientListOk)result).Value;
        Assert.Empty(list);
    }

    [Fact]
    public void Upsert_And_GetAll_RoundTrips()
    {
        var client = new SyncClient(
            "client-001",
            10,
            DateTime.UtcNow.ToString("O"),
            DateTime.UtcNow.ToString("O")
        );

        var upsertResult = PostgresSyncClientRepository.Upsert(_conn, client);
        Assert.IsType<BoolSyncOk>(upsertResult);

        var getResult = PostgresSyncClientRepository.GetAll(_conn);
        Assert.IsType<SyncClientListOk>(getResult);
        var list = ((SyncClientListOk)getResult).Value;
        Assert.Single(list);
        Assert.Equal("client-001", list[0].OriginId);
        Assert.Equal(10, list[0].LastSyncVersion);
    }

    [Fact]
    public void GetByOrigin_NotFound_ReturnsNull()
    {
        var result = PostgresSyncClientRepository.GetByOrigin(_conn, "nonexistent");

        Assert.IsType<SyncClientOk>(result);
        var client = ((SyncClientOk)result).Value;
        Assert.Null(client);
    }

    [Fact]
    public void GetByOrigin_Found_ReturnsClient()
    {
        var client = new SyncClient(
            "client-002",
            20,
            DateTime.UtcNow.ToString("O"),
            DateTime.UtcNow.ToString("O")
        );
        PostgresSyncClientRepository.Upsert(_conn, client);

        var result = PostgresSyncClientRepository.GetByOrigin(_conn, "client-002");
        Assert.IsType<SyncClientOk>(result);
        var found = ((SyncClientOk)result).Value;
        Assert.NotNull(found);
        Assert.Equal("client-002", found.OriginId);
        Assert.Equal(20, found.LastSyncVersion);
    }

    [Fact]
    public void Upsert_UpdatesExisting()
    {
        var client1 = new SyncClient(
            "client-003",
            10,
            DateTime.UtcNow.ToString("O"),
            DateTime.UtcNow.ToString("O")
        );
        PostgresSyncClientRepository.Upsert(_conn, client1);

        var client2 = new SyncClient(
            "client-003",
            50,
            DateTime.UtcNow.ToString("O"),
            DateTime.UtcNow.ToString("O")
        );
        PostgresSyncClientRepository.Upsert(_conn, client2);

        var result = PostgresSyncClientRepository.GetByOrigin(_conn, "client-003");
        var found = ((SyncClientOk)result).Value!;
        Assert.Equal(50, found.LastSyncVersion);
    }

    [Fact]
    public void Delete_RemovesClient()
    {
        var client = new SyncClient(
            "client-004",
            10,
            DateTime.UtcNow.ToString("O"),
            DateTime.UtcNow.ToString("O")
        );
        PostgresSyncClientRepository.Upsert(_conn, client);

        var deleteResult = PostgresSyncClientRepository.Delete(_conn, "client-004");
        Assert.IsType<BoolSyncOk>(deleteResult);

        var getResult = PostgresSyncClientRepository.GetByOrigin(_conn, "client-004");
        var found = ((SyncClientOk)getResult).Value;
        Assert.Null(found);
    }

    [Fact]
    public void Delete_NonExistent_Succeeds()
    {
        var result = PostgresSyncClientRepository.Delete(_conn, "nonexistent");
        Assert.IsType<BoolSyncOk>(result);
    }

    [Fact]
    public void GetMinVersion_NoClients_ReturnsZero()
    {
        var result = PostgresSyncClientRepository.GetMinVersion(_conn);

        Assert.IsType<LongSyncOk>(result);
        var min = ((LongSyncOk)result).Value;
        Assert.Equal(0, min);
    }

    [Fact]
    public void GetMinVersion_WithClients_ReturnsMin()
    {
        var clients = new[]
        {
            new SyncClient("c1", 100, DateTime.UtcNow.ToString("O"), DateTime.UtcNow.ToString("O")),
            new SyncClient("c2", 50, DateTime.UtcNow.ToString("O"), DateTime.UtcNow.ToString("O")),
            new SyncClient("c3", 75, DateTime.UtcNow.ToString("O"), DateTime.UtcNow.ToString("O")),
        };

        foreach (var client in clients)
        {
            PostgresSyncClientRepository.Upsert(_conn, client);
        }

        var result = PostgresSyncClientRepository.GetMinVersion(_conn);
        var min = ((LongSyncOk)result).Value;
        Assert.Equal(50, min);
    }

    [Fact]
    public void GetAll_OrderedByVersion()
    {
        var clients = new[]
        {
            new SyncClient("c1", 100, DateTime.UtcNow.ToString("O"), DateTime.UtcNow.ToString("O")),
            new SyncClient("c2", 25, DateTime.UtcNow.ToString("O"), DateTime.UtcNow.ToString("O")),
            new SyncClient("c3", 50, DateTime.UtcNow.ToString("O"), DateTime.UtcNow.ToString("O")),
        };

        foreach (var client in clients)
        {
            PostgresSyncClientRepository.Upsert(_conn, client);
        }

        var result = PostgresSyncClientRepository.GetAll(_conn);
        var list = ((SyncClientListOk)result).Value;

        // Should be ordered by last_sync_version ASC
        Assert.Equal("c2", list[0].OriginId); // 25
        Assert.Equal("c3", list[1].OriginId); // 50
        Assert.Equal("c1", list[2].OriginId); // 100
    }

    #endregion

    #region PostgresSyncSession Tests

    [Fact]
    public void EnableSuppression_Succeeds()
    {
        var result = PostgresSyncSession.EnableSuppression(_conn);
        Assert.IsType<BoolSyncOk>(result);
    }

    [Fact]
    public void DisableSuppression_Succeeds()
    {
        var result = PostgresSyncSession.DisableSuppression(_conn);
        Assert.IsType<BoolSyncOk>(result);
    }

    [Fact]
    public void Suppression_Toggle_Works()
    {
        // Enable
        var enable = PostgresSyncSession.EnableSuppression(_conn);
        Assert.IsType<BoolSyncOk>(enable);

        // Verify it's enabled
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT sync_active FROM _sync_session";
        var value = Convert.ToInt32(
            cmd.ExecuteScalar(),
            System.Globalization.CultureInfo.InvariantCulture
        );
        Assert.Equal(1, value);

        // Disable
        var disable = PostgresSyncSession.DisableSuppression(_conn);
        Assert.IsType<BoolSyncOk>(disable);

        // Verify it's disabled
        value = Convert.ToInt32(
            cmd.ExecuteScalar(),
            System.Globalization.CultureInfo.InvariantCulture
        );
        Assert.Equal(0, value);
    }

    #endregion

    #region PostgresSyncSchema Tests

    [Fact]
    public void SetOriginId_And_GetOriginId_RoundTrips()
    {
        var setResult = PostgresSyncSchema.SetOriginId(_conn, "my-origin-123");
        Assert.IsType<BoolSyncOk>(setResult);

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM _sync_state WHERE key = 'origin_id'";
        var origin = cmd.ExecuteScalar()?.ToString();
        Assert.Equal("my-origin-123", origin);
    }

    [Fact]
    public void CreateSchema_CreatesAllTables()
    {
        // Schema already created in InitializeAsync, verify tables exist
        var tables = new[] { "_sync_log", "_sync_state", "_sync_session", "_sync_clients" };

        foreach (var table in tables)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText =
                "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = @table)";
            cmd.Parameters.AddWithValue("@table", table);
            var exists = (bool)cmd.ExecuteScalar()!;
            Assert.True(exists, $"Table {table} should exist");
        }
    }

    [Fact]
    public void GetOriginId_AfterSet_ReturnsValue()
    {
        PostgresSyncSchema.SetOriginId(_conn, "test-origin-id");

        var result = PostgresSyncSchema.GetOriginId(_conn);

        Assert.IsType<StringSyncOk>(result);
        Assert.Equal("test-origin-id", ((StringSyncOk)result).Value);
    }

    [Fact]
    public void GetOriginId_NotSet_ReturnsEmpty()
    {
        // Origin is empty by default from schema initialization
        var result = PostgresSyncSchema.GetOriginId(_conn);

        Assert.IsType<StringSyncOk>(result);
        Assert.Equal("", ((StringSyncOk)result).Value);
    }

    [Fact]
    public void InitializeOriginId_WhenEmpty_GeneratesNew()
    {
        var originId = PostgresSyncSchema.InitializeOriginId(_conn);

        Assert.NotEmpty(originId);
        Assert.True(Guid.TryParse(originId, out _));
    }

    [Fact]
    public void InitializeOriginId_WhenSet_ReturnsExisting()
    {
        PostgresSyncSchema.SetOriginId(_conn, "existing-origin");

        var originId = PostgresSyncSchema.InitializeOriginId(_conn);

        Assert.Equal("existing-origin", originId);
    }

    [Fact]
    public void CreateSchema_Idempotent()
    {
        // Schema already created in InitializeAsync
        // Call again should succeed
        var result = PostgresSyncSchema.CreateSchema(_conn);

        Assert.IsType<BoolSyncOk>(result);
    }

    #endregion

    #region PostgresChangeApplier Tests

    [Fact]
    public void ApplyChange_Insert_CreatesRow()
    {
        // Create test table
        using var createCmd = _conn.CreateCommand();
        createCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS test_person (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL
            )
            """;
        createCmd.ExecuteNonQuery();

        var entry = new SyncLogEntry(
            1,
            "test_person",
            "{\"id\":\"p1\"}",
            SyncOperation.Insert,
            "{\"id\":\"p1\",\"name\":\"Alice\"}",
            "test-origin",
            DateTime.UtcNow.ToString("O")
        );

        var result = PostgresChangeApplier.ApplyChange(_conn, entry, Logger);
        Assert.IsType<BoolSyncOk>(result);

        // Verify row exists
        using var verifyCmd = _conn.CreateCommand();
        verifyCmd.CommandText = "SELECT name FROM test_person WHERE id = 'p1'";
        var name = verifyCmd.ExecuteScalar()?.ToString();
        Assert.Equal("Alice", name);
    }

    [Fact]
    public void ApplyChange_Update_UpdatesRow()
    {
        // Create and populate test table
        using var createCmd = _conn.CreateCommand();
        createCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS test_update (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL
            );
            INSERT INTO test_update (id, name) VALUES ('u1', 'Original');
            """;
        createCmd.ExecuteNonQuery();

        var entry = new SyncLogEntry(
            1,
            "test_update",
            "{\"id\":\"u1\"}",
            SyncOperation.Update,
            "{\"id\":\"u1\",\"name\":\"Updated\"}",
            "test-origin",
            DateTime.UtcNow.ToString("O")
        );

        var result = PostgresChangeApplier.ApplyChange(_conn, entry, Logger);
        Assert.IsType<BoolSyncOk>(result);

        using var verifyCmd = _conn.CreateCommand();
        verifyCmd.CommandText = "SELECT name FROM test_update WHERE id = 'u1'";
        var name = verifyCmd.ExecuteScalar()?.ToString();
        Assert.Equal("Updated", name);
    }

    [Fact]
    public void ApplyChange_Delete_RemovesRow()
    {
        // Create and populate test table
        using var createCmd = _conn.CreateCommand();
        createCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS test_delete (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL
            );
            INSERT INTO test_delete (id, name) VALUES ('d1', 'ToDelete');
            """;
        createCmd.ExecuteNonQuery();

        var entry = new SyncLogEntry(
            1,
            "test_delete",
            "{\"id\":\"d1\"}",
            SyncOperation.Delete,
            null,
            "test-origin",
            DateTime.UtcNow.ToString("O")
        );

        var result = PostgresChangeApplier.ApplyChange(_conn, entry, Logger);
        Assert.IsType<BoolSyncOk>(result);

        using var verifyCmd = _conn.CreateCommand();
        verifyCmd.CommandText = "SELECT COUNT(*) FROM test_delete WHERE id = 'd1'";
        var count = Convert.ToInt32(
            verifyCmd.ExecuteScalar(),
            System.Globalization.CultureInfo.InvariantCulture
        );
        Assert.Equal(0, count);
    }

    [Fact]
    public void ApplyChange_Insert_Upserts()
    {
        using var createCmd = _conn.CreateCommand();
        createCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS test_upsert (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL
            );
            INSERT INTO test_upsert (id, name) VALUES ('x1', 'Original');
            """;
        createCmd.ExecuteNonQuery();

        var entry = new SyncLogEntry(
            1,
            "test_upsert",
            "{\"id\":\"x1\"}",
            SyncOperation.Insert,
            "{\"id\":\"x1\",\"name\":\"Upserted\"}",
            "test-origin",
            DateTime.UtcNow.ToString("O")
        );

        var result = PostgresChangeApplier.ApplyChange(_conn, entry, Logger);
        Assert.IsType<BoolSyncOk>(result);

        using var verifyCmd = _conn.CreateCommand();
        verifyCmd.CommandText = "SELECT name FROM test_upsert WHERE id = 'x1'";
        var name = verifyCmd.ExecuteScalar()?.ToString();
        Assert.Equal("Upserted", name);
    }

    [Fact]
    public void ApplyChange_Update_NonExistent_UpsertsViaInsert()
    {
        using var createCmd = _conn.CreateCommand();
        createCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS test_update_upsert (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL
            )
            """;
        createCmd.ExecuteNonQuery();

        var entry = new SyncLogEntry(
            1,
            "test_update_upsert",
            "{\"id\":\"new1\"}",
            SyncOperation.Update,
            "{\"id\":\"new1\",\"name\":\"NewRecord\"}",
            "test-origin",
            DateTime.UtcNow.ToString("O")
        );

        var result = PostgresChangeApplier.ApplyChange(_conn, entry, Logger);
        Assert.IsType<BoolSyncOk>(result);

        using var verifyCmd = _conn.CreateCommand();
        verifyCmd.CommandText = "SELECT name FROM test_update_upsert WHERE id = 'new1'";
        var name = verifyCmd.ExecuteScalar()?.ToString();
        Assert.Equal("NewRecord", name);
    }

    [Fact]
    public void ApplyChange_ForeignKeyViolation_ReturnsFalse()
    {
        using var createCmd = _conn.CreateCommand();
        createCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS test_parent (
                id TEXT PRIMARY KEY
            );
            CREATE TABLE IF NOT EXISTS test_child (
                id TEXT PRIMARY KEY,
                parent_id TEXT REFERENCES test_parent(id)
            )
            """;
        createCmd.ExecuteNonQuery();

        var entry = new SyncLogEntry(
            1,
            "test_child",
            "{\"id\":\"c1\"}",
            SyncOperation.Insert,
            "{\"id\":\"c1\",\"parent_id\":\"nonexistent\"}",
            "test-origin",
            DateTime.UtcNow.ToString("O")
        );

        var result = PostgresChangeApplier.ApplyChange(_conn, entry, Logger);
        Assert.IsType<BoolSyncOk>(result);
        var success = ((BoolSyncOk)result).Value;
        Assert.False(success); // FK violation returns false for deferral
    }

    #endregion

    #region PostgresTriggerGenerator Tests

    [Fact]
    public void CreateTriggers_CreatesAllTriggers()
    {
        // Create test table
        using var createCmd = _conn.CreateCommand();
        createCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS test_trigger_table (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL
            )
            """;
        createCmd.ExecuteNonQuery();

        var result = PostgresTriggerGenerator.CreateTriggers(_conn, "test_trigger_table", Logger);
        Assert.IsType<BoolSyncOk>(result);

        // Verify triggers exist
        var triggerTypes = new[] { "insert", "update", "delete" };
        foreach (var triggerType in triggerTypes)
        {
            using var checkCmd = _conn.CreateCommand();
            checkCmd.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = @name)";
            checkCmd.Parameters.AddWithValue("@name", $"test_trigger_table_sync_{triggerType}");
            var exists = (bool)checkCmd.ExecuteScalar()!;
            Assert.True(exists, $"Trigger for {triggerType} should exist");
        }
    }

    [Fact]
    public void Triggers_LogInsert()
    {
        // Create and setup test table with triggers
        using var createCmd = _conn.CreateCommand();
        createCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS test_log_insert (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL
            )
            """;
        createCmd.ExecuteNonQuery();

        PostgresTriggerGenerator.CreateTriggers(_conn, "test_log_insert", Logger);

        // Ensure suppression is disabled
        PostgresSyncSession.DisableSuppression(_conn);
        PostgresSyncSchema.SetOriginId(_conn, "trigger-test-origin");

        // Insert a record
        using var insertCmd = _conn.CreateCommand();
        insertCmd.CommandText =
            "INSERT INTO test_log_insert (id, name) VALUES ('ti1', 'TriggerTest')";
        insertCmd.ExecuteNonQuery();

        // Check sync log
        var changes = PostgresSyncLogRepository.FetchChanges(_conn, 0, 100);
        var list = ((SyncLogListOk)changes).Value;
        var insertEntry = list.FirstOrDefault(e =>
            e.TableName == "test_log_insert" && e.Operation == SyncOperation.Insert
        );

        Assert.NotNull(insertEntry);
        Assert.Contains("ti1", insertEntry.PkValue);
        Assert.Contains("TriggerTest", insertEntry.Payload!);
    }

    [Fact]
    public void Triggers_LogUpdate()
    {
        using var createCmd = _conn.CreateCommand();
        createCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS test_log_update (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL
            );
            INSERT INTO test_log_update (id, name) VALUES ('tu1', 'Before');
            """;
        createCmd.ExecuteNonQuery();

        PostgresTriggerGenerator.CreateTriggers(_conn, "test_log_update", Logger);
        PostgresSyncSession.DisableSuppression(_conn);
        PostgresSyncSchema.SetOriginId(_conn, "trigger-test-origin");

        // Get current max version
        var maxBefore = ((LongSyncOk)PostgresSyncLogRepository.GetMaxVersion(_conn)).Value;

        // Update
        using var updateCmd = _conn.CreateCommand();
        updateCmd.CommandText = "UPDATE test_log_update SET name = 'After' WHERE id = 'tu1'";
        updateCmd.ExecuteNonQuery();

        // Check sync log
        var changes = PostgresSyncLogRepository.FetchChanges(_conn, maxBefore, 100);
        var list = ((SyncLogListOk)changes).Value;
        var updateEntry = list.FirstOrDefault(e =>
            e.TableName == "test_log_update" && e.Operation == SyncOperation.Update
        );

        Assert.NotNull(updateEntry);
        Assert.Contains("After", updateEntry.Payload!);
    }

    [Fact]
    public void Triggers_LogDelete()
    {
        using var createCmd = _conn.CreateCommand();
        createCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS test_log_delete (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL
            );
            INSERT INTO test_log_delete (id, name) VALUES ('td1', 'ToDelete');
            """;
        createCmd.ExecuteNonQuery();

        PostgresTriggerGenerator.CreateTriggers(_conn, "test_log_delete", Logger);
        PostgresSyncSession.DisableSuppression(_conn);
        PostgresSyncSchema.SetOriginId(_conn, "trigger-test-origin");

        var maxBefore = ((LongSyncOk)PostgresSyncLogRepository.GetMaxVersion(_conn)).Value;

        // Delete
        using var deleteCmd = _conn.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM test_log_delete WHERE id = 'td1'";
        deleteCmd.ExecuteNonQuery();

        // Check sync log
        var changes = PostgresSyncLogRepository.FetchChanges(_conn, maxBefore, 100);
        var list = ((SyncLogListOk)changes).Value;
        var deleteEntry = list.FirstOrDefault(e =>
            e.TableName == "test_log_delete" && e.Operation == SyncOperation.Delete
        );

        Assert.NotNull(deleteEntry);
        Assert.Contains("td1", deleteEntry.PkValue);
        Assert.Null(deleteEntry.Payload); // Tombstone has no payload
    }

    [Fact]
    public void Triggers_SuppressedWhenActive()
    {
        using var createCmd = _conn.CreateCommand();
        createCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS test_suppression (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL
            )
            """;
        createCmd.ExecuteNonQuery();

        PostgresTriggerGenerator.CreateTriggers(_conn, "test_suppression", Logger);
        PostgresSyncSchema.SetOriginId(_conn, "trigger-test-origin");

        // Enable suppression
        PostgresSyncSession.EnableSuppression(_conn);

        var maxBefore = ((LongSyncOk)PostgresSyncLogRepository.GetMaxVersion(_conn)).Value;

        // Insert while suppressed
        using var insertCmd = _conn.CreateCommand();
        insertCmd.CommandText =
            "INSERT INTO test_suppression (id, name) VALUES ('sup1', 'Suppressed')";
        insertCmd.ExecuteNonQuery();

        // Check sync log - should be empty
        var changes = PostgresSyncLogRepository.FetchChanges(_conn, maxBefore, 100);
        var list = ((SyncLogListOk)changes).Value;
        Assert.Empty(list);

        // Disable and insert
        PostgresSyncSession.DisableSuppression(_conn);

        using var insertCmd2 = _conn.CreateCommand();
        insertCmd2.CommandText =
            "INSERT INTO test_suppression (id, name) VALUES ('unsup1', 'NotSuppressed')";
        insertCmd2.ExecuteNonQuery();

        // Check sync log - should have entry
        var changes2 = PostgresSyncLogRepository.FetchChanges(_conn, maxBefore, 100);
        var list2 = ((SyncLogListOk)changes2).Value;
        Assert.Single(list2);
    }

    #endregion

    [Fact]
    public async Task Repositories_WithoutSyncSchema_ReturnDatabaseErrors()
    {
        using var bare = await fixture
            .CreateDatabaseAsync("sync_repo_no_schema")
            .ConfigureAwait(true);
        var client = new SyncClient("origin-1", 1, "2025-01-01T00:00:00Z", "2025-01-01T00:00:00Z");
        var entry = new SyncLogEntry(
            0,
            "TestTable",
            "{\"Id\":\"t1\"}",
            SyncOperation.Insert,
            "{\"Id\":\"t1\"}",
            "origin-1",
            "2025-01-01T00:00:00Z"
        );

        Assert.True(PostgresSyncClientRepository.GetAll(bare).IsError);
        Assert.True(PostgresSyncClientRepository.GetByOrigin(bare, "origin-1").IsError);
        Assert.True(PostgresSyncClientRepository.Upsert(bare, client).IsError);
        Assert.True(PostgresSyncClientRepository.Delete(bare, "origin-1").IsError);
        Assert.True(PostgresSyncClientRepository.GetMinVersion(bare).IsError);
        Assert.True(PostgresSyncLogRepository.FetchChanges(bare, 0, 10).IsError);
        Assert.True(PostgresSyncLogRepository.Insert(bare, entry).IsError);
        Assert.True(PostgresSyncLogRepository.GetLastServerVersion(bare).IsError);
        Assert.True(PostgresSyncLogRepository.GetMaxVersion(bare).IsError);
        Assert.True(PostgresSyncLogRepository.UpdateLastServerVersion(bare, 5).IsError);
    }
}
