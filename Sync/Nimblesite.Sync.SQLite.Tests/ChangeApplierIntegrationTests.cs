// Implements [CI-TEST-SHARED-COMMAND-PARAMETERS].
// Implements [CI-TEST-SHARED-COMMAND].

using static global::Tests.Shared.ResultAssertions;

namespace Nimblesite.Sync.SQLite.Tests;

/// <summary>
/// Integration tests for ChangeApplierSQLite.
/// Tests applying sync changes (insert, update, delete) to SQLite database.
/// NO MOCKS - real file-based SQLite databases only! NO :memory:!
/// </summary>
public sealed partial class ChangeApplierIntegrationTests : IDisposable
{
    private readonly SqliteConnection _db;
    private readonly string _dbPath;
    private readonly string _originId = Guid.NewGuid().ToString();
    private const string Timestamp = "2025-01-01T00:00:00.000Z";

    /// <summary>
    /// Initializes test with file-based SQLite database.
    /// </summary>
    public ChangeApplierIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"change_applier_{Guid.NewGuid():N}.db");
        _db = global::Tests.Shared.TempSqliteDatabase.OpenSync(path: _dbPath, origin: _originId);

        // Create test table
        DatabaseCommands.Execute(
            connection: _db,
            configure: command => command.CommandText = Data_Shared_1
        );
    }

    #region Insert Operations

    [Fact]
    public void ApplyChange_Insert_CreatesNewRecord()
    {
        // Arrange
        var result = VerifySharedContinuation1(
            input0: Fixture_ApplyChange_Insert_CreatesNewRecord_1,
            input1: _db
        );
        Assert.True((Ok(actual: result)).Value);

        VerifyReusableWorkflow15(
            configure0: command =>
                command.CommandText = "SELECT Name, Age, Email FROM Person WHERE Id = 'p1'",
            input0: _db,
            value0: "Alice",
            value2: 30,
            value4: "alice@example.com"
        );
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(ApplyChange_Insert_WithNullPayload_ReturnsError_CasesData))]
    public void ApplyChange_Insert_WithNullPayload_ReturnsError_Cases(
        string caseName,
        Func<SyncLogEntry> input0
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseCaseMatrix1(input0: input0, input1: _db);
    }

    [Fact]
    public void ApplyChange_Insert_DuplicateKey_ReplacesExisting()
    {
        // Arrange - Insert first record
        var entry1 = Fixture_ApplyChange_Insert_DuplicateKey_ReplacesExisting_1();
        ChangeApplierSQLite.ApplyChange(_db, entry1);

        // Act - Insert with same PK (should replace)
        var entry2 = Fixture_ApplyChange_Insert_DuplicateKey_ReplacesExisting_2();
        global::Tests.Shared.ResultAssertions.Ok(
            actual: ChangeApplierSQLite.ApplyChange(_db, entry2)
        );

        using var cmd = _db.CreateCommand();
        using var reader = DatabaseCommands.ReadFirstRow(
            cmd,
            command => command.CommandText = "SELECT Name, Age FROM Person WHERE Id = 'p1'"
        );
        Assert.Equal("Alice Updated", reader.GetString(0));
        Assert.Equal(31, reader.GetInt64(1));
    }

    #endregion

    #region Update Operations

    [Fact]
    public void ApplyChange_Update_ModifiesExistingRecord()
    {
        // Arrange - Insert first
        InsertPerson("p1", "Alice", 30);

        VerifyReusableWorkflow9(
            input0: Fixture_ApplyChange_Update_ModifiesExistingRecord_1,
            input1: ApplyChangeAndAssertSuccess
        );

        VerifyReusableWorkflow15(
            configure0: command =>
                command.CommandText = "SELECT Name, Age, Email FROM Person WHERE Id = 'p1'",
            input0: _db,
            value0: "Alice Updated",
            value2: 31,
            value4: "alice.new@example.com"
        );
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(IndependentScenarioData11))]
    public void ApplyChange_Update_NonExistingRecord_InsertsAsUpsert_ScenarioCases(
        string scenarioName,
        global::System.Func<global::Nimblesite.Sync.Core.SyncLogEntry> scenario0,
        System.String scenario1,
        System.Int64 scenario2
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: scenarioName));

        // Arrange
        VerifyReusableWorkflow12(
            configure0: command => command.CommandText = scenario1,
            input0: scenario0,
            input1: VerifyDatabaseWorkflow1,
            input2: _db,
            input3: scenario2
        );
    }

    #endregion

    #region Delete Operations

    [Fact]
    public void ApplyChange_Delete_RemovesExistingRecord()
    {
        // Arrange
        InsertPerson("p1", "Alice", 30);

        var entry = Fixture_ApplyChange_Delete_RemovesExistingRecord_1();

        // Act
        VerifyDatabaseWorkflow1(
            configure0: command =>
                command.CommandText = "SELECT COUNT(*) FROM Person WHERE Id = 'p1'",
            input0: entry,
            input1: _db,
            value0: 0L
        );
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(IndependentScenarioData12))]
    public void ApplyChange_Delete_NonExistingRecord_StillSucceeds_ScenarioCases(
        string scenarioName,
        global::System.Func<global::Nimblesite.Sync.Core.SyncLogEntry> scenario0
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: scenarioName));

        // Arrange
        VerifyReusableWorkflow9(input0: scenario0, input1: ApplyChangeAndAssertSuccess);
    }

    #endregion

    #region JSON Type Handling


    [Fact]
    public void ApplyChange_Insert_HandlesNullValues()
    {
        // Arrange
        VerifyReusableWorkflow9(
            input0: Fixture_ApplyChange_Insert_HandlesNullValues_1,
            input1: ApplyChangeAndAssertSuccess
        );

        Assert.True(
            global::Tests.Shared.DatabaseCommands.Scalar(
                connection: _db,
                configure: command =>
                    command.CommandText = "SELECT Email FROM Person WHERE Id = 'p1'"
            ) is DBNull
        );
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(IndependentCommentScenarioData3))]
    public void ApplyChange_Insert_HandlesBooleanValues_CommentScenarioCases(
        string scenarioName,
        global::System.Func<global::Nimblesite.Sync.Core.SyncLogEntry> scenario1,
        System.Int64 scenario3
    )
    {
        Assert.False(string.IsNullOrEmpty(scenarioName)); // Arrange - Create table with boolean-like column
        DatabaseCommands.Execute(
            connection: _db,
            configure: command =>
                command.CommandText = "CREATE TABLE Settings (Id TEXT PRIMARY KEY, Enabled INTEGER)"
        );

        VerifyReusableWorkflow12(
            configure0: command =>
                command.CommandText = "SELECT Enabled FROM Settings WHERE Id = 's1'",
            input0: scenario1,
            input1: VerifyDatabaseWorkflow1,
            input2: _db,
            input3: scenario3
        );
    }

    [Fact]
    public void ApplyChange_Insert_HandlesDoubleValues()
    {
        // Arrange
        DatabaseCommands.Execute(
            connection: _db,
            configure: command =>
                command.CommandText = "CREATE TABLE Product (Id TEXT PRIMARY KEY, Price REAL)"
        );

        VerifyReusableWorkflow9(
            input0: Fixture_ApplyChange_Insert_HandlesDoubleValues_1,
            input1: ApplyChangeAndAssertSuccess
        );

        var price = global::Tests.Shared.DatabaseCommands.ScalarDouble(
            connection: _db,
            configure: command =>
                command.CommandText = "SELECT Price FROM Product WHERE Id = 'prod1'"
        );

        Assert.Equal(99.99, price, precision: 2);
    }

    #endregion

    #region Foreign Key Handling

    [Fact]
    public void ApplyChange_Insert_WithForeignKeyViolation_ReturnsFalse()
    {
        // Arrange - Create FK relationship
        var result = VerifyReusableWorkflow13(
            configure0: command =>
                command.CommandText =
                    Data_ApplyChange_Insert_WithForeignKeyViolation_ReturnsFalse_1,
            input0: _db,
            input1: VerifySharedContinuation1,
            input2: Fixture_ApplyChange_Insert_WithForeignKeyViolation_ReturnsFalse_1
        );
        Assert.False((Ok(actual: result)).Value);
    }

    [Fact]
    public void ApplyChange_Insert_WithValidForeignKey_Succeeds()
    {
        // Arrange - Create FK relationship and parent record
        var result = VerifyReusableWorkflow13(
            configure0: command =>
                command.CommandText = Data_ApplyChange_Insert_WithValidForeignKey_Succeeds_1,
            input0: _db,
            input1: VerifySharedContinuation1,
            input2: Fixture_ApplyChange_Insert_WithValidForeignKey_Succeeds_1
        );
        Assert.True((Ok(actual: result)).Value);
    }

    #endregion

    #region Batch Operations

    [Fact]
    public void ApplyChange_MultipleBatches_AllSucceed()
    {
        // Arrange
        var entries = new[]
        {
            Fixture_ApplyChange_MultipleBatches_AllSucceed_1(),
            Fixture_ApplyChange_MultipleBatches_AllSucceed_2(),
            Fixture_ApplyChange_MultipleBatches_AllSucceed_3(),
        };

        // Act
        var results = entries.Select(e => ChangeApplierSQLite.ApplyChange(_db, e)).ToList();

        // Assert
        Assert.All(
            results,
            r =>
            {
                _ = global::Tests.Shared.ResultAssertions.Expected<BoolSyncOk>(actual: r);
                Assert.True((Ok(actual: r)).Value);
            }
        );

        Assert.Equal(
            3L,
            global::Tests.Shared.DatabaseCommands.Scalar(
                connection: _db,
                configure: command => command.CommandText = "SELECT COUNT(*) FROM Person"
            )
        );
    }

    #endregion

    #region Helpers

    private void InsertPerson(string id, string name, int age)
    {
        DatabaseCommands.Execute(
            connection: _db,
            configure: cmd =>
                cmd.CommandText = "INSERT INTO Person (Id, Name, Age) VALUES (@id, @name, @age)",
            parameters: () => [("@id", id), ("@name", name), ("@age", age)]
        );
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _db.Close();
        global::Nimblesite.Sync.SQLite.Tests.EndToEndSyncTests.VerifyReusableWorkflow5(
            input0: _db,
            input1: _dbPath
        );
    }

    #endregion

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void ApplyChangeAndAssertSuccess(global::Nimblesite.Sync.Core.SyncLogEntry input0)
    {
        // Act
        global::Tests.Shared.ResultAssertions.Ok(
            actual: ChangeApplierSQLite.ApplyChange(_db, input0)
        );
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseCaseMatrix1(Func<SyncLogEntry> input0, SqliteConnection input1)
    { // Arrange
        var entry = input0();

        // Act
        global::Tests.Shared.ResultAssertions.Error(
            actual: ChangeApplierSQLite.ApplyChange(input1, entry)
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void VerifyDatabaseWorkflow1(
        SyncLogEntry input0,
        SqliteConnection input1,
        long value0,
        global::System.Action<global::System.Data.Common.DbCommand> configure0
    )
    {
        // Act
        ApplyChangeAndAssertSuccess(input0: input0);

        Assert.Equal(
            value0,
            global::Tests.Shared.DatabaseCommands.Scalar(connection: input1, configure: configure0)
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static Outcome.Result<bool, SyncError> VerifySharedContinuation1(
        Func<SyncLogEntry> input0,
        SqliteConnection input1
    )
    { // Arrange
        var entry = input0();

        // Act
        var result = ChangeApplierSQLite.ApplyChange(input1, entry);

        // Assert
        _ = global::Tests.Shared.ResultAssertions.Expected<BoolSyncOk>(actual: result);

        return result;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void VerifyReusableWorkflow9(
        Func<SyncLogEntry> input0,
        Action<SyncLogEntry> input1
    )
    {
        var entry = input0();
        // Act
        input1(obj: entry);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void VerifyReusableWorkflow12(
        Func<SyncLogEntry> input0,
        Action<SyncLogEntry, SqliteConnection, long, Action<System.Data.Common.DbCommand>> input1,
        SqliteConnection input2,
        long input3,
        global::System.Action<global::System.Data.Common.DbCommand> configure0
    )
    {
        // Arrange
        var entry = input0();
        // Act
        input1(arg4: configure0, arg1: entry, arg2: input2, arg3: input3);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static Outcome.Result<bool, SyncError> VerifyReusableWorkflow13(
        SqliteConnection input0,
        Func<Func<SyncLogEntry>, SqliteConnection, Outcome.Result<bool, SyncError>> input1,
        Func<SyncLogEntry> input2,
        global::System.Action<global::System.Data.Common.DbCommand> configure0
    )
    { // Arrange - Create FK relationship
        DatabaseCommands.Execute(connection: input0, configure: configure0);

        var result = input1(arg1: input2, arg2: input0);

        return result;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow15(
        SqliteConnection input0,
        string value0,
        int value2,
        string value4,
        global::System.Action<global::System.Data.Common.DbCommand> configure0
    )
    {
        using var cmd = input0.CreateCommand();
        using var reader = DatabaseCommands.ReadFirstRow(cmd, configure0);
        Assert.Equal(value0, reader.GetString(0));
        Assert.Equal(value2, reader.GetInt64(1));
        Assert.Equal(value4, reader.GetString(2));
    }
}
