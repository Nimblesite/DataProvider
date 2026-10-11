// Implements [CI-TEST-SHARED-COMMAND-PARAMETERS].
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.Shared;

namespace Nimblesite.Sync.Tests;

public sealed partial class ChangeApplierTests : IDisposable
{
    private static readonly NullLogger Logger = NullLogger.Instance;
    private readonly TestDb _db = new();

    [Fact]
    public void ApplyBatch_SkipsOwnOriginChanges()
    {
        var myOrigin = "my-origin";
        var batch = new SyncBatch(
            [
                Fixture_ApplyBatch_SkipsOwnOriginChanges_1(myOrigin: myOrigin),
                Fixture_ApplyBatch_SkipsOwnOriginChanges_2(),
            ],
            0,
            2,
            false
        );

        var appliedEntries = new List<SyncLogEntry>();

        var result = ChangeApplier.ApplyBatch(
            batch,
            myOrigin,
            3,
            entry =>
            {
                appliedEntries.Add(entry);
                return new BoolSyncOk(true);
            },
            Logger
        );

        var applyResult = TestDb.AssertSuccess(result);
        Assert.Single(appliedEntries);
        Assert.Equal("other-origin", appliedEntries[0].Origin);
        Assert.Equal(1, applyResult.AppliedCount);
    }

    [Fact]
    public void ApplyBatch_DefersAndRetriesFkViolations()
    {
        // Simulate: Child insert comes before Parent insert (FK violation on first try)
        var batch = new SyncBatch(
            [
                Fixture_ApplyBatch_DefersAndRetriesFkViolations_1(),
                Fixture_ApplyBatch_DefersAndRetriesFkViolations_2(),
            ],
            0,
            2,
            false
        );

        var attemptCounts = new Dictionary<long, int>();

        var result = ChangeApplier.ApplyBatch(
            batch,
            "my-origin",
            3,
            entry =>
            {
                attemptCounts.TryGetValue(entry.Version, out var count);
                attemptCounts[entry.Version] = count + 1;

                // Child fails first time (FK violation), succeeds after Parent is inserted
                if (entry.TableName == "Child" && count == 0)
                {
                    return new BoolSyncOk(false); // FK violation
                }

                return new BoolSyncOk(true);
            },
            Logger
        );

        var applyResult = TestDb.AssertSuccess(result);
        Assert.Equal(2, applyResult.AppliedCount);
        Assert.Equal(2, attemptCounts[1]); // Child tried twice
        Assert.Equal(1, attemptCounts[2]); // Parent tried once
    }

    [Fact]
    public void ApplyBatch_FailsAfterMaxRetries()
    {
        var batch = new SyncBatch([Fixture_ApplyBatch_FailsAfterMaxRetries_1()], 0, 1, false);

        var result = ChangeApplier.ApplyBatch(
            batch,
            "my-origin",
            3,
            _ => new BoolSyncOk(false),
            Logger
        ); // Always FK violation

        var failure = Assert.IsType<BatchApplyResultError>(result);
        Assert.IsType<SyncErrorDeferredChangeFailed>(failure.Value);
    }

    [Fact]
    public void ApplyBatch_WithRealSqlite_HandlesInsertUpdateDelete()
    {
        // Insert Parent first
        InsertParent("p1", "Parent1");

        var batch = new SyncBatch(
            [
                Fixture_ApplyBatch_WithRealSqlite_HandlesInsertUpdateDelete_1(),
                Fixture_ApplyBatch_WithRealSqlite_HandlesInsertUpdateDelete_2(),
            ],
            0,
            2,
            false
        );

        var result = ChangeApplier.ApplyBatch(batch, "my-origin", 3, ApplyToDb, Logger);

        var applyResult = TestDb.AssertSuccess(result);
        Assert.Equal(2, applyResult.AppliedCount);

        // Verify final state
        var childName = GetChildName("c1");
        Assert.Equal("UpdatedChild", childName);
    }

    [Fact]
    public void IsForeignKeyViolation_DetectsViolations()
    {
        Assert.True(ChangeApplier.IsForeignKeyViolation("FOREIGN KEY constraint failed"));
        Assert.True(ChangeApplier.IsForeignKeyViolation("FK_Child_Parent violation"));
        Assert.True(ChangeApplier.IsForeignKeyViolation("foreign key constraint violated"));
        Assert.False(ChangeApplier.IsForeignKeyViolation("UNIQUE constraint failed"));
    }

    private void InsertParent(string id, string name)
    {
        DatabaseCommands.Execute(
            connection: _db.Connection,
            configure: cmd => cmd.CommandText = "INSERT INTO Parent (Id, Name) VALUES ($id, $name)",
            parameters: () => [("$id", id), ("$name", name)]
        );
    }

    private string? GetChildName(string id)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Child WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteScalar() as string;
    }

    private BoolSyncResult ApplyToDb(SyncLogEntry entry)
    {
        try
        {
            using var cmd = _db.Connection.CreateCommand();

            if (entry.Operation == SyncOperation.Insert && entry.TableName == "Child")
            {
                var payload = System.Text.Json.JsonSerializer.Deserialize<
                    Dictionary<string, string>
                >(entry.Payload!);
                cmd.CommandText =
                    "INSERT INTO Child (Id, ParentId, Name) VALUES ($id, $parentId, $name)";
                cmd.Parameters.AddWithValue("$id", payload!["Id"]);
                cmd.Parameters.AddWithValue("$parentId", payload["ParentId"]);
                cmd.Parameters.AddWithValue("$name", payload["Name"]);
            }
            else if (entry.Operation == SyncOperation.Update && entry.TableName == "Child")
            {
                var payload = System.Text.Json.JsonSerializer.Deserialize<
                    Dictionary<string, string>
                >(entry.Payload!);
                cmd.CommandText = "UPDATE Child SET Name = $name WHERE Id = $id";
                cmd.Parameters.AddWithValue("$id", payload!["Id"]);
                cmd.Parameters.AddWithValue("$name", payload["Name"]);
            }
            else if (entry.Operation == SyncOperation.Delete && entry.TableName == "Child")
            {
                var pk = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
                    entry.PkValue
                );
                cmd.CommandText = "DELETE FROM Child WHERE Id = $id";
                cmd.Parameters.AddWithValue("$id", pk!["Id"]);
            }
            else
            {
                return new BoolSyncOk(true);
            }

            cmd.ExecuteNonQuery();
            return new BoolSyncOk(true);
        }
        catch (SqliteException ex) when (ChangeApplier.IsForeignKeyViolation(ex.Message))
        {
            return new BoolSyncOk(false);
        }
        catch (Exception ex)
        {
            return new BoolSyncError(new SyncErrorDatabase(ex.Message));
        }
    }

    public void Dispose() => _db.Dispose();
}
