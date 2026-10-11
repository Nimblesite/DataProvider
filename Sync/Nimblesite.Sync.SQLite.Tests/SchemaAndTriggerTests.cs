// Implements [CI-TEST-SHARED-COMMAND].
using Microsoft.Data.Sqlite;
using Tests.Shared;
using Xunit;
using static global::Tests.Shared.ResultAssertions;
using static global::Tests.Shared.StringAssertions;

namespace Nimblesite.Sync.SQLite.Tests;

/// <summary>
/// Integration tests for SyncSchema and TriggerGenerator.
/// Tests schema creation, trigger generation, and schema metadata operations.
/// NO MOCKS - real SQLite databases only!
/// </summary>
public sealed partial class SchemaAndTriggerTests : IDisposable
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
        cmd.CommandText = Data_CreateSchema_EmptyDatabase_CreatesAllTables_1;

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
    [MemberData(nameof(CreateSchema_CreatesSyncLogIndexes_CasesInlineCases))]
    // CreateSchema_CreatesSubscriptionIndexes

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
        cmd.CommandText = Data_CreateSchema_CreatesSyncClientsIndex_1;

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
        ContainsAll(
            actual: triggers,
            expected:
            [
                "Person_sync_insert",
                "Person_sync_update",
                "Person_sync_delete",
                "AFTER INSERT ON Person",
                "AFTER UPDATE ON Person",
                "AFTER DELETE ON Person",
            ]
        );
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
        ContainsAll(actual: triggers, expected: ["sync_active", "_sync_session"]);
    }

    [Fact]
    public void GenerateTriggers_BuildsJsonPayload()
    {
        // Arrange
        var columns = new List<string> { "Id", "Name", "Email" };

        // Act
        var triggers = TriggerGenerator.GenerateTriggers("Users", columns, "Id");

        // Assert
        ContainsAll(
            actual: triggers,
            expected: ["json_object", "'Id', NEW.Id", "'Name', NEW.Name", "'Email', NEW.Email"]
        );
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
                command.CommandText = Data_GenerateTriggersFromSchema_ValidTable_ReturnsTriggers_1
        );

        // Act
        var result = TriggerGenerator.GenerateTriggersFromSchema(
            _db,
            "Products",
            NullLogger.Instance
        );

        // Assert

        var triggers = Ok(actual: result).Value;
        ContainsAll(
            actual: triggers,
            expected: ["Products_sync_insert", "Products_sync_update", "Products_sync_delete"]
        );
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
        cmd.CommandText = Data_CreateTriggers_ValidTable_CreatesTriggers_1;
        cmd.ExecuteNonQuery();

        // Act
        var result = TriggerGenerator.CreateTriggers(_db, "Orders", NullLogger.Instance);

        // Assert
        Assert.True(result is BoolSyncOk);

        // Verify triggers exist
        cmd.CommandText = Data_CreateTriggers_ValidTable_CreatesTriggers_2;

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
        cmd.CommandText = Data_DropTriggers_ExistingTriggers_RemovesThem_1;
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
        cmd.CommandText = Data_GetTableColumns_ValidTable_ReturnsColumns_1;
        cmd.ExecuteNonQuery();

        // Act
        var result = TriggerGenerator.GetTableColumns(_db, "Customers");

        // Assert

        var columns = Ok(actual: result).Value;
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
        cmd.CommandText = Data_Triggers_InsertLogsToSyncLog_1;
        CreateTriggersAndPrepareInsert(
            input0: cmd,
            value0: "Events",
            value1: "INSERT INTO Events (EventId, Title) VALUES ('e1', 'Meeting')",
            value2: "SELECT COUNT(*) FROM _sync_log WHERE table_name = 'Events'"
        );
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
        cmd.CommandText = Data_Triggers_UpdateLogsToSyncLog_1;
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
        cmd.CommandText = Data_Triggers_DeleteLogsToSyncLog_1;
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
        cmd.CommandText = Data_Triggers_SyncActiveSuppressions_1;
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
        cmd.CommandText = Data_Triggers_LogPrimaryKeyAsJson_1;
        CreateTriggersAndPrepareInsert(
            input0: cmd,
            value0: "Records",
            value1: "INSERT INTO Records (RecordId, Data) VALUES ('r123', 'test data')",
            value2: "SELECT pk_value FROM _sync_log WHERE table_name = 'Records' LIMIT 1"
        );
        var pkValue = cmd.ExecuteScalar() as string;

        Assert.NotNull(pkValue);
        ContainsAll(actual: pkValue, expected: ["RecordId", "r123"]);
    }

    [Fact]
    public void Triggers_LogPayloadAsJson()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);
        SyncSchema.SetOriginId(_db, OriginId);
        using var cmd = _db.CreateCommand();
        cmd.CommandText = Data_Triggers_LogPayloadAsJson_1;
        CreateTriggersAndPrepareInsert(
            input0: cmd,
            value0: "Records",
            value1: "INSERT INTO Records (RecordId, Name, Value) VALUES ('r1', 'TestName', 42)",
            value2: "SELECT payload FROM _sync_log WHERE table_name = 'Records' LIMIT 1"
        );
        var payload = cmd.ExecuteScalar() as string;

        Assert.NotNull(payload);
        ContainsAll(actual: payload, expected: ["RecordId", "Name", "Value", "TestName"]);
    }

    [Fact]
    public void Triggers_LogOriginId()
    {
        // Arrange
        var myOrigin = "my-unique-origin";
        SyncSchema.CreateSchema(_db);
        SyncSchema.SetOriginId(_db, myOrigin);
        using var cmd = _db.CreateCommand();
        cmd.CommandText = Data_Triggers_LogOriginId_1;
        CreateTriggersAndPrepareInsert(
            input0: cmd,
            value0: "Tasks",
            value1: "INSERT INTO Tasks (TaskId, Title) VALUES ('t1', 'Do something')",
            value2: "SELECT origin FROM _sync_log WHERE table_name = 'Tasks' LIMIT 1"
        );
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
        cmd.CommandText = Data_Triggers_LogTimestamp_1;
        CreateTriggersAndPrepareInsert(
            input0: cmd,
            value0: "Tasks",
            value1: "INSERT INTO Tasks (TaskId, Title) VALUES ('t1', 'Task')",
            value2: "SELECT timestamp FROM _sync_log WHERE table_name = 'Tasks' LIMIT 1"
        );
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
                command.CommandText = Data_GenerateTriggersFromSchema_TableWithoutPK_ReturnsError_1
        );

        // Act
        var result = TriggerGenerator.GenerateTriggersFromSchema(
            _db,
            "NoPrimaryKey",
            NullLogger.Instance
        );

        // Assert

        var error = Expected<StringSyncError>(actual: result).Value;
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

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void CreateTriggersAndPrepareInsert(
        global::Microsoft.Data.Sqlite.SqliteCommand input0,
        string value0,
        string value1,
        string value2
    )
    {
        input0.ExecuteNonQuery();
        TriggerGenerator.CreateTriggers(_db, value0, NullLogger.Instance);

        // Act
        input0.CommandText = value1;
        input0.ExecuteNonQuery();

        // Assert
        input0.CommandText = value2;
    }
}
