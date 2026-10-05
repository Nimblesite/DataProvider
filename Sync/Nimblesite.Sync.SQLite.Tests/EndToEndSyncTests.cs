using Microsoft.Data.Sqlite;
using Xunit;
using PersonCommand = global::Tests.Shared.PersonCommandParameters;

namespace Nimblesite.Sync.SQLite.Tests;

/// <summary>
/// Real end-to-end integration tests that sync data between two SQLite databases.
/// No mocks - actual database operations with triggers and change capture.
/// </summary>
public sealed class EndToEndSyncTests : IDisposable
{
    private readonly SqliteConnection _sourceDb;
    private readonly SqliteConnection _targetDb;
    private readonly string _sourceOrigin = Guid.NewGuid().ToString();
    private readonly string _targetOrigin = Guid.NewGuid().ToString();
    private readonly string _sourceDbPath;
    private readonly string _targetDbPath;

    public EndToEndSyncTests()
    {
        _sourceDbPath = Path.Combine(Path.GetTempPath(), $"e2e_source_{Guid.NewGuid()}.db");
        _targetDbPath = Path.Combine(Path.GetTempPath(), $"e2e_target_{Guid.NewGuid()}.db");
        _sourceDb = CreateDatabase(_sourceDbPath);
        _targetDb = CreateDatabase(_targetDbPath);

        SetupSchema(_sourceDb, _sourceOrigin);
        SetupSchema(_targetDb, _targetOrigin);
    }

    [Fact]
    public void Sync_InsertInSource_AppearsInTarget()
    {
        // Arrange: Insert a person in source
        InsertPerson(_sourceDb, "p1", "Alice", "alice@example.com");

        // Act: Fetch changes from source, apply to target
        var changes = FetchChangesFromSource();
        Assert.Single(changes);

        ApplyChangesToTarget(changes);

        // Assert: Person exists in target
        var person = GetPerson(_targetDb, "p1");
        Assert.NotNull(person);
        Assert.Equal("Alice", person.Value.Name);
        Assert.Equal("alice@example.com", person.Value.Email);
    }

    [Fact]
    public void Sync_UpdateInSource_UpdatesTarget()
    {
        // Arrange: Insert then update in source
        InsertPerson(_sourceDb, "p1", "Alice", "alice@example.com");
        UpdatePerson(_sourceDb, "p1", "Alice Updated", "alice.updated@example.com");

        // Act: Sync all changes
        var changes = FetchChangesFromSource();
        Assert.Equal(2, changes.Count);

        ApplyChangesToTarget(changes);

        // Assert: Target has updated values
        var person = GetPerson(_targetDb, "p1");
        Assert.NotNull(person);
        Assert.Equal("Alice Updated", person.Value.Name);
        Assert.Equal("alice.updated@example.com", person.Value.Email);
    }

    [Fact]
    public void Sync_DeleteInSource_DeletesFromTarget()
    {
        // Arrange: Insert in both, then delete from source
        InsertPerson(_sourceDb, "p1", "Alice", "alice@example.com");
        var changes1 = FetchChangesFromSource();
        ApplyChangesToTarget(changes1);

        // Verify target has the person
        Assert.NotNull(GetPerson(_targetDb, "p1"));

        // Delete from source
        DeletePerson(_sourceDb, "p1");

        // Act: Sync delete
        var changes2 = FetchChangesFromSource(changes1.Max(c => c.Version));
        Assert.Single(changes2);
        Assert.Equal(SyncOperation.Delete, changes2[0].Operation);

        ApplyChangesToTarget(changes2);

        // Assert: Person is gone from target
        Assert.Null(GetPerson(_targetDb, "p1"));
    }

    [Fact]
    public void Sync_MultipleRecords_AllSynced()
    {
        // Arrange: Insert multiple records
        InsertPerson(_sourceDb, "p1", "Alice", "alice@example.com");
        InsertPerson(_sourceDb, "p2", "Bob", "bob@example.com");
        InsertPerson(_sourceDb, "p3", "Charlie", "charlie@example.com");

        // Act: Nimblesite.Sync.Core
        var changes = FetchChangesFromSource();
        Assert.Equal(3, changes.Count);

        ApplyChangesToTarget(changes);

        // Assert: All records exist in target
        Assert.NotNull(GetPerson(_targetDb, "p1"));
        Assert.NotNull(GetPerson(_targetDb, "p2"));
        Assert.NotNull(GetPerson(_targetDb, "p3"));
    }

