// Implements [CI-TEST-SHARED-COMMAND].
using Tests.Shared;
using static global::Tests.Shared.ResultAssertions;

namespace Nimblesite.Sync.Postgres.Tests;

/// <summary>
/// Integration tests for Postgres repositories.
/// Uses the shared postgres container; each test gets its own database.
/// </summary>
[Collection(PostgresTestSuite.Name)]
public sealed partial class PostgresRepositoryTests(PostgresContainerFixture fixture)
    : IAsyncLifetime
{
    private NpgsqlConnection _conn = null!;
    private static readonly ILogger Logger = NullLogger.Instance;

    public async Task InitializeAsync()
    {
        _conn = await fixture.CreateDatabaseAsync("sync_repo_test").ConfigureAwait(false);

        // Create schema
        var schemaResult = PostgresSyncSchema.CreateSchema(_conn);
        global::Tests.Shared.ResultAssertions.ExactOk(actual: schemaResult);
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

        var list = (Assert.IsType<SyncLogListOk>(result)).Value;
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
        global::Tests.Shared.ResultAssertions.ExactOk(actual: insertResult);

        var fetchResult = PostgresSyncLogRepository.FetchChanges(_conn, 0, 100);

        var list = (Assert.IsType<SyncLogListOk>(fetchResult)).Value;
        Assert.Single(list);
        Assert.Equal("TestTable", list[0].TableName);
        Assert.Equal("{\"Id\":\"t1\"}", list[0].PkValue);
        Assert.Equal(SyncOperation.Insert, list[0].Operation);
    }

    [Fact]
    public void FetchChanges_RespectsFromVersion()
    {
        // Insert 3 entries
        VerifySyncLoopWorkflow1(
            input0: VerifySharedChecks4,
            input1: _conn,
            value1: 3,
            value3: 100,
            value4: 3
        );

        // Fetch from version 1 - should get 2
        // Fetch from version 1 - should get 2
        var from1List = FetchLog(fromVersion: 1);
        Assert.Equal(2, from1List.Count);

        // Fetch from version 2 - should get 1
        // Fetch from version 2 - should get 1
        var from2List = FetchLog(fromVersion: 2);
        Assert.Single(from2List);
    }

    [Fact]
    public void FetchChanges_RespectsLimit()
    {
        // Insert 10 entries
        VerifySyncLoopWorkflow1(
            input0: VerifySharedChecks4,
            input1: _conn,
            value1: 10,
            value3: 5,
            value4: 5
        );
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(IndependentGetMaxVersion_EmptyLog_ReturnsZero_ContextCasesData))]
    public void GetMaxVersion_EmptyLog_ReturnsZero_ContextCases(
        string caseName,
        global::System.Func<
            global::Npgsql.NpgsqlConnection,
            global::Outcome.Result<global::System.Int64, global::Nimblesite.Sync.Core.SyncError>
        > value0
    )
    {
        NamedCase(caseName: caseName);
        ExerciseOperationCaseMatrix1(input0: _conn, value0: value0);
    }

    [Fact]
    public void GetMaxVersion_WithEntries_ReturnsMax()
    {
        // Insert 3 entries
        for (var i = 1; i <= 3; i++)
        {
            VerifySharedChecks4(input0: i, input1: _conn);
        }

        var max = (Ok(read: () => PostgresSyncLogRepository.GetMaxVersion(_conn))).Value;
        Assert.Equal(3, max);
    }

    [Fact]
    public void UpdateLastServerVersion_And_Get_RoundTrips()
    {
        var updateResult = PostgresSyncLogRepository.UpdateLastServerVersion(_conn, 42);
        global::Tests.Shared.ResultAssertions.ExactOk(actual: updateResult);

        var getResult = PostgresSyncLogRepository.GetLastServerVersion(_conn);

        var version = (global::Tests.Shared.ResultAssertions.ExactOk(actual: getResult)).Value;
        Assert.Equal(42, version);
    }

    [Fact]
    public void UpdateLastServerVersion_Upserts()
    {
        PostgresSyncLogRepository.UpdateLastServerVersion(_conn, 10);
        PostgresSyncLogRepository.UpdateLastServerVersion(_conn, 20);
        PostgresSyncLogRepository.UpdateLastServerVersion(_conn, 30);

        var version = (Ok(read: () => PostgresSyncLogRepository.GetLastServerVersion(_conn))).Value;
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
            global::Tests.Shared.ResultAssertions.ExactOk(actual: result);
        }

        var list = FetchLog(fromVersion: 0);
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

        var list = (Assert.IsType<SyncClientListOk>(result)).Value;
        Assert.Empty(list);
    }

    [Fact]
    public void Upsert_And_GetAll_RoundTrips()
    {
        var client = Client(origin: "client-001", version: 10);

        var upsertResult = PostgresSyncClientRepository.Upsert(_conn, client);
        global::Tests.Shared.ResultAssertions.ExactOk(actual: upsertResult);

        var list = GetAllClients();
        Assert.Single(list);
        Assert.Equal("client-001", list[0].OriginId);
        Assert.Equal(10, list[0].LastSyncVersion);
    }

    [Fact]
    public void GetByOrigin_NotFound_ReturnsNull()
    {
        var result = PostgresSyncClientRepository.GetByOrigin(_conn, "nonexistent");

        var client = (global::Tests.Shared.ResultAssertions.ExactOk(actual: result)).Value;
        Assert.Null(client);
    }

    [Fact]
    public void GetByOrigin_Found_ReturnsClient()
    {
        VerifySharedChecks3(input0: _conn, value0: "client-002", value1: 20);

        var found = GetClient("client-002");
        Assert.NotNull(found);
        Assert.Equal("client-002", found.OriginId);
        Assert.Equal(20, found.LastSyncVersion);
    }

    [Fact]
    public void Upsert_UpdatesExisting()
    {
        VerifySharedChecks3(input0: _conn, value0: "client-003", value1: 10);

        VerifySharedChecks3(input0: _conn, value0: "client-003", value1: 50);

        var found = (
            Ok(read: () => PostgresSyncClientRepository.GetByOrigin(_conn, "client-003"))
        ).Value!;
        Assert.Equal(50, found.LastSyncVersion);
    }

    [Fact]
    public void Delete_RemovesClient()
    {
        VerifySharedChecks3(input0: _conn, value0: "client-004", value1: 10);

        var deleteResult = PostgresSyncClientRepository.Delete(_conn, "client-004");
        global::Tests.Shared.ResultAssertions.ExactOk(actual: deleteResult);

        var found = (
            Ok(read: () => PostgresSyncClientRepository.GetByOrigin(_conn, "client-004"))
        ).Value;
        Assert.Null(found);
    }

    [Fact]
    public void Delete_NonExistent_Succeeds()
    {
        var result = PostgresSyncClientRepository.Delete(_conn, "nonexistent");
        global::Tests.Shared.ResultAssertions.ExactOk(actual: result);
    }

    [Fact]
    public void GetMinVersion_WithClients_ReturnsMin()
    {
        VerifySharedChecks5(input0: _conn, value5: 50, value9: 75);

        var min = (Ok(read: () => PostgresSyncClientRepository.GetMinVersion(_conn))).Value;
        Assert.Equal(50, min);
    }

    [Fact]
    public void GetAll_OrderedByVersion()
    {
        VerifySharedChecks5(input0: _conn, value5: 25, value9: 50);

        var list = (Ok(read: () => PostgresSyncClientRepository.GetAll(_conn))).Value;

        // Should be ordered by last_sync_version ASC
        Assert.Equal("c2", list[0].OriginId); // 25
        Assert.Equal("c3", list[1].OriginId); // 50
        Assert.Equal("c1", list[2].OriginId); // 100
    }

    #endregion

    #region PostgresSyncSession Tests

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(EnableSuppression_Succeeds_ContextCasesData))]
    public void EnableSuppression_Succeeds_ContextCases(
        string caseName,
        global::System.Func<
            global::Npgsql.NpgsqlConnection,
            global::Outcome.Result<global::System.Boolean, global::Nimblesite.Sync.Core.SyncError>
        > value0
    )
    {
        NamedCase(caseName: caseName);
        ExerciseOperationCaseMatrix2(input0: _conn, value0: value0);
    }

    [Fact]
    public void Suppression_Toggle_Works()
    {
        // Enable
        var enable = PostgresSyncSession.EnableSuppression(_conn);
        global::Tests.Shared.ResultAssertions.ExactOk(actual: enable);

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
        global::Tests.Shared.ResultAssertions.ExactOk(actual: disable);

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
        VerifyDatabaseWorkflow1(
            configure0: command =>
                command.CommandText = "SELECT value FROM _sync_state WHERE key = 'origin_id'",
            input0: setResult,
            input1: _conn,
            value0: "my-origin-123"
        );
    }

    [Fact]
    public void CreateSchema_CreatesAllTables()
    {
        // Schema already created in InitializeAsync, verify tables exist
        var tables = new[] { "_sync_log", "_sync_state", "_sync_session", "_sync_clients" };

        foreach (var table in tables)
        {
            var exists = (bool)
                global::Tests.Shared.DatabaseCommands.Scalar(
                    connection: _conn,
                    configure: command =>
                        command.CommandText =
                            "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = @table)",
                    parameters: () => [("@table", table)]
                )!;

            Assert.True(exists, $"Table {table} should exist");
        }
    }

    [Fact]
    public void GetOriginId_AfterSet_ReturnsValue()
    {
        PostgresSyncSchema.SetOriginId(_conn, "test-origin-id");

        var result = PostgresSyncSchema.GetOriginId(_conn);

        global::Tests.Shared.ResultAssertions.ExactOk(actual: result);
        Assert.Equal("test-origin-id", (Ok(actual: result)).Value);
    }

    [Fact]
    public void GetOriginId_NotSet_ReturnsEmpty()
    {
        // Origin is empty by default from schema initialization
        var result = PostgresSyncSchema.GetOriginId(_conn);

        global::Tests.Shared.ResultAssertions.ExactOk(actual: result);
        Assert.Equal("", (Ok(actual: result)).Value);
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

    #endregion

    #region PostgresChangeApplier Tests

    [Fact]
    public void ApplyChange_Insert_CreatesRow()
    {
        // Create test table
        var result = ApplyInsert(
            createTableSql: Data_ApplyChange_Insert_CreatesRow_1,
            table: "test_person",
            pk: "{\"id\":\"p1\"}",
            payload: "{\"id\":\"p1\",\"name\":\"Alice\"}"
        );
        global::Tests.Shared.ResultAssertions.ExactOk(actual: result);

        // Verify row exists
        var name = global::Tests
            .Shared.DatabaseCommands.Scalar(
                connection: _conn,
                configure: command =>
                    command.CommandText = "SELECT name FROM test_person WHERE id = 'p1'"
            )
            ?.ToString();

        Assert.Equal("Alice", name);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(IndependentCommentScenarioData2))]
    public void ApplyChange_Update_UpdatesRow_CommentScenarioCases(
        string scenarioName,
        System.String scenario0,
        System.String scenario1,
        System.String scenario2,
        System.String scenario3,
        System.String scenario4,
        System.String scenario5
    )
    {
        NamedCase(caseName: scenarioName); // Create and populate test table
        DatabaseCommands.Execute(
            connection: _conn,
            configure: command => command.CommandText = scenario0
        );

        var result = VerifySharedChecks1(
            input0: _conn,
            input1: Logger,
            value1: scenario1,
            value2: scenario2,
            value3: SyncOperation.Update,
            value4: scenario3
        );
        VerifyDatabaseWorkflow1(
            configure0: command => command.CommandText = scenario4,
            input0: result,
            input1: _conn,
            value0: scenario5
        );
    }

    [Fact]
    public void ApplyChange_Delete_RemovesRow()
    {
        // Create and populate test table
        using var createCmd = _conn.CreateCommand();
        createCmd.CommandText = Data_ApplyChange_Delete_RemovesRow_1;
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
        global::Tests.Shared.ResultAssertions.ExactOk(actual: result);

        var count = global::Tests.Shared.DatabaseCommands.ScalarInt32(
            connection: _conn,
            configure: command =>
                command.CommandText = "SELECT COUNT(*) FROM test_delete WHERE id = 'd1'"
        );

        Assert.Equal(0, count);
    }

    [Fact]
    public void ApplyChange_Insert_Upserts()
    {
        var result = ApplyInsert(
            createTableSql: Data_ApplyChange_Insert_Upserts_1,
            table: "test_upsert",
            pk: "{\"id\":\"x1\"}",
            payload: "{\"id\":\"x1\",\"name\":\"Upserted\"}"
        );
        VerifyDatabaseWorkflow1(
            configure0: command =>
                command.CommandText = "SELECT name FROM test_upsert WHERE id = 'x1'",
            input0: result,
            input1: _conn,
            value0: "Upserted"
        );
    }

    [Fact]
    public void ApplyChange_ForeignKeyViolation_ReturnsFalse()
    {
        var result = ApplyInsert(
            createTableSql: Data_ApplyChange_ForeignKeyViolation_ReturnsFalse_1,
            table: "test_child",
            pk: "{\"id\":\"c1\"}",
            payload: "{\"id\":\"c1\",\"parent_id\":\"nonexistent\"}"
        );

        var success = (global::Tests.Shared.ResultAssertions.ExactOk(actual: result)).Value;
        Assert.False(success); // FK violation returns false for deferral
    }

    #endregion

    #region PostgresTriggerGenerator Tests

    [Fact]
    public void CreateTriggers_CreatesAllTriggers()
    {
        // Create test table
        using var createCmd = _conn.CreateCommand();
        createCmd.CommandText = Data_CreateTriggers_CreatesAllTriggers_1;
        createCmd.ExecuteNonQuery();

        var result = PostgresTriggerGenerator.CreateTriggers(_conn, "test_trigger_table", Logger);
        global::Tests.Shared.ResultAssertions.ExactOk(actual: result);

        // Verify triggers exist
        var triggerTypes = new[] { "insert", "update", "delete" };
        foreach (var triggerType in triggerTypes)
        {
            var exists = (bool)
                global::Tests.Shared.DatabaseCommands.Scalar(
                    connection: _conn,
                    configure: command =>
                        command.CommandText =
                            "SELECT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = @name)",
                    parameters: () => [("@name", $"test_trigger_table_sync_{triggerType}")]
                )!;

            Assert.True(exists, $"Trigger for {triggerType} should exist");
        }
    }

    [Fact]
    public void Triggers_LogInsert()
    {
        // Create and setup test table with triggers
        CreateTable(sql: Data_Triggers_LogInsert_1);

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
        // Check sync log
        var list = (Ok(read: () => PostgresSyncLogRepository.FetchChanges(_conn, 0, 100))).Value;
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
        var updateEntry = VerifyTriggerLog(
            table: "test_log_update",
            configure0: command => command.CommandText = Data_Triggers_LogUpdate_1,
            configure1: command =>
                command.CommandText = "UPDATE test_log_update SET name = 'After' WHERE id = 'tu1'",
            operation: SyncOperation.Update
        );
        Assert.Contains("After", updateEntry.Payload!);
    }

    [Fact]
    public void Triggers_LogDelete()
    {
        var deleteEntry = VerifyTriggerLog(
            table: "test_log_delete",
            configure0: command => command.CommandText = Data_Triggers_LogDelete_1,
            configure1: command =>
                command.CommandText = "DELETE FROM test_log_delete WHERE id = 'td1'",
            operation: SyncOperation.Delete
        );
        Assert.Contains("td1", deleteEntry.PkValue);
        Assert.Null(deleteEntry.Payload); // Tombstone has no payload
    }

    [Fact]
    public void Triggers_SuppressedWhenActive()
    {
        CreateTable(sql: Data_Triggers_SuppressedWhenActive_1);

        PostgresTriggerGenerator.CreateTriggers(_conn, "test_suppression", Logger);
        PostgresSyncSchema.SetOriginId(_conn, "trigger-test-origin");

        // Enable suppression
        PostgresSyncSession.EnableSuppression(_conn);

        var maxBefore = (Ok(actual: PostgresSyncLogRepository.GetMaxVersion(_conn))).Value;

        // Insert while suppressed
        var list = VerifySharedContinuation1(
            configure0: command =>
                command.CommandText =
                    "INSERT INTO test_suppression (id, name) VALUES ('sup1', 'Suppressed')",
            input0: _conn,
            input1: maxBefore
        );
        Assert.Empty(list);

        // Disable and insert
        PostgresSyncSession.DisableSuppression(_conn);

        var list2 = VerifySharedContinuation1(
            configure0: command =>
                command.CommandText =
                    "INSERT INTO test_suppression (id, name) VALUES ('unsup1', 'NotSuppressed')",
            input0: _conn,
            input1: maxBefore
        );
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
        var entry = Fixture_Repositories_WithoutSyncSchema_ReturnDatabaseErrors_1();

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

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void CreateTable(string sql) =>
        DatabaseCommands.Execute(
            connection: _conn,
            configure: command => command.CommandText = sql
        );

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static SyncClient Client(string origin, int version) =>
        new(origin, version, DateTime.UtcNow.ToString("O"), DateTime.UtcNow.ToString("O"));

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private List<SyncClient> GetAllClients() =>
        [.. (Assert.IsType<SyncClientListOk>(PostgresSyncClientRepository.GetAll(_conn))).Value];

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private SyncClient? GetClient(string origin) =>
        (Ok(read: () => PostgresSyncClientRepository.GetByOrigin(_conn, origin))).Value;

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private List<SyncLogEntry> FetchLog(long fromVersion) =>
        [
            .. (
                Ok(read: () => PostgresSyncLogRepository.FetchChanges(_conn, fromVersion, 100))
            ).Value,
        ];

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private Outcome.Result<bool, SyncError> ApplyInsert(
        string createTableSql,
        string table,
        string pk,
        string payload
    ) =>
        VerifyReusableWorkflow1(
            configure0: command => command.CommandText = createTableSql,
            input0: _conn,
            input1: VerifySharedChecks1,
            input2: Logger,
            value0: table,
            value1: pk,
            value3: payload
        );

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private SyncLogEntry VerifyTriggerLog(
        string table,
        global::System.Action<global::System.Data.Common.DbCommand> configure0,
        global::System.Action<global::System.Data.Common.DbCommand> configure1,
        SyncOperation operation
    ) =>
        VerifyReusableWorkflow2(
            configure0: configure0,
            configure1: configure1,
            input0: _conn,
            input1: Logger,
            input2: VerifySharedContinuation1,
            value0: table,
            value2: table,
            value3: operation
        );

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void VerifyDatabaseWorkflow1(
        Outcome.Result<bool, SyncError> input0,
        NpgsqlConnection input1,
        string value0,
        global::System.Action<global::System.Data.Common.DbCommand> configure0
    )
    {
        global::Tests.Shared.ResultAssertions.ExactOk(actual: input0);

        var origin = global::Tests
            .Shared.DatabaseCommands.Scalar(connection: input1, configure: configure0)
            ?.ToString();

        Assert.Equal(value0, origin);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static Outcome.Result<bool, SyncError> VerifySharedChecks1(
        NpgsqlConnection input0,
        ILogger input1,
        string value1,
        string value2,
        global::Nimblesite.Sync.Core.SyncOperation value3,
        string value4
    )
    {
        var entry = new SyncLogEntry(
            1,
            value1,
            value2,
            value3,
            value4,
            "test-origin",
            DateTime.UtcNow.ToString("O")
        );

        var result = PostgresChangeApplier.ApplyChange(input0, entry, input1);

        return result;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void VerifySharedChecks3(NpgsqlConnection input0, string value0, int value1)
    {
        var client = new SyncClient(
            value0,
            value1,
            DateTime.UtcNow.ToString("O"),
            DateTime.UtcNow.ToString("O")
        );
        PostgresSyncClientRepository.Upsert(input0, client);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void VerifySharedChecks4(int input0, NpgsqlConnection input1)
    {
        var entry = new SyncLogEntry(
            0,
            "TestTable",
            $"{{\"Id\":\"t{input0}\"}}",
            SyncOperation.Insert,
            $"{{\"Id\":\"t{input0}\",\"Name\":\"Test {input0}\"}}",
            "test-origin",
            DateTime.UtcNow.ToString("O")
        );
        PostgresSyncLogRepository.Insert(input1, entry);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void VerifySharedChecks5(NpgsqlConnection input0, int value5, int value9)
    {
        var clients = new[]
        {
            Client(origin: "c1", version: 100),
            Client(origin: "c2", version: value5),
            Client(origin: "c3", version: value9),
        };

        foreach (var client in clients)
        {
            PostgresSyncClientRepository.Upsert(input0, client);
        }
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static IReadOnlyList<SyncLogEntry> VerifySharedContinuation1(
        NpgsqlConnection input0,
        long input1,
        global::System.Action<global::System.Data.Common.DbCommand> configure0
    )
    {
        // Update
        DatabaseCommands.Execute(connection: input0, configure: configure0);

        // Check sync log
        // Check sync log
        var list = (
            Ok(read: () => PostgresSyncLogRepository.FetchChanges(input0, input1, 100))
        ).Value;

        return list;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static Outcome.Result<bool, SyncError> VerifyReusableWorkflow1(
        NpgsqlConnection input0,
        Func<
            NpgsqlConnection,
            ILogger,
            string,
            string,
            SyncOperation,
            string,
            Outcome.Result<bool, SyncError>
        > input1,
        ILogger input2,
        string value0,
        string value1,
        string value3,
        global::System.Action<global::System.Data.Common.DbCommand> configure0
    )
    { // Create test table
        DatabaseCommands.Execute(connection: input0, configure: configure0);

        var result = input1(
            arg1: input0,
            arg2: input2,
            arg3: value0,
            arg4: value1,
            arg5: SyncOperation.Insert,
            arg6: value3
        );

        return result;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static SyncLogEntry VerifyReusableWorkflow2(
        NpgsqlConnection input0,
        ILogger input1,
        Func<
            NpgsqlConnection,
            long,
            Action<System.Data.Common.DbCommand>,
            IReadOnlyList<SyncLogEntry>
        > input2,
        string value0,
        string value2,
        global::Nimblesite.Sync.Core.SyncOperation value3,
        global::System.Action<global::System.Data.Common.DbCommand> configure0,
        global::System.Action<global::System.Data.Common.DbCommand> configure1
    )
    {
        DatabaseCommands.Execute(connection: input0, configure: configure0);

        PostgresTriggerGenerator.CreateTriggers(input0, value0, input1);
        PostgresSyncSession.DisableSuppression(input0);
        PostgresSyncSchema.SetOriginId(input0, "trigger-test-origin");

        // Get current max version
        var maxBefore = (Ok(actual: PostgresSyncLogRepository.GetMaxVersion(input0))).Value;

        // Update
        var list = input2(arg3: configure1, arg1: input0, arg2: maxBefore);
        var updateEntry = list.FirstOrDefault(e => e.TableName == value2 && e.Operation == value3);

        Assert.NotNull(updateEntry);

        return updateEntry;
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseOperationCaseMatrix1(
        NpgsqlConnection input0,
        global::System.Func<
            global::Npgsql.NpgsqlConnection,
            global::Outcome.Result<global::System.Int64, global::Nimblesite.Sync.Core.SyncError>
        > value0
    )
    {
        var result = value0(arg: input0);

        var max = (global::Tests.Shared.ResultAssertions.ExactOk(actual: result)).Value;
        Assert.Equal(0, max);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseOperationCaseMatrix2(
        NpgsqlConnection input0,
        global::System.Func<
            global::Npgsql.NpgsqlConnection,
            global::Outcome.Result<global::System.Boolean, global::Nimblesite.Sync.Core.SyncError>
        > value0
    )
    {
        var result = value0(arg: input0);
        global::Tests.Shared.ResultAssertions.ExactOk(actual: result);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifySyncLoopWorkflow1(
        Action<int, NpgsqlConnection> input0,
        NpgsqlConnection input1,
        int value1,
        int value3,
        int value4
    )
    { // Insert 3 entries
        for (var i = 1; i <= value1; i++)
        {
            input0(i, input1);
        }

        // Fetch from version 0 - should get all 3
        // Fetch from version 0 - should get all 3
        var allList = (
            Ok(read: () => PostgresSyncLogRepository.FetchChanges(input1, 0, value3))
        ).Value;
        Assert.Equal(value4, allList.Count);
    }
}
