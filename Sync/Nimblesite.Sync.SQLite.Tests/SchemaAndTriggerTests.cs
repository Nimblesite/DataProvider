// Implements [CI-TEST-SHARED-COMMAND].

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
    private readonly string _dbPath = global::Tests.Shared.TempSqliteDatabase.PathFor(
        "schemaandtriggertests"
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
        global::Tests.Shared.ResultAssertions.Ok(actual: SyncSchema.CreateSchema(_db));

        // Verify tables exist
        using var cmd = _db.CreateCommand();
        cmd.CommandText = Data_CreateSchema_EmptyDatabase_CreatesAllTables_1;

        var tables = DatabaseCommands.ReadStrings(cmd);

        global::Nimblesite.Sync.SQLite.Tests.SpecComplianceTests.VerifyDatabaseWorkflow3(
            input0: tables,
            value0: "_sync_clients",
            value1: "_sync_log",
            value2: "_sync_session",
            value3: "_sync_state"
        );
    }

    [Fact]
    public void CreateSchema_IdempotentMultipleCalls_Succeeds()
    {
        // Act
        var result1 = SyncSchema.CreateSchema(_db);
        var result2 = SyncSchema.CreateSchema(_db);
        var result3 = SyncSchema.CreateSchema(_db);

        // Assert
        _ = global::Tests.Shared.ResultAssertions.Expected<BoolSyncOk>(actual: result1);
        _ = global::Tests.Shared.ResultAssertions.Expected<BoolSyncOk>(actual: result2);
        _ = global::Tests.Shared.ResultAssertions.Expected<BoolSyncOk>(actual: result3);
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
        var syncActive = global::Tests.Shared.DatabaseCommands.Scalar(
            connection: _db,
            configure: command => command.CommandText = "SELECT sync_active FROM _sync_session"
        );

        Assert.Equal(0L, syncActive);
    }

    [Fact]
    public void SetOriginId_ValidOrigin_StoresValue()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);

        // Act
        global::Tests.Shared.ResultAssertions.Ok(actual: SyncSchema.SetOriginId(_db, OriginId));

        var storedOrigin =
            global::Tests.Shared.DatabaseCommands.Scalar(
                connection: _db,
                configure: command =>
                    command.CommandText = "SELECT value FROM _sync_state WHERE key = 'origin_id'"
            ) as string;

        Assert.Equal(OriginId, storedOrigin);
    }

    [Fact]
    public void GetOriginId_AfterSet_ReturnsValue()
    {
        // Arrange
        global::Tests.Shared.TempSqliteDatabase.InitializeSync(connection: _db, origin: OriginId);

        // Act
        var result = SyncSchema.GetOriginId(_db);

        // Assert
        _ = global::Tests.Shared.ResultAssertions.Expected<StringSyncOk>(actual: result);
        Assert.Equal(OriginId, (Ok(actual: result)).Value);
    }

    [Fact]
    public void GetOriginId_BeforeSet_ReturnsEmptyString()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);

        // Act
        var result = SyncSchema.GetOriginId(_db);

        // Assert
        _ = global::Tests.Shared.ResultAssertions.Expected<StringSyncOk>(actual: result);
        Assert.Equal("", (Ok(actual: result)).Value);
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

        var indexes = DatabaseCommands.ReadStrings(cmd);

        Assert.Contains(caseValue2, indexes);
        Assert.Contains(caseValue3, indexes);
    }

    [Fact]
    public void CreateSchema_CreatesSyncClientsIndex()
    {
        // Act
        SyncSchema.CreateSchema(_db);

        // Assert

        var indexName =
            global::Tests.Shared.DatabaseCommands.Scalar(
                connection: _db,
                configure: command =>
                    command.CommandText = Data_CreateSchema_CreatesSyncClientsIndex_1
            ) as string;

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
        // Act
        // Assert
        // Act
        // Assert
        SqlFragments(
            read: () =>
                TriggerGenerator.GenerateTriggersFromSchema(_db, "Products", NullLogger.Instance),
            expected: ["Products_sync_insert", "Products_sync_update", "Products_sync_delete"]
        );
    }

    [Fact]
    public void GenerateTriggersFromSchema_NonExistentTable_ReturnsError()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);

        // Act
        global::Tests.Shared.ResultAssertions.Error(
            actual: TriggerGenerator.GenerateTriggersFromSchema(
                _db,
                "NonExistentTable",
                NullLogger.Instance
            )
        );
    }

    [Fact]
    public void CreateTriggers_ValidTable_CreatesTriggers()
    {
        // Arrange
        using var cmd = CreateSyncCommand();
        cmd.CommandText = Data_CreateTriggers_ValidTable_CreatesTriggers_1;
        cmd.ExecuteNonQuery();

        // Act
        global::Tests.Shared.ResultAssertions.Ok(
            actual: TriggerGenerator.CreateTriggers(_db, "Orders", NullLogger.Instance)
        );

        // Verify triggers exist
        cmd.CommandText = Data_CreateTriggers_ValidTable_CreatesTriggers_2;

        global::Nimblesite.Sync.SQLite.Tests.SpecComplianceTests.VerifySyncLoopWorkflow4(
            input0: cmd,
            value0: "Orders_sync_delete",
            value1: "Orders_sync_insert",
            value2: "Orders_sync_update"
        );
    }

    [Fact]
    public void CreateTriggers_NonExistentTable_ReturnsError()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);

        // Act
        global::Tests.Shared.ResultAssertions.Error(
            actual: TriggerGenerator.CreateTriggers(_db, "NonExistent", NullLogger.Instance)
        );
    }

    [Fact]
    public void DropTriggers_ExistingTriggers_RemovesThem()
    {
        // Arrange
        using var cmd = CreateSyncCommand();
        CreateTableAndTriggers(
            command: cmd,
            createTableSql: Data_DropTriggers_ExistingTriggers_RemovesThem_1,
            db: _db,
            table: "Items"
        );

        // Verify triggers exist
        cmd.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type='trigger' AND name LIKE 'Items_sync%'";
        Assert.Equal(3L, cmd.ExecuteScalar());

        // Act
        global::Tests.Shared.ResultAssertions.Ok(
            actual: TriggerGenerator.DropTriggers(_db, "Items", NullLogger.Instance)
        );
        Assert.Equal(0L, cmd.ExecuteScalar());
    }

    [Fact]
    public void DropTriggers_NonExistentTriggers_Succeeds()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);

        // Act
        global::Tests.Shared.ResultAssertions.Ok(
            actual: TriggerGenerator.DropTriggers(_db, "NonExistent", NullLogger.Instance)
        );
    }

    [Fact]
    public void GetTableColumns_ValidTable_ReturnsColumns()
    {
        // Arrange
        using var cmd = _db.CreateCommand();
        cmd.CommandText = Data_GetTableColumns_ValidTable_ReturnsColumns_1;
        cmd.ExecuteNonQuery();

        // Act
        // Act
        // Assert
        var columns = Ok(read: () => TriggerGenerator.GetTableColumns(_db, "Customers")).Value;
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
        _ = global::Tests.Shared.ResultAssertions.Expected<ColumnInfoListOk>(actual: result);
        Assert.Empty((Ok(actual: result)).Value);
    }

    [Fact]
    public void Triggers_InsertLogsToSyncLog()
    {
        // Arrange
        using var cmd = CreateSyncCommand();
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
        using var cmd = CreateSyncCommand();
        VerifySharedChecks3(
            input0: cmd,
            input1: Data_Triggers_UpdateLogsToSyncLog_1,
            input2: _db,
            value2: "UPDATE Events SET Title = 'Updated Meeting' WHERE EventId = 'e1'",
            value4: "update"
        );
    }

    [Fact]
    public void Triggers_DeleteLogsToSyncLog()
    {
        // Arrange
        using var cmd = CreateSyncCommand();
        VerifySharedChecks3(
            input0: cmd,
            input1: Data_Triggers_DeleteLogsToSyncLog_1,
            input2: _db,
            value2: "DELETE FROM Events WHERE EventId = 'e1'",
            value4: "delete"
        );

        cmd.CommandText =
            "SELECT payload FROM _sync_log WHERE table_name = 'Events' ORDER BY version DESC LIMIT 1";
        Assert.True(cmd.ExecuteScalar() is DBNull); // Tombstone has NULL payload
    }

    [Fact]
    public void Triggers_SyncActiveSuppressions()
    {
        // Arrange
        using var cmd = CreateSyncCommand();
        CreateTableAndTriggers(
            command: cmd,
            createTableSql: Data_Triggers_SyncActiveSuppressions_1,
            db: _db,
            table: "Events"
        );

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
        using var cmd = CreateSyncCommand();
        var pkValue = VerifyTriggerLogQuery(
            command: cmd,
            createTableSql: Data_Triggers_LogPrimaryKeyAsJson_1,
            table: "Records",
            insertSql: "INSERT INTO Records (RecordId, Data) VALUES ('r123', 'test data')",
            selectSql: "SELECT pk_value FROM _sync_log WHERE table_name = 'Records' LIMIT 1"
        );
        ContainsAll(actual: pkValue, expected: ["RecordId", "r123"]);
    }

    [Fact]
    public void Triggers_LogPayloadAsJson()
    {
        // Arrange
        using var cmd = CreateSyncCommand();
        var payload = VerifyTriggerLogQuery(
            command: cmd,
            createTableSql: Data_Triggers_LogPayloadAsJson_1,
            table: "Records",
            insertSql: "INSERT INTO Records (RecordId, Name, Value) VALUES ('r1', 'TestName', 42)",
            selectSql: "SELECT payload FROM _sync_log WHERE table_name = 'Records' LIMIT 1"
        );
        ContainsAll(actual: payload, expected: ["RecordId", "Name", "Value", "TestName"]);
    }

    [Fact]
    public void Triggers_LogOriginId()
    {
        // Arrange
        var myOrigin = "my-unique-origin";
        global::Tests.Shared.TempSqliteDatabase.InitializeSync(connection: _db, origin: myOrigin);
        using var cmd = _db.CreateCommand();
        var origin = VerifySharedChecks1(
            input0: cmd,
            input1: Data_Triggers_LogOriginId_1,
            value0: "Tasks",
            value1: "INSERT INTO Tasks (TaskId, Title) VALUES ('t1', 'Do something')",
            value2: "SELECT origin FROM _sync_log WHERE table_name = 'Tasks' LIMIT 1"
        );

        Assert.Equal(myOrigin, origin);
    }

    [Fact]
    public void Triggers_LogTimestamp()
    {
        // Arrange
        using var cmd = CreateSyncCommand();
        var timestamp = VerifyTriggerLogQuery(
            command: cmd,
            createTableSql: Data_Triggers_LogTimestamp_1,
            table: "Tasks",
            insertSql: "INSERT INTO Tasks (TaskId, Title) VALUES ('t1', 'Task')",
            selectSql: "SELECT timestamp FROM _sync_log WHERE table_name = 'Tasks' LIMIT 1"
        );
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
        _ = global::Tests.Shared.ResultAssertions.Expected<SyncErrorDatabase>(actual: error);
        Assert.Contains("no primary key", ((SyncErrorDatabase)error).Message.ToLowerInvariant());
    }

    #endregion

    #region SyncSessionManager Tests

    [Fact]
    public void EnableSuppression_ValidConnection_SetsFlag()
    {
        // Arrange
        VerifyDatabaseWorkflow1(
            input0: _db,
            value0: SyncSchema.CreateSchema,
            value1: SyncSessionManager.EnableSuppression,
            value2: 1L
        );
    }

    [Fact]
    public void DisableSuppression_ValidConnection_ClearsFlag()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);
        VerifyDatabaseWorkflow1(
            input0: _db,
            value0: SyncSessionManager.EnableSuppression,
            value1: SyncSessionManager.DisableSuppression,
            value2: 0L
        );
    }

    [Fact]
    public void IsSuppressionActive_WhenActive_ReturnsTrue()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);
        SyncSessionManager.EnableSuppression(_db);

        // Act
        AssertOkValue(SyncSessionManager.IsSuppressionActive(_db), value => Assert.True(value));
    }

    [Fact]
    public void IsSuppressionActive_WhenInactive_ReturnsFalse()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);

        // Act
        AssertOkValue(SyncSessionManager.IsSuppressionActive(_db), value => Assert.False(value));
    }

    [Fact]
    public void EnableDisable_MultipleToggles_WorksCorrectly()
    {
        // Arrange
        SyncSchema.CreateSchema(_db);

        // Act & Assert
        SyncSessionManager.EnableSuppression(_db);
        Assert.True((Ok(actual: SyncSessionManager.IsSuppressionActive(_db))).Value);

        SyncSessionManager.DisableSuppression(_db);
        Assert.False((Ok(actual: SyncSessionManager.IsSuppressionActive(_db))).Value);

        SyncSessionManager.EnableSuppression(_db);
        Assert.True((Ok(actual: SyncSessionManager.IsSuppressionActive(_db))).Value);

        SyncSessionManager.DisableSuppression(_db);
        Assert.False((Ok(actual: SyncSessionManager.IsSuppressionActive(_db))).Value);
    }

    #endregion

    public void Dispose()
    {
        global::Nimblesite.Sync.SQLite.Tests.EndToEndSyncTests.VerifyReusableWorkflow5(
            input0: _db,
            input1: _dbPath
        );
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

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private string? VerifySharedChecks1(
        SqliteCommand input0,
        string input1,
        string value0,
        string value1,
        string value2
    )
    {
        input0.CommandText = input1;
        CreateTriggersAndPrepareInsert(
            input0: input0,
            value0: value0,
            value1: value1,
            value2: value2
        );
        var pkValue = input0.ExecuteScalar() is string text ? text : null;

        return pkValue;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void VerifySharedChecks3(
        SqliteCommand input0,
        string input1,
        SqliteConnection input2,
        string value2,
        string value4
    )
    {
        CreateTableAndTriggers(
            command: input0,
            createTableSql: input1,
            db: input2,
            table: "Events"
        );

        input0.CommandText = "INSERT INTO Events (EventId, Title) VALUES ('e1', 'Meeting')";
        input0.ExecuteNonQuery();

        // Act
        input0.CommandText = value2;
        input0.ExecuteNonQuery();

        // Assert
        input0.CommandText =
            "SELECT operation FROM _sync_log WHERE table_name = 'Events' ORDER BY version DESC LIMIT 1";
        Assert.Equal(value4, input0.ExecuteScalar());
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static string VerifyReusableWorkflow4(
        Func<SqliteCommand, string, string, string, string, string?> input0,
        SqliteCommand input1,
        global::System.String value0,
        string value1,
        string value2,
        string value3
    )
    {
        var pkValue = input0(arg1: input1, arg2: value0, arg3: value1, arg4: value2, arg5: value3);
        Assert.NotNull(pkValue);
        return pkValue;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void AssertOkValue<TValue, TError>(
        Outcome.Result<TValue, TError> result,
        Action<TValue> verify
    )
    {
        _ = global::Tests.Shared.ResultAssertions.ExactOk(actual: result);
        verify((Ok(actual: result)).Value);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private SqliteCommand CreateSyncCommand()
    {
        global::Tests.Shared.TempSqliteDatabase.InitializeSync(connection: _db, origin: OriginId);
        return _db.CreateCommand();
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void CreateTableAndTriggers(
        SqliteCommand command,
        string createTableSql,
        SqliteConnection db,
        string table
    )
    {
        command.CommandText = createTableSql;
        command.ExecuteNonQuery();
        TriggerGenerator.CreateTriggers(db, table, NullLogger.Instance);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private string? VerifyTriggerLogQuery(
        SqliteCommand command,
        string createTableSql,
        string table,
        string insertSql,
        string selectSql
    ) =>
        VerifyReusableWorkflow4(
            input0: VerifySharedChecks1,
            input1: command,
            value0: createTableSql,
            value1: table,
            value2: insertSql,
            value3: selectSql
        );

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyDatabaseWorkflow1(
        SqliteConnection input0,
        global::System.Func<
            global::Microsoft.Data.Sqlite.SqliteConnection,
            global::Outcome.Result<global::System.Boolean, global::Nimblesite.Sync.Core.SyncError>
        > value0,
        global::System.Func<
            global::Microsoft.Data.Sqlite.SqliteConnection,
            global::Outcome.Result<global::System.Boolean, global::Nimblesite.Sync.Core.SyncError>
        > value1,
        object value2
    )
    {
        value0(arg: input0);

        // Act
        global::Tests.Shared.ResultAssertions.Ok(actual: value1(arg: input0));

        Assert.Equal(
            value2,
            global::Tests.Shared.DatabaseCommands.Scalar(
                connection: input0,
                configure: command => command.CommandText = "SELECT sync_active FROM _sync_session"
            )
        );
    }
}