    [Fact]
    public void Sync_BatchedChanges_AllApplied()
    {
        // Arrange: Insert many records
        for (int i = 0; i < 50; i++)
        {
            InsertPerson(_sourceDb, $"p{i}", $"Person{i}", $"person{i}@example.com");
        }

        // Act: Fetch in batches of 10
        var allChanges = new List<SyncLogEntry>();
        long fromVersion = 0;
        while (true)
        {
            var batch = FetchChangesFromSource(fromVersion, batchSize: 10);
            if (batch.Count == 0)
                break;
            allChanges.AddRange(batch);
            fromVersion = batch.Max(c => c.Version);
        }

        Assert.Equal(50, allChanges.Count);

        // Apply in batches
        foreach (var batch in allChanges.Chunk(10))
        {
            ApplyChangesToTarget([.. batch]);
        }

        // Assert: All 50 records synced
        for (int i = 0; i < 50; i++)
        {
            Assert.NotNull(GetPerson(_targetDb, $"p{i}"));
        }
    }

    [Fact]
    public void Sync_TriggerSuppression_PreventsDuplicateLogging()
    {
        // Arrange: Insert in source, sync to target
        InsertPerson(_sourceDb, "p1", "Alice", "alice@example.com");
        var changes = FetchChangesFromSource();

        // Act: Apply with suppression enabled
        SyncSessionManager.EnableSuppression(_targetDb);
        ApplyChangesToTarget(changes, skipSuppression: true);
        SyncSessionManager.DisableSuppression(_targetDb);

        // Assert: Target should NOT have logged this change (it came from sync)
        var targetChanges = FetchChanges(_targetDb, 0);
        Assert.Empty(targetChanges);

        // But the data should be there
        Assert.NotNull(GetPerson(_targetDb, "p1"));
    }

    [Fact]
    public void Sync_SkipsOwnOriginChanges()
    {
        // Arrange: Create change with target's own origin
        var fakeEntry = new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Insert,
            "{\"Id\":\"p1\",\"Name\":\"Fake\",\"Email\":\"fake@example.com\"}",
            _targetOrigin, // Same as target's origin
            DateTime.UtcNow.ToString("O")
        );

        // Act: Try to apply - should skip because it's own origin
        var result = ChangeApplier.ApplyBatch(
            new SyncBatch([fakeEntry], 0, 1, false),
            _targetOrigin,
            3,
            entry => ApplySingleChange(_targetDb, entry),
            NullLogger.Instance
        );

        // Assert: No error, but nothing applied

