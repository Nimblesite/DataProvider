// Implements [CI-TEST-SHARED-COMMAND-PARAMETERS].
// Implements [CI-TEST-SHARED-COMMAND].
using Microsoft.Data.Sqlite;
using Tests.Shared;
using Xunit;

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
        _db = new SqliteConnection($"Data Source={_dbPath}");
        _db.Open();
        SyncSchema.CreateSchema(_db);
        SyncSchema.SetOriginId(_db, _originId);

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
        var entry = Fixture_ApplyChange_Insert_CreatesNewRecord_1();

        // Act
        var result = ChangeApplierSQLite.ApplyChange(_db, entry);

        // Assert
        Assert.True(result is BoolSyncOk);
        Assert.True(((BoolSyncOk)result).Value);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Name, Age, Email FROM Person WHERE Id = 'p1'";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("Alice", reader.GetString(0));
        Assert.Equal(30, reader.GetInt64(1));
        Assert.Equal("alice@example.com", reader.GetString(2));
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
        var result = ChangeApplierSQLite.ApplyChange(_db, entry2);

        // Assert
        Assert.True(result is BoolSyncOk);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Name, Age FROM Person WHERE Id = 'p1'";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
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

        var entry = Fixture_ApplyChange_Update_ModifiesExistingRecord_1();

        // Act
        ApplyChangeAndAssertSuccess(input0: entry);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Name, Age, Email FROM Person WHERE Id = 'p1'";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("Alice Updated", reader.GetString(0));
        Assert.Equal(31, reader.GetInt64(1));
        Assert.Equal("alice.new@example.com", reader.GetString(2));
    }

    [Fact]
    public void ApplyChange_Update_NonExistingRecord_InsertsAsUpsert()
    {
        // Arrange
        var entry = Fixture_ApplyChange_Update_NonExistingRecord_InsertsAsUpsert_1();

        // Act
        ApplyChangeAndAssertSuccess(input0: entry);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Person WHERE Id = 'p1'";
        Assert.Equal(1L, cmd.ExecuteScalar());
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
        ApplyChangeAndAssertSuccess(input0: entry);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Person WHERE Id = 'p1'";
        Assert.Equal(0L, cmd.ExecuteScalar());
    }

    [Fact]
    public void ApplyChange_Delete_NonExistingRecord_StillSucceeds()
    {
        // Arrange
        var entry = Fixture_ApplyChange_Delete_NonExistingRecord_StillSucceeds_1();

        // Act
        ApplyChangeAndAssertSuccess(input0: entry);
    }

    #endregion

    #region JSON Type Handling

    [Fact]
    public void ApplyChange_Insert_HandlesStringValues()
    {
        // Arrange
        var entry = Fixture_ApplyChange_Insert_HandlesStringValues_1();

        // Act
        ApplyChangeAndAssertSuccess(input0: entry);
    }

    [Fact]
    public void ApplyChange_Insert_HandlesIntegerValues()
    {
        // Arrange
        var entry = Fixture_ApplyChange_Insert_HandlesIntegerValues_1();

        // Act
        ApplyChangeAndAssertSuccess(input0: entry);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Age FROM Person WHERE Id = 'p1'";
        Assert.Equal(25L, cmd.ExecuteScalar());
    }

    [Fact]
    public void ApplyChange_Insert_HandlesNullValues()
    {
        // Arrange
        var entry = Fixture_ApplyChange_Insert_HandlesNullValues_1();

        // Act
        ApplyChangeAndAssertSuccess(input0: entry);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Email FROM Person WHERE Id = 'p1'";
        Assert.True(cmd.ExecuteScalar() is DBNull);
    }

    [Fact]
    public void ApplyChange_Insert_HandlesBooleanValues()
    {
        // Arrange - Create table with boolean-like column
        DatabaseCommands.Execute(
            connection: _db,
            configure: command =>
                command.CommandText = "CREATE TABLE Settings (Id TEXT PRIMARY KEY, Enabled INTEGER)"
        );

        var entry = Fixture_ApplyChange_Insert_HandlesBooleanValues_1();

        // Act
        ApplyChangeAndAssertSuccess(input0: entry);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Enabled FROM Settings WHERE Id = 's1'";
        Assert.Equal(1L, cmd.ExecuteScalar());
    }

    [Fact]
    public void ApplyChange_Insert_HandlesFalseBooleanValues()
    {
        // Arrange
        DatabaseCommands.Execute(
            connection: _db,
            configure: command =>
                command.CommandText = "CREATE TABLE Settings (Id TEXT PRIMARY KEY, Enabled INTEGER)"
        );

        var entry = Fixture_ApplyChange_Insert_HandlesFalseBooleanValues_1();

        // Act
        ApplyChangeAndAssertSuccess(input0: entry);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Enabled FROM Settings WHERE Id = 's1'";
        Assert.Equal(0L, cmd.ExecuteScalar());
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

        var entry = Fixture_ApplyChange_Insert_HandlesDoubleValues_1();

        // Act
        ApplyChangeAndAssertSuccess(input0: entry);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Price FROM Product WHERE Id = 'prod1'";
        var price = Convert.ToDouble(
            cmd.ExecuteScalar(),
            System.Globalization.CultureInfo.InvariantCulture
        );
        Assert.Equal(99.99, price, precision: 2);
    }

    #endregion

    #region Foreign Key Handling

    [Fact]
    public void ApplyChange_Insert_WithForeignKeyViolation_ReturnsFalse()
    {
        // Arrange - Create FK relationship
        DatabaseCommands.Execute(
            connection: _db,
            configure: command =>
                command.CommandText = Data_ApplyChange_Insert_WithForeignKeyViolation_ReturnsFalse_1
        );

        var entry = Fixture_ApplyChange_Insert_WithForeignKeyViolation_ReturnsFalse_1();

        // Act
        var result = ChangeApplierSQLite.ApplyChange(_db, entry);

        // Assert - FK violation returns false (for deferred retry)
        Assert.True(result is BoolSyncOk);
        Assert.False(((BoolSyncOk)result).Value);
    }

    [Fact]
    public void ApplyChange_Insert_WithValidForeignKey_Succeeds()
    {
        // Arrange - Create FK relationship and parent record
        DatabaseCommands.Execute(
            connection: _db,
            configure: command =>
                command.CommandText = Data_ApplyChange_Insert_WithValidForeignKey_Succeeds_1
        );

        var entry = Fixture_ApplyChange_Insert_WithValidForeignKey_Succeeds_1();

        // Act
        var result = ChangeApplierSQLite.ApplyChange(_db, entry);

        // Assert
        Assert.True(result is BoolSyncOk);
        Assert.True(((BoolSyncOk)result).Value);
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
                Assert.True(r is BoolSyncOk);
                Assert.True(((BoolSyncOk)r).Value);
            }
        );

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Person";
        Assert.Equal(3L, cmd.ExecuteScalar());
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
        _db.Dispose();
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch
            { /* File may be locked */
            }
        }
    }

    #endregion

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void ApplyChangeAndAssertSuccess(global::Nimblesite.Sync.Core.SyncLogEntry input0)
    {
        // Act
        var result = ChangeApplierSQLite.ApplyChange(_db, input0);

        // Assert
        Assert.True(result is BoolSyncOk);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseCaseMatrix1(Func<SyncLogEntry> input0, SqliteConnection input1)
    { // Arrange
        var entry = input0();

        // Act
        var result = ChangeApplierSQLite.ApplyChange(input1, entry);

        // Assert
        Assert.True(result is BoolSyncError);
    }
}
