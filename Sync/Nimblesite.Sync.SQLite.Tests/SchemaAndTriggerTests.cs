// Implements [CI-TEST-SHARED-COMMAND].
using Microsoft.Data.Sqlite;
using Tests.Shared;
using Xunit;

namespace Nimblesite.Sync.SQLite.Tests;

/// <summary>
/// Integration tests for SyncSchema and TriggerGenerator.
/// Tests schema creation, trigger generation, and schema metadata operations.
/// NO MOCKS - real SQLite databases only!
/// </summary>
public sealed class SchemaAndTriggerTests : IDisposable
{
    private readonly SqliteConnection _db;
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"schemaandtriggertests_{Guid.NewGuid()}.db"
    );
    private const string OriginId = "test-origin-id";

    public SchemaAndTriggerTests()
    {
        _db = new SqliteConnection($"Data Source={_dbPath}");
        _db.Open();
    }

    #region SyncSchema Tests

    [Fact]
    public void CreateSchema_EmptyDatabase_CreatesAllTables()
    {
        // Act
        var result = SyncSchema.CreateSchema(_db);

        // Assert
        Assert.True(result is BoolSyncOk);

        // Verify tables exist
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            SELECT name FROM sqlite_master
            WHERE type='table'
            AND name LIKE '_sync%'
            ORDER BY name
            """;

        var tables = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }

        Assert.Contains("_sync_clients", tables);
        Assert.Contains("_sync_log", tables);
        Assert.Contains("_sync_session", tables);
        Assert.Contains("_sync_state", tables);
        Assert.Contains("_sync_subscriptions", tables);
    }

    [Fact]
    public void CreateSchema_IdempotentMultipleCalls_Succeeds()
    {
        // Act
        var result1 = SyncSchema.CreateSchema(_db);
        var result2 = SyncSchema.CreateSchema(_db);
        var result3 = SyncSchema.CreateSchema(_db);

        // Assert
        Assert.True(result1 is BoolSyncOk);
        Assert.True(result2 is BoolSyncOk);
        Assert.True(result3 is BoolSyncOk);
    }

    [Fact]
    public void CreateSchema_InitializesSyncState()
    {
        // Act
        SyncSchema.CreateSchema(_db);

        // Assert
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM _sync_state ORDER BY key";

        var state = new Dictionary<string, string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            state[reader.GetString(0)] = reader.GetString(1);
        }

        Assert.Contains("last_push_version", state.Keys);
        Assert.Contains("last_server_version", state.Keys);
        Assert.Contains("origin_id", state.Keys);
        Assert.Equal("0", state["last_push_version"]);
        Assert.Equal("0", state["last_server_version"]);
    }

    [Fact]
    public void CreateSchema_InitializesSyncSession()
    {
        // Act
        SyncSchema.CreateSchema(_db);

        // Assert
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT sync_active FROM _sync_session";
        var syncActive = cmd.ExecuteScalar();

        Assert.Equal(0L, syncActive);
    }

    [Fact]
    public void SetOriginId_ValidOrigin_StoresValue()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);

        // Act
        var result = SyncSchema.SetOriginId(_db, OriginId);

        // Assert
        Assert.True(result is BoolSyncOk);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT value FROM _sync_state WHERE key = 'origin_id'";
        var storedOrigin = cmd.ExecuteScalar() as string;

        Assert.Equal(OriginId, storedOrigin);
    }

    [Fact]
    public void GetOriginId_AfterSet_ReturnsValue()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);
        SyncSchema.SetOriginId(_db, OriginId);

        // Act
        var result = SyncSchema.GetOriginId(_db);

        // Assert
        Assert.True(result is StringSyncOk);
        Assert.Equal(OriginId, ((StringSyncOk)result).Value);
    }

    [Fact]
    public void GetOriginId_BeforeSet_ReturnsEmptyString()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);

        // Act
        var result = SyncSchema.GetOriginId(_db);

        // Assert
        Assert.True(result is StringSyncOk);
        Assert.Equal("", ((StringSyncOk)result).Value);
    }

    [Theory]
    // CreateSchema_CreatesSyncLogIndexes
    [InlineData(
        """
            SELECT name FROM sqlite_master
            WHERE type='index'
            AND name LIKE 'idx_sync_log%'
            """,
        "idx_sync_log_version",
        "idx_sync_log_table"
    )]
    // CreateSchema_CreatesSubscriptionIndexes
    [InlineData(
        """
            SELECT name FROM sqlite_master
            WHERE type='index'
            AND name LIKE 'idx_subscriptions%'
            ORDER BY name
            """,
        "idx_subscriptions_origin",
        "idx_subscriptions_table"
    )]
    public void CreateSchema_CreatesSyncLogIndexes_Cases(
        string caseValue0,
        string caseValue2,
        string caseValue3
    )
    {
        // Act
        SyncSchema.CreateSchema(_db);

        // Assert
        using var cmd = _db.CreateCommand();
        cmd.CommandText = caseValue0;

        var indexes = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            indexes.Add(reader.GetString(0));
        }

        Assert.Contains(caseValue2, indexes);
        Assert.Contains(caseValue3, indexes);
    }

    [Fact]
    public void CreateSchema_CreatesSyncClientsIndex()
    {
        // Act
        SyncSchema.CreateSchema(_db);

        // Assert
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            SELECT name FROM sqlite_master
            WHERE type='index'
            AND name LIKE 'idx_sync_clients%'
            """;

        var indexName = cmd.ExecuteScalar() as string;
        Assert.Equal("idx_sync_clients_version", indexName);
    }

    #endregion

    #region TriggerGenerator Tests

    [Fact]
    public void GenerateTriggers_ValidTable_ReturnsInsertUpdateDeleteTriggers()
    {
        // Arrange
        var columns = new List<string> { "Id", "Name", "Age" };
        var pkColumn = "Id";

        // Act
        var triggers = TriggerGenerator.GenerateTriggers("Person", columns, pkColumn);

        // Assert
        Assert.Contains("Person_sync_insert", triggers);
        Assert.Contains("Person_sync_update", triggers);
        Assert.Contains("Person_sync_delete", triggers);
        Assert.Contains("AFTER INSERT ON Person", triggers);
        Assert.Contains("AFTER UPDATE ON Person", triggers);
        Assert.Contains("AFTER DELETE ON Person", triggers);
    }

    [Fact]
    public void GenerateTriggers_IncludesSyncSessionCheck()
    {
        // Arrange
        var columns = new List<string> { "Id", "Name" };

        // Act
        var triggers = TriggerGenerator.GenerateTriggers("Orders", columns, "Id");

        // Assert
        // Should check sync_active flag to prevent trigger firing during sync
        Assert.Contains("sync_active", triggers);
        Assert.Contains("_sync_session", triggers);
    }

    [Fact]
    public void GenerateTriggers_BuildsJsonPayload()
    {
        // Arrange
        var columns = new List<string> { "Id", "Name", "Email" };

        // Act
        var triggers = TriggerGenerator.GenerateTriggers("Users", columns, "Id");

        // Assert
        Assert.Contains("json_object", triggers);
        Assert.Contains("'Id', NEW.Id", triggers);
        Assert.Contains("'Name', NEW.Name", triggers);
        Assert.Contains("'Email', NEW.Email", triggers);
    }

    [Fact]
    public void GenerateTriggers_DeleteUsesOld()
    {
        // Arrange
        var columns = new List<string> { "Id", "Data" };

        // Act
        var triggers = TriggerGenerator.GenerateTriggers("Items", columns, "Id");

        // Assert
        Assert.Contains("OLD.Id", triggers);
    }

    [Fact]
    public void GenerateTriggersFromSchema_ValidTable_ReturnsTriggers()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);
        DatabaseCommands.Execute(
            connection: _db,
            configure: command =>
                command.CommandText = """
                CREATE TABLE Products (
                    ProductId TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Price REAL
                );
                """
        );

        // Act
        var result = TriggerGenerator.GenerateTriggersFromSchema(
            _db,
            "Products",
            NullLogger.Instance
        );

        // Assert
        Assert.True(result is StringSyncOk);
        var triggers = ((StringSyncOk)result).Value;
        Assert.Contains("Products_sync_insert", triggers);
        Assert.Contains("Products_sync_update", triggers);
        Assert.Contains("Products_sync_delete", triggers);
    }

    [Fact]
    public void GenerateTriggersFromSchema_NonExistentTable_ReturnsError()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);

        // Act
        var result = TriggerGenerator.GenerateTriggersFromSchema(
            _db,
            "NonExistentTable",
            NullLogger.Instance
        );

        // Assert
        Assert.True(result is StringSyncError);
    }

    [Fact]
    public void CreateTriggers_ValidTable_CreatesTriggers()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);
        SyncSchema.SetOriginId(_db, OriginId);
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Orders (
                OrderId TEXT PRIMARY KEY,
                CustomerId TEXT,
                Total REAL
            );
            """;
        cmd.ExecuteNonQuery();

        // Act
        var result = TriggerGenerator.CreateTriggers(_db, "Orders", NullLogger.Instance);

        // Assert
        Assert.True(result is BoolSyncOk);

        // Verify triggers exist
        cmd.CommandText = """
            SELECT name FROM sqlite_master
            WHERE type='trigger'
            AND name LIKE 'Orders_sync%'
            ORDER BY name
            """;

        var triggers = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            triggers.Add(reader.GetString(0));
        }

        Assert.Contains("Orders_sync_delete", triggers);
        Assert.Contains("Orders_sync_insert", triggers);
        Assert.Contains("Orders_sync_update", triggers);
    }

    [Fact]
    public void CreateTriggers_NonExistentTable_ReturnsError()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);

        // Act
        var result = TriggerGenerator.CreateTriggers(_db, "NonExistent", NullLogger.Instance);

        // Assert
        Assert.True(result is BoolSyncError);
    }

    [Fact]
    public void DropTriggers_ExistingTriggers_RemovesThem()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);
        SyncSchema.SetOriginId(_db, OriginId);
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Items (
                ItemId TEXT PRIMARY KEY,
                Description TEXT
            );
            """;
        cmd.ExecuteNonQuery();
        TriggerGenerator.CreateTriggers(_db, "Items", NullLogger.Instance);

        // Verify triggers exist
        cmd.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type='trigger' AND name LIKE 'Items_sync%'";
        Assert.Equal(3L, cmd.ExecuteScalar());

        // Act
        var result = TriggerGenerator.DropTriggers(_db, "Items", NullLogger.Instance);

        // Assert
        Assert.True(result is BoolSyncOk);
        Assert.Equal(0L, cmd.ExecuteScalar());
    }

    [Fact]
    public void DropTriggers_NonExistentTriggers_Succeeds()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);

        // Act
        var result = TriggerGenerator.DropTriggers(_db, "NonExistent", NullLogger.Instance);

        // Assert
        Assert.True(result is BoolSyncOk);
    }

    [Fact]
    public void GetTableColumns_ValidTable_ReturnsColumns()
    {
        // Arrange
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Customers (
                CustomerId TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Email TEXT,
                Age INTEGER
            );
            """;
        cmd.ExecuteNonQuery();

        // Act
        var result = TriggerGenerator.GetTableColumns(_db, "Customers");

        // Assert
        Assert.True(result is ColumnInfoListOk);
        var columns = ((ColumnInfoListOk)result).Value;
        Assert.Equal(4, columns.Count);

        var pk = columns.First(c => c.IsPrimaryKey);
        Assert.Equal("CustomerId", pk.Name);
    }

    [Fact]
    public void GetTableColumns_NonExistentTable_ReturnsEmptyList()
    {
        // Act
        var result = TriggerGenerator.GetTableColumns(_db, "NonExistent");

        // Assert
        Assert.True(result is ColumnInfoListOk);
        Assert.Empty(((ColumnInfoListOk)result).Value);
    }

    [Fact]
    public void Triggers_InsertLogsToSyncLog()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);
        SyncSchema.SetOriginId(_db, OriginId);
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Events (
                EventId TEXT PRIMARY KEY,
                Title TEXT
            );
            """;
        cmd.ExecuteNonQuery();
        TriggerGenerator.CreateTriggers(_db, "Events", NullLogger.Instance);

        // Act
        cmd.CommandText = "INSERT INTO Events (EventId, Title) VALUES ('e1', 'Meeting')";
        cmd.ExecuteNonQuery();

        // Assert
        cmd.CommandText = "SELECT COUNT(*) FROM _sync_log WHERE table_name = 'Events'";
        Assert.Equal(1L, cmd.ExecuteScalar());

        cmd.CommandText =
            "SELECT operation FROM _sync_log WHERE table_name = 'Events' ORDER BY version DESC LIMIT 1";
        Assert.Equal("insert", cmd.ExecuteScalar());
    }

    [Fact]
    public void Triggers_UpdateLogsToSyncLog()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);
        SyncSchema.SetOriginId(_db, OriginId);
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Events (
                EventId TEXT PRIMARY KEY,
                Title TEXT
            );
            """;
        cmd.ExecuteNonQuery();
        TriggerGenerator.CreateTriggers(_db, "Events", NullLogger.Instance);

        cmd.CommandText = "INSERT INTO Events (EventId, Title) VALUES ('e1', 'Meeting')";
        cmd.ExecuteNonQuery();

        // Act
        cmd.CommandText = "UPDATE Events SET Title = 'Updated Meeting' WHERE EventId = 'e1'";
        cmd.ExecuteNonQuery();

        // Assert
        cmd.CommandText =
            "SELECT operation FROM _sync_log WHERE table_name = 'Events' ORDER BY version DESC LIMIT 1";
        Assert.Equal("update", cmd.ExecuteScalar());
    }

    [Fact]
    public void Triggers_DeleteLogsToSyncLog()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);
        SyncSchema.SetOriginId(_db, OriginId);
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Events (
                EventId TEXT PRIMARY KEY,
                Title TEXT
            );
            """;
        cmd.ExecuteNonQuery();
        TriggerGenerator.CreateTriggers(_db, "Events", NullLogger.Instance);

        cmd.CommandText = "INSERT INTO Events (EventId, Title) VALUES ('e1', 'Meeting')";
        cmd.ExecuteNonQuery();

        // Act
        cmd.CommandText = "DELETE FROM Events WHERE EventId = 'e1'";
        cmd.ExecuteNonQuery();

        // Assert
        cmd.CommandText =
            "SELECT operation FROM _sync_log WHERE table_name = 'Events' ORDER BY version DESC LIMIT 1";
        Assert.Equal("delete", cmd.ExecuteScalar());

        cmd.CommandText =
            "SELECT payload FROM _sync_log WHERE table_name = 'Events' ORDER BY version DESC LIMIT 1";
        Assert.True(cmd.ExecuteScalar() is DBNull); // Tombstone has NULL payload
    }

    [Fact]
    public void Triggers_SyncActiveSuppressions()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);
        SyncSchema.SetOriginId(_db, OriginId);
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Events (
                EventId TEXT PRIMARY KEY,
                Title TEXT
            );
            """;
        cmd.ExecuteNonQuery();
        TriggerGenerator.CreateTriggers(_db, "Events", NullLogger.Instance);

        // Set sync_active to suppress triggers
        cmd.CommandText = "UPDATE _sync_session SET sync_active = 1";
        cmd.ExecuteNonQuery();

        // Act - Insert while sync_active is set
        cmd.CommandText = "INSERT INTO Events (EventId, Title) VALUES ('e1', 'SuppressedEvent')";
        cmd.ExecuteNonQuery();

        // Assert - No entry should be logged
        cmd.CommandText = "SELECT COUNT(*) FROM _sync_log WHERE table_name = 'Events'";
        Assert.Equal(0L, cmd.ExecuteScalar());

        // Cleanup - Reset sync_active
        cmd.CommandText = "UPDATE _sync_session SET sync_active = 0";
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public void Triggers_LogPrimaryKeyAsJson()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);
        SyncSchema.SetOriginId(_db, OriginId);
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Records (
                RecordId TEXT PRIMARY KEY,
                Data TEXT
            );
            """;
        cmd.ExecuteNonQuery();
        TriggerGenerator.CreateTriggers(_db, "Records", NullLogger.Instance);

        // Act
        cmd.CommandText = "INSERT INTO Records (RecordId, Data) VALUES ('r123', 'test data')";
        cmd.ExecuteNonQuery();

        // Assert
        cmd.CommandText = "SELECT pk_value FROM _sync_log WHERE table_name = 'Records' LIMIT 1";
        var pkValue = cmd.ExecuteScalar() as string;

        Assert.NotNull(pkValue);
        Assert.Contains("RecordId", pkValue);
        Assert.Contains("r123", pkValue);
    }

    [Fact]
    public void Triggers_LogPayloadAsJson()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);
        SyncSchema.SetOriginId(_db, OriginId);
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Records (
                RecordId TEXT PRIMARY KEY,
                Name TEXT,
                Value INTEGER
            );
            """;
        cmd.ExecuteNonQuery();
        TriggerGenerator.CreateTriggers(_db, "Records", NullLogger.Instance);

        // Act
        cmd.CommandText =
            "INSERT INTO Records (RecordId, Name, Value) VALUES ('r1', 'TestName', 42)";
        cmd.ExecuteNonQuery();

        // Assert
        cmd.CommandText = "SELECT payload FROM _sync_log WHERE table_name = 'Records' LIMIT 1";
        var payload = cmd.ExecuteScalar() as string;

        Assert.NotNull(payload);
        Assert.Contains("RecordId", payload);
        Assert.Contains("Name", payload);
        Assert.Contains("Value", payload);
        Assert.Contains("TestName", payload);
    }

    [Fact]
    public void Triggers_LogOriginId()
    {
        // Arrange
        var myOrigin = "my-unique-origin";
        SyncSchema.CreateSchema(_db);
        SyncSchema.SetOriginId(_db, myOrigin);
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Tasks (
                TaskId TEXT PRIMARY KEY,
                Title TEXT
            );
            """;
        cmd.ExecuteNonQuery();
        TriggerGenerator.CreateTriggers(_db, "Tasks", NullLogger.Instance);

        // Act
        cmd.CommandText = "INSERT INTO Tasks (TaskId, Title) VALUES ('t1', 'Do something')";
        cmd.ExecuteNonQuery();

        // Assert
        cmd.CommandText = "SELECT origin FROM _sync_log WHERE table_name = 'Tasks' LIMIT 1";
        var origin = cmd.ExecuteScalar() as string;

        Assert.Equal(myOrigin, origin);
    }

    [Fact]
    public void Triggers_LogTimestamp()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);
        SyncSchema.SetOriginId(_db, OriginId);
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Tasks (
                TaskId TEXT PRIMARY KEY,
                Title TEXT
            );
            """;
        cmd.ExecuteNonQuery();
        TriggerGenerator.CreateTriggers(_db, "Tasks", NullLogger.Instance);

        // Act
        cmd.CommandText = "INSERT INTO Tasks (TaskId, Title) VALUES ('t1', 'Task')";
        cmd.ExecuteNonQuery();

        // Assert
        cmd.CommandText = "SELECT timestamp FROM _sync_log WHERE table_name = 'Tasks' LIMIT 1";
        var timestamp = cmd.ExecuteScalar() as string;

        Assert.NotNull(timestamp);
        Assert.Matches(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}", timestamp);
    }

    #endregion

    #region Table Without Primary Key

    [Fact]
    public void GenerateTriggersFromSchema_TableWithoutPK_ReturnsError()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);
        DatabaseCommands.Execute(
            connection: _db,
            configure: command =>
                command.CommandText = """
                CREATE TABLE NoPrimaryKey (
                    Name TEXT,
                    Value INTEGER
                );
                """
        );

        // Act
        var result = TriggerGenerator.GenerateTriggersFromSchema(
            _db,
            "NoPrimaryKey",
            NullLogger.Instance
        );

        // Assert
        Assert.True(result is StringSyncError);
        var error = ((StringSyncError)result).Value;
        Assert.True(error is SyncErrorDatabase);
        Assert.Contains("no primary key", ((SyncErrorDatabase)error).Message.ToLowerInvariant());
    }

    #endregion

    #region SyncSessionManager Tests

    [Fact]
    public void EnableSuppression_ValidConnection_SetsFlag()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);

        // Act
        var result = SyncSessionManager.EnableSuppression(_db);

        // Assert
        Assert.True(result is BoolSyncOk);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT sync_active FROM _sync_session";
        Assert.Equal(1L, cmd.ExecuteScalar());
    }

    [Fact]
    public void DisableSuppression_ValidConnection_ClearsFlag()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);
        SyncSessionManager.EnableSuppression(_db);

        // Act
        var result = SyncSessionManager.DisableSuppression(_db);

        // Assert
        Assert.True(result is BoolSyncOk);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT sync_active FROM _sync_session";
        Assert.Equal(0L, cmd.ExecuteScalar());
    }

    [Fact]
    public void IsSuppressionActive_WhenActive_ReturnsTrue()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);
        SyncSessionManager.EnableSuppression(_db);

        // Act
        var result = SyncSessionManager.IsSuppressionActive(_db);

        // Assert
        Assert.True(result is BoolSyncOk);
        Assert.True(((BoolSyncOk)result).Value);
    }

    [Fact]
    public void IsSuppressionActive_WhenInactive_ReturnsFalse()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);

        // Act
        var result = SyncSessionManager.IsSuppressionActive(_db);

        // Assert
        Assert.True(result is BoolSyncOk);
        Assert.False(((BoolSyncOk)result).Value);
    }

    [Fact]
    public void EnableDisable_MultipleToggles_WorksCorrectly()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);

        // Act & Assert
        SyncSessionManager.EnableSuppression(_db);
        Assert.True(((BoolSyncOk)SyncSessionManager.IsSuppressionActive(_db)).Value);

        SyncSessionManager.DisableSuppression(_db);
        Assert.False(((BoolSyncOk)SyncSessionManager.IsSuppressionActive(_db)).Value);

        SyncSessionManager.EnableSuppression(_db);
        Assert.True(((BoolSyncOk)SyncSessionManager.IsSuppressionActive(_db)).Value);

        SyncSessionManager.DisableSuppression(_db);
        Assert.False(((BoolSyncOk)SyncSessionManager.IsSuppressionActive(_db)).Value);
    }

    #endregion

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch
            {
                /* File may be locked */
            }
        }
    }
}