        var applyResult = (Assert.IsType<BatchApplyResultOk>(result)).Value;
        Assert.Equal(0, applyResult.AppliedCount);
        Assert.Null(GetPerson(_targetDb, "p1"));
    }

    [Fact]
    public void Sync_BiDirectional_BothDbsGetChanges()
    {
        // Arrange: Insert in source
        InsertPerson(_sourceDb, "p1", "From Source", "source@example.com");

        // Insert in target (simulating offline change)
        InsertPerson(_targetDb, "p2", "From Target", "target@example.com");

        // Act: Sync source -> target
        var sourceChanges = FetchChangesFromSource();
        ApplyChangesToTarget(sourceChanges);

        // Sync target -> source
        var targetChanges = FetchChanges(_targetDb, 0);
        ApplyChanges(_sourceDb, targetChanges, _sourceOrigin);

        // Assert: Both DBs have both records
        Assert.NotNull(GetPerson(_sourceDb, "p1"));
        Assert.NotNull(GetPerson(_sourceDb, "p2"));
        Assert.NotNull(GetPerson(_targetDb, "p1"));
        Assert.NotNull(GetPerson(_targetDb, "p2"));
    }

    private static SqliteConnection CreateDatabase(string dbPath)
    {
        var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        return connection;
    }

    private static void SetupSchema(SqliteConnection connection, string originId)
    {
        SyncSchema.CreateSchema(connection);
        SyncSchema.SetOriginId(connection, originId);

        // Create Person table
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Person (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Email TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();

        // Use TriggerGenerator to create sync triggers (spec Section 9)
        var triggerResult = TriggerGenerator.CreateTriggers(connection, "Person", TestLogger.L);
        if (triggerResult is BoolSyncError { Value: SyncErrorDatabase dbError })
        {
            throw new InvalidOperationException($"Failed to create triggers: {dbError.Message}");
        }
    }

    private static void InsertPerson(SqliteConnection db, string id, string name, string email)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO Person (Id, Name, Email) VALUES (@id, @name, @email)";
        PersonCommand.Execute(command: cmd, id: id, name: name, email: email);
    }

    private static void UpdatePerson(SqliteConnection db, string id, string name, string email)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE Person SET Name = @name, Email = @email WHERE Id = @id";
        PersonCommand.Execute(command: cmd, id: id, name: name, email: email);
    }

    private static void DeletePerson(SqliteConnection db, string id)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "DELETE FROM Person WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    private static (string Id, string Name, string Email)? GetPerson(SqliteConnection db, string id)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Email FROM Person WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return (reader.GetString(0), reader.GetString(1), reader.GetString(2));
        }
        return null;
    }

    private List<SyncLogEntry> FetchChangesFromSource(long fromVersion = 0, int batchSize = 1000)
    {
        var result = SyncLogRepository.FetchChanges(_sourceDb, fromVersion, batchSize);
        Assert.IsType<SyncLogListOk>(result);
        return [.. ((SyncLogListOk)result).Value];
    }

    private static List<SyncLogEntry> FetchChanges(
        SqliteConnection db,
        long fromVersion,
        int batchSize = 1000
    )
    {
        var result = SyncLogRepository.FetchChanges(db, fromVersion, batchSize);
        Assert.IsType<SyncLogListOk>(result);
        return [.. ((SyncLogListOk)result).Value];
    }

    private void ApplyChangesToTarget(List<SyncLogEntry> changes, bool skipSuppression = false) =>
        ApplyChanges(_targetDb, changes, _targetOrigin, skipSuppression);

    private static void ApplyChanges(
        SqliteConnection db,
        List<SyncLogEntry> changes,
        string myOrigin,
        bool skipSuppression = false
    )
    {
        if (changes.Count == 0)
            return;

        if (!skipSuppression)
        {
            SyncSessionManager.EnableSuppression(db);
        }

        try
        {
            var batch = new SyncBatch(
                changes,
                changes.Min(c => c.Version) - 1,
                changes.Max(c => c.Version),
                false
            );

            var result = ChangeApplier.ApplyBatch(
                batch,
                myOrigin,
                3,
                entry => ApplySingleChange(db, entry),
                NullLogger.Instance
            );

            Assert.IsType<BatchApplyResultOk>(result);
        }
        finally
        {
            if (!skipSuppression)
            {
                SyncSessionManager.DisableSuppression(db);
            }
        }
    }

    private static BoolSyncResult ApplySingleChange(SqliteConnection db, SyncLogEntry entry)
    {
        try
        {
            using var cmd = db.CreateCommand();

            if (entry.TableName == "Person")
            {
                switch (entry.Operation)
                {
                    case SyncOperation.Insert:
                    case SyncOperation.Update:
                        var payload = System.Text.Json.JsonSerializer.Deserialize<
                            Dictionary<string, string>
                        >(entry.Payload!);
                        cmd.CommandText = """
                            INSERT INTO Person (Id, Name, Email) VALUES (@id, @name, @email)
                            ON CONFLICT(Id) DO UPDATE SET Name = @name, Email = @email
                            """;
                        cmd.Parameters.AddWithValue("@id", payload!["Id"]);
                        cmd.Parameters.AddWithValue("@name", payload["Name"]);
                        cmd.Parameters.AddWithValue("@email", payload["Email"]);
                        break;

                    case SyncOperation.Delete:
                        var pk = System.Text.Json.JsonSerializer.Deserialize<
                            Dictionary<string, string>
                        >(entry.PkValue);
                        cmd.CommandText = "DELETE FROM Person WHERE Id = @id";
                        cmd.Parameters.AddWithValue("@id", pk!["Id"]);
                        break;
                }

                cmd.ExecuteNonQuery();
            }

            return new BoolSyncOk(true);
        }
        catch (SqliteException ex)
        {
            if (ChangeApplier.IsForeignKeyViolation(ex.Message))
            {
                return new BoolSyncOk(false);
            }
            return new BoolSyncError(new SyncErrorDatabase(ex.Message));
        }
    }

    public void Dispose()
    {
        _sourceDb.Dispose();
        _targetDb.Dispose();
        if (File.Exists(_sourceDbPath))
        {
            try
            {
                File.Delete(_sourceDbPath);
            }
            catch
            { /* File may be locked */
            }
        }
        if (File.Exists(_targetDbPath))
        {
            try
            {
                File.Delete(_targetDbPath);
            }
            catch
            { /* File may be locked */
            }
        }
    }
}
