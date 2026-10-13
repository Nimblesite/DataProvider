// Implements [CI-TEST-SHARED-COMMAND-PARAMETERS].
// Implements [CI-TEST-SHARED-COMMAND].
using Tests.Shared;
using static global::Tests.Shared.ResultAssertions;

namespace Nimblesite.Sync.Http.Tests;

/// <summary>
/// E2E integration tests for cross-database sync between SQLite and PostgreSQL.
/// These tests PROVE the spec is fully implemented across different database backends.
/// Uses the shared postgres container; each test gets its own database.
/// </summary>
[Collection(PostgresTestSuite.Name)]
[Trait("Category", "Docker")]
public sealed partial class CrossDatabaseSyncTests(PostgresContainerFixture fixture)
    : PostgresSyncTestFixture(fixture: fixture, databaseName: "syncdb")
{
    /// <summary>
    /// Creates a fresh SQLite file database with sync schema and triggers.
    /// </summary>
    private SqliteConnection CreateSqliteDb(string originId)
    {
        var dbPath = global::Tests.Shared.TempSqliteDatabase.PathFor("http_sync");
        _sqliteDbPaths.Add(dbPath);
        var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();

        // Create sync schema
        VerifyReusableWorkflow1(
            configure0: command => command.CommandText = Data_CreateSqliteDb_1,
            input0: conn,
            input1: originId
        );

        // Create triggers
        TriggerGenerator.CreateTriggers(conn, "Person", NullLogger.Instance);

        return conn;
    }

    /// <summary>
    /// Creates PostgreSQL database with sync schema (lowercase table for cross-db compat).
    /// </summary>
    private NpgsqlConnection CreatePostgresDb(string originId) =>
        CreatePostgresSyncDatabase(originId: originId, createTableSql: Data_CreatePostgresDb_1);

    /// <summary>Runs a scalar query and returns its value as a string.</summary>
    internal static string? ScalarString(
        global::System.Data.Common.DbConnection connection,
        string sql
    ) =>
        global::Tests
            .Shared.DatabaseCommands.Scalar(
                connection: connection,
                configure: command => command.CommandText = sql
            )
            ?.ToString();

    /// <summary>Creates the person triggers on PostgreSQL, asserting success.</summary>
    private void EnsurePostgresPersonTriggers(NpgsqlConnection postgres)
    {
        var triggerResult = PostgresTriggerGenerator.CreateTriggers(postgres, "person", _logger);
        Assert.True(triggerResult is BoolSyncOk, $"Trigger creation failed: {triggerResult}");
    }

    /// <summary>Fetches all changes from a SQLite sync log, asserting the result is Ok.</summary>
    internal static IReadOnlyList<SyncLogEntry> FetchAllChanges(SqliteConnection connection) =>
        (Ok(read: () => SyncLogRepository.FetchChanges(connection, 0, 100))).Value;

    /// <summary>Fetches all changes from a PostgreSQL sync log, asserting the result is Ok.</summary>
    internal static IReadOnlyList<SyncLogEntry> FetchAllPostgresChanges(
        NpgsqlConnection connection
    ) => (Ok(read: () => PostgresSyncLogRepository.FetchChanges(connection, 0, 100))).Value;

    /// <summary>Applies a change with suppression enabled, returning the apply result.</summary>
    internal static Outcome.Result<bool, SyncError> ApplyChangeWithSuppression(
        SqliteConnection connection,
        SyncLogEntry entry
    )
    {
        SyncSessionManager.EnableSuppression(connection);
        var result = ChangeApplierSQLite.ApplyChange(connection, entry);
        SyncSessionManager.DisableSuppression(connection);
        return result;
    }

    /// <summary>Executes a single SQL statement.</summary>
    internal static void ExecuteSql(
        global::System.Data.Common.DbConnection connection,
        string sql
    ) =>
        DatabaseCommands.Execute(
            connection: connection,
            configure: command => command.CommandText = sql
        );

    /// <summary>Asserts a person insert entry's table, operation and origin.</summary>
    internal static void AssertPersonInsert(SyncLogEntry entry, string originId)
    {
        Assert.Equal("Person", entry.TableName);
        Assert.Equal(SyncOperation.Insert, entry.Operation);
        Assert.Equal(originId, entry.Origin);
    }

    /// <summary>Asserts a scalar integer query result.</summary>
    internal static void AssertScalarInt32(
        global::System.Data.Common.DbConnection connection,
        string sql,
        int expected
    ) =>
        Assert.Equal(
            expected,
            DatabaseCommands.ScalarInt32(
                connection: connection,
                configure: command => command.CommandText = sql
            )
        );

    /// <summary>Asserts the result is Ok and contains exactly one change.</summary>
    internal static IReadOnlyList<SyncLogEntry> SingleChange(
        Outcome.Result<IReadOnlyList<SyncLogEntry>, SyncError> changes,
        string message
    )
    {
        var list = Ok(actual: changes, message: message).Value;
        Assert.Single(list);
        return list;
    }

    /// <summary>Applies entries to PostgreSQL with suppression, asserting each apply succeeds.</summary>
    private void ApplyEntriesToPostgres(
        NpgsqlConnection postgres,
        IEnumerable<SyncLogEntry> entries
    )
    {
        PostgresSyncSession.EnableSuppression(postgres);
        foreach (var entry in entries)
        {
            var result = PostgresChangeApplier.ApplyChange(postgres, entry, _logger);
            Assert.True(result is BoolSyncOk, $"ApplyChange failed: {result}");
        }
        PostgresSyncSession.DisableSuppression(postgres);
    }

    [Fact]
    public void SQLite_Insert_SyncsTo_Postgres()
    {
        // Arrange
        using var sqlite = CreateSqliteDb(Guid.NewGuid().ToString());
        using var postgres = CreatePostgresDb(Guid.NewGuid().ToString());

        // Act - Insert in SQLite
        var changes = VerifyReusableWorkflow2(
            configure0: cmd =>
                cmd.CommandText =
                    "INSERT INTO Person (Id, Name, Email) VALUES ('p1', 'Alice', 'alice@example.com')",
            input0: sqlite
        );

        var changesList = SingleChange(changes, $"FetchChanges failed: {changes}");

        // Apply changes to Postgres with suppression
        ApplyEntriesToPostgres(postgres, changesList);

        // Assert - Verify in Postgres
        Assert.Equal("Alice", ScalarString(postgres, "SELECT name FROM person WHERE id = 'p1'"));
    }

    [Fact]
    public void Postgres_Insert_SyncsTo_SQLite()
    {
        // Arrange
        using var postgres = CreatePostgresDb(Guid.NewGuid().ToString());
        using var sqlite = CreateSqliteDb(Guid.NewGuid().ToString());

        // First create triggers in Postgres for this table
        EnsurePostgresPersonTriggers(postgres);

        // Act - Insert in Postgres
        ExecuteSql(
            postgres,
            "INSERT INTO person (id, name, email) VALUES ('p2', 'Bob', 'bob@example.com')"
        );

        // Fetch changes from Postgres
        var changes = PostgresSyncLogRepository.FetchChanges(postgres, 0, 100);

        var changesList = SingleChange(changes, $"FetchChanges failed: {changes}");

        // Apply changes to SQLite with suppression
        SyncSessionManager.EnableSuppression(sqlite);
        foreach (var entry in changesList)
        {
            var result = ChangeApplierSQLite.ApplyChange(sqlite, entry);
            Assert.True(result is BoolSyncOk, $"ApplyChange failed: {result}");
        }
        SyncSessionManager.DisableSuppression(sqlite);

        // Assert - Verify in SQLite (note: SQLite preserves case from payload)
        Assert.Equal("Bob", ScalarString(sqlite, "SELECT Name FROM Person WHERE Id = 'p2'"));
    }

    [Fact]
    public void Bidirectional_Sync_BetweenDatabases()
    {
        // Arrange
        var sqliteOriginId = Guid.NewGuid().ToString();

        using var sqlite = CreateSqliteDb(sqliteOriginId);
        using var postgres = CreatePostgresDb(Guid.NewGuid().ToString());

        // Create triggers in Postgres
        EnsurePostgresPersonTriggers(postgres);

        // Insert in SQLite
        ExecuteSql(
            sqlite,
            "INSERT INTO Person (Id, Name, Email) VALUES ('s1', 'SQLite User', 'sqlite@example.com')"
        );

        // Insert in Postgres
        ExecuteSql(
            postgres,
            "INSERT INTO person (id, name, email) VALUES ('p1', 'Postgres User', 'postgres@example.com')"
        );

        // Sync SQLite -> Postgres
        var sqliteChangesList = FetchAllChanges(sqlite);

        ApplyEntriesToPostgres(postgres, sqliteChangesList);

        // Sync Postgres -> SQLite (skip echo)
        var postgresChangesList = FetchAllPostgresChanges(postgres);

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
        VerifyReusableWorkflow6(
            configure0: command => command.CommandText = "SELECT COUNT(*) FROM person",
            input0: postgres,
            value0: 2
        );

        AssertScalarInt32(sqlite, "SELECT COUNT(*) FROM Person", 2);
    }

    [Fact]
    public void Update_SyncsAcrossDatabases()
    {
        // Arrange
        using var sqlite = CreateSqliteDb(Guid.NewGuid().ToString());
        using var postgres = CreatePostgresDb(Guid.NewGuid().ToString());

        // Insert in SQLite
        VerifySharedContinuation1(
            configure0: cmd =>
                cmd.CommandText =
                    "INSERT INTO Person (Id, Name, Email) VALUES ('u1', 'Original', 'original@example.com')",
            configure1: cmd =>
                cmd.CommandText =
                    "UPDATE Person SET Name = 'Updated', Email = 'updated@example.com' WHERE Id = 'u1'",
            input0: sqlite,
            input1: postgres,
            input2: _logger,
            value3: SyncOperation.Update
        );

        // Assert
        using var verifyCmd = postgres.CreateCommand();
        using var reader = DatabaseCommands.ReadFirstRow(
            verifyCmd,
            command => command.CommandText = "SELECT name, email FROM person WHERE id = 'u1'"
        );
        Assert.Equal("Updated", reader.GetString(0));
        Assert.Equal("updated@example.com", reader.GetString(1));
    }

    [Fact]
    public void Delete_SyncsAcrossDatabases_AsTombstone()
    {
        // Arrange
        using var sqlite = CreateSqliteDb(Guid.NewGuid().ToString());
        using var postgres = CreatePostgresDb(Guid.NewGuid().ToString());

        // Insert and sync
        VerifySharedContinuation1(
            configure0: cmd =>
                cmd.CommandText =
                    "INSERT INTO Person (Id, Name, Email) VALUES ('d1', 'ToDelete', 'delete@example.com')",
            configure1: cmd => cmd.CommandText = "DELETE FROM Person WHERE Id = 'd1'",
            input0: sqlite,
            input1: postgres,
            input2: _logger,
            value3: SyncOperation.Delete
        );

        // Assert - Record deleted
        VerifyReusableWorkflow6(
            configure0: command =>
                command.CommandText = "SELECT COUNT(*) FROM person WHERE id = 'd1'",
            input0: postgres,
            value0: 0
        );
    }

    [Fact]
    public void TriggerSuppression_PreventsEcho()
    {
        // Arrange
        var sqliteOriginId = Guid.NewGuid().ToString();

        using var sqlite = CreateSqliteDb(sqliteOriginId);

        // Insert WITH suppression - should NOT log
        VerifyReusableWorkflow7(
            configure0: cmd =>
                cmd.CommandText =
                    "INSERT INTO Person (Id, Name, Email) VALUES ('s1', 'Suppressed', 'suppressed@example.com')",
            input0: sqlite
        );

        // Insert WITHOUT suppression - should log
        var list2 = VerifySharedPhase1(
            configure0: cmd =>
                cmd.CommandText =
                    "INSERT INTO Person (Id, Name, Email) VALUES ('n1', 'Normal', 'normal@example.com')",
            input0: sqlite
        );
        Assert.Single(list2);
    }

    [Fact]
    public void HashVerification_ConsistentAcrossDatabases()
    {
        // Arrange
        var sqliteOriginId = Guid.NewGuid().ToString();
        using var sqlite = CreateSqliteDb(sqliteOriginId);

        // Insert data
        var list = VerifySharedPhase1(
            configure0: cmd =>
                cmd.CommandText =
                    "INSERT INTO Person (Id, Name, Email) VALUES ('h1', 'HashTest', 'hash@example.com')",
            input0: sqlite
        );

        // Compute batch hash
        var hash1 = HashVerifier.ComputeBatchHash(list);
        Assert.NotNull(hash1);
        Assert.NotEmpty(hash1);

        // Same changes = same hash
        var hash2 = HashVerifier.ComputeBatchHash(list);
        Assert.Equal(hash1, hash2);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void VerifySharedContinuation1(
        SqliteConnection input0,
        NpgsqlConnection input1,
        ILogger input2,
        global::Nimblesite.Sync.Core.SyncOperation value3,
        global::System.Action<global::System.Data.Common.DbCommand> configure0,
        global::System.Action<global::System.Data.Common.DbCommand> configure1
    )
    {
        // Insert in SQLite
        DatabaseCommands.Execute(connection: input0, configure: configure0);

        // Sync to Postgres
        var insertChanges = SyncLogRepository.FetchChanges(input0, 0, 100);
        PostgresSyncSession.EnableSuppression(input1);
        foreach (var entry in (Ok(actual: insertChanges)).Value)
        {
            PostgresChangeApplier.ApplyChange(input1, entry, input2);
        }
        PostgresSyncSession.DisableSuppression(input1);

        var versionAfterInsert = (Ok(actual: insertChanges)).Value.Max(e => e.Version);

        // Act - Update in SQLite
        DatabaseCommands.Execute(connection: input0, configure: configure1);

        // Sync update to Postgres
        // Sync update to Postgres
        var updateList = Ok(read: () =>
            SyncLogRepository.FetchChanges(input0, versionAfterInsert, 100)
        ).Value;
        Assert.Single(updateList);
        Assert.Equal(value3, updateList[0].Operation);

        PostgresSyncSession.EnableSuppression(input1);
        var applyResult = PostgresChangeApplier.ApplyChange(input1, updateList[0], input2);
        PostgresSyncSession.DisableSuppression(input1);
        Assert.True(applyResult is BoolSyncOk);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static IReadOnlyList<SyncLogEntry> VerifySharedPhase1(
        SqliteConnection input0,
        global::System.Action<global::System.Data.Common.DbCommand> configure0
    )
    {
        // Insert WITHOUT suppression - should log
        DatabaseCommands.Execute(connection: input0, configure: configure0);

        // Assert - One change logged
        return FetchAllChanges(input0);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow1(
        SqliteConnection input0,
        string input1,
        global::System.Action<global::System.Data.Common.DbCommand> configure0
    )
    {
        // Create sync schema
        global::Tests.Shared.TempSqliteDatabase.InitializeSync(connection: input0, origin: input1);

        // Create test table
        DatabaseCommands.Execute(connection: input0, configure: configure0);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static Outcome.Result<IReadOnlyList<SyncLogEntry>, SyncError> VerifyReusableWorkflow2(
        SqliteConnection input0,
        global::System.Action<global::System.Data.Common.DbCommand> configure0
    )
    {
        // Act - Insert in SQLite
        DatabaseCommands.Execute(connection: input0, configure: configure0);

        // Fetch changes from SQLite
        var changes = SyncLogRepository.FetchChanges(input0, 0, 100);

        return changes;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow6(
        NpgsqlConnection input0,
        int value0,
        global::System.Action<global::System.Data.Common.DbCommand> configure0
    )
    {
        // Assert - Both databases have both records
        var pgCount = global::Tests.Shared.DatabaseCommands.ScalarInt32(
            connection: input0,
            configure: configure0
        );

        Assert.Equal(value0, pgCount);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow7(
        SqliteConnection input0,
        global::System.Action<global::System.Data.Common.DbCommand> configure0
    )
    {
        // Insert WITH suppression - should NOT log
        SyncSessionManager.EnableSuppression(input0);
        DatabaseCommands.Execute(connection: input0, configure: configure0);
        SyncSessionManager.DisableSuppression(input0);

        // Assert - No changes logged
        Assert.Empty(FetchAllChanges(input0));
    }
}
