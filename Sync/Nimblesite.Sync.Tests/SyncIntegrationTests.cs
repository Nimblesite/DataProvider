// Implements [CI-TEST-SHARED-COMMAND-PARAMETERS].
// Implements [CI-TEST-SHARED-COMMAND].
using Microsoft.Data.Sqlite;
using Tests.Shared;
using static global::Tests.Shared.SyncFixtureSqliteHelpers;

namespace Nimblesite.Sync.Tests;

/// <summary>
/// Real end-to-end integration tests syncing between two SQLite databases.
/// No mocks - actual data flowing between server and client DBs.
/// </summary>
public sealed partial class SyncIntegrationTests : DualSqliteSyncTestBase
{
    private const string ServerOrigin = "server-origin-001";
    private const string ClientOrigin = "client-origin-001";

    public SyncIntegrationTests()
        : base(
            serverPath: global::Tests.Shared.TempSqliteDatabase.PathFor("syncintegration_server"),
            clientPath: global::Tests.Shared.TempSqliteDatabase.PathFor("syncintegration_client"),
            serverOrigin: ServerOrigin,
            clientOrigin: ClientOrigin,
            createSql: SqlData_CreateSyncDatabase_1
        ) { }

    [Fact]
    public void PullChanges_ServerHasNewRecords_ClientReceivesThem()
    {
        // Server has data
        InsertPerson(_serverDb, "p1", "Alice");
        InsertPerson(_serverDb, "p2", "Bob");

        // Sync: Pull from server to client
        PullToClient();
        VerifyReusableWorkflow1(
            input0: GetPersonName,
            input1: _clientDb,
            value0: "Alice",
            value1: "p1",
            value2: "Bob",
            value3: "p2"
        );
    }

    [Fact]
    public void PullChanges_ServerHasUpdates_ClientGetsUpdates()
    {
        // Initial sync
        InsertPerson(_serverDb, "p1", "Alice");
        PullChanges(_serverDb, _clientDb, ClientOrigin);

        // Server updates
        UpdatePerson(_serverDb, "p1", "Alice Updated");

        // Sync again
        PullToClient();
        Assert.Equal("Alice Updated", GetPersonName(_clientDb, "p1"));
    }

    [Fact]
    public void PullChanges_ServerDeletesRecord_ClientDeletesIt()
    {
        // Initial sync
        InsertPerson(_serverDb, "p1", "Alice");
        PullChanges(_serverDb, _clientDb, ClientOrigin);
        Assert.Equal("Alice", GetPersonName(_clientDb, "p1"));

        // Server deletes
        DeletePerson(_serverDb, "p1");

        // Sync again
        PullToClient();
        Assert.Null(GetPersonName(_clientDb, "p1"));
    }

    [Fact]
    public void PushChanges_ClientHasNewRecords_ServerReceivesThem()
    {
        // Client creates data locally
        InsertPerson(_clientDb, "p1", "Charlie");

        // Push from client to server
        PushToServer();
        Assert.Equal("Charlie", GetPersonName(_serverDb, "p1"));
    }

    [Fact]
    public void BiDirectionalSync_BothHaveChanges_BothGetUpdated()
    {
        // Server has Alice
        InsertPerson(_serverDb, "p1", "Alice");

        // Client has Bob (created offline)
        InsertPerson(_clientDb, "p2", "Bob");

        // Pull server -> client
        PullChanges(_serverDb, _clientDb, ClientOrigin);

        // Push client -> server
        PushChanges(_clientDb, _serverDb, ServerOrigin);

        // Both should have both records
        VerifyReusableWorkflow1(
            input0: GetPersonName,
            input1: _serverDb,
            value0: "Alice",
            value1: "p1",
            value2: "Bob",
            value3: "p2"
        );
        VerifyReusableWorkflow1(
            input0: GetPersonName,
            input1: _clientDb,
            value0: "Alice",
            value1: "p1",
            value2: "Bob",
            value3: "p2"
        );
    }

    [Fact]
    public void Sync_WithForeignKeys_HandlesCorrectOrder()
    {
        // Server has parent and child
        InsertParent(_serverDb, "parent1", "Parent One");
        InsertChild(_serverDb, "child1", "parent1", "Child One");

        // Pull to client - should handle FK order via defer/retry
        PullToClient();
        Assert.Equal("Parent One", GetParentName(_clientDb, "parent1"));
        Assert.Equal("Child One", GetChildName(_clientDb, "child1"));
    }

    [Fact]
    public void Sync_EchoPreventionWorks_NoInfiniteLoop()
    {
        // Server creates record
        InsertPerson(_serverDb, "p1", "Alice");

        // Get server's sync log count before
        var serverLogCountBefore = GetSyncLogCount(_serverDb);

        // Pull to client (should NOT create new log entries on server)
        PullChanges(_serverDb, _clientDb, ClientOrigin);

        // Push back (client got the record but it's from server origin, should skip)
        PushChanges(_clientDb, _serverDb, ServerOrigin);

        // Server log count should NOT have grown from the push
        var serverLogCountAfter = GetSyncLogCount(_serverDb);
        Assert.Equal(serverLogCountBefore, serverLogCountAfter);
    }

    [Fact]
    public void Sync_LargeBatch_ProcessesInBatches()
    {
        // Create 50 records on server
        for (var i = 1; i <= 50; i++)
        {
            InsertPerson(_serverDb, $"p{i}", $"Person {i}");
        }

        // Pull with small batch size
        var pullResult = PullChangesWithBatchSize(
            _serverDb,
            _clientDb,
            ClientOrigin,
            batchSize: 10
        );

        Assert.Equal(50, pullResult);
        VerifyReusableWorkflow1(
            input0: GetPersonName,
            input1: _clientDb,
            value0: "Person 1",
            value1: "p1",
            value2: "Person 50",
            value3: "p50"
        );
    }

    [Fact]
    public void HashVerification_AfterSync_HashesMatch()
    {
        // Create data on server
        InsertPerson(_serverDb, "p1", "Alice");
        InsertPerson(_serverDb, "p2", "Bob");

        // Sync to client
        PullChanges(_serverDb, _clientDb, ClientOrigin);

        // Compute hashes
        var serverHash = ComputePersonTableHash(_serverDb);
        var clientHash = ComputePersonTableHash(_clientDb);

        Assert.Equal(serverHash, clientHash);
    }

    // === Helper Methods ===

    private static int GetSyncLogCount(SqliteConnection db)
    {
        return global::Tests.Shared.DatabaseCommands.ScalarInt32(
            connection: db,
            configure: command => command.CommandText = "SELECT COUNT(*) FROM _sync_log"
        );
    }

    private static long GetLastSyncVersion(SqliteConnection db)
    {
        return long.Parse(
            (string)
                global::Tests.Shared.DatabaseCommands.Scalar(
                    connection: db,
                    configure: command =>
                        command.CommandText =
                            "SELECT value FROM _sync_state WHERE key = 'last_server_version'"
                )!,
            System.Globalization.CultureInfo.InvariantCulture
        );
    }

    private static void SetLastSyncVersion(SqliteConnection db, long version)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            "UPDATE _sync_state SET value = $version WHERE key = 'last_server_version'";
        cmd.Parameters.AddWithValue(
            "$version",
            version.ToString(System.Globalization.CultureInfo.InvariantCulture)
        );
        cmd.ExecuteNonQuery();
    }

    private static void SetSyncActive(SqliteConnection db, bool active)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE _sync_session SET sync_active = $active";
        cmd.Parameters.AddWithValue("$active", active ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    private void PullToClient() =>
        VerifyReusableWorkflow2(
            input0: PullChanges,
            input1: _serverDb,
            input2: _clientDb,
            value0: ClientOrigin
        );

    private void PushToServer() =>
        VerifyReusableWorkflow2(
            input0: PushChanges,
            input1: _clientDb,
            input2: _serverDb,
            value0: ServerOrigin
        );

    private int PullChanges(
        SqliteConnection source,
        SqliteConnection target,
        string targetOrigin
    ) => PullChangesWithBatchSize(source, target, targetOrigin, 1000);

    private int PullChangesWithBatchSize(
        SqliteConnection source,
        SqliteConnection target,
        string targetOrigin,
        int batchSize
    )
    {
        var lastVersion = GetLastSyncVersion(target);
        var totalApplied = 0;

        // Enable trigger suppression on target
        SetSyncActive(target, true);

        try
        {
            var result = BatchManager.ProcessAllBatches(
                lastVersion,
                new BatchConfig(batchSize),
                (from, limit) =>
                    new SyncLogListOk(
                        TestDb.FetchChanges(db: source, fromVersion: from, limit: limit)
                    ),
                batch =>
                {
                    var applyResult = ChangeApplier.ApplyBatch(
                        batch,
                        targetOrigin,
                        3,
                        entry => ApplyChange(target, entry),
                        Logger
                    );
                    return applyResult;
                },
                version => SetLastSyncVersion(target, version),
                Logger
            );

            if (result is IntSyncOk success)
            {
                totalApplied = success.Value;
            }
        }
        finally
        {
            SetSyncActive(target, false);
        }

        return totalApplied;
    }

    private int PushChanges(
        SqliteConnection source,
        SqliteConnection target,
        string targetOrigin
    ) =>
        // For push, we read from source's log and apply to target
        // This is similar to pull but in reverse direction
        PullChanges(source, target, targetOrigin);

    private static BoolSyncResult ApplyChange(SqliteConnection db, SyncLogEntry entry)
    {
        try
        {
            using var cmd = db.CreateCommand();

            switch (entry.Operation)
            {
                case SyncOperation.Insert:
                    ApplyInsert(db, entry);
                    break;
                case SyncOperation.Update:
                    ApplyUpdate(db, entry);
                    break;
                case SyncOperation.Delete:
                    ApplyDelete(db, entry);
                    break;
            }

            return new BoolSyncOk(true);
        }
        catch (SqliteException ex) when (ChangeApplier.IsForeignKeyViolation(ex.Message))
        {
            return new BoolSyncOk(false); // Defer for retry
        }
        catch (Exception ex)
        {
            return new BoolSyncError(new SyncErrorDatabase(ex.Message));
        }
    }

    private static void ApplyInsert(SqliteConnection db, SyncLogEntry entry)
    {
        VerifyUnifiedWorkflow101(
            input0: entry,
            input1: db,
            value10: "INSERT OR REPLACE INTO Child (Id, ParentId, Name) VALUES ($id, $parentId, $name)",
            value13: "$parentId"
        );
    }

    private static void ApplyUpdate(SqliteConnection db, SyncLogEntry entry) =>
        // For simplicity, treat update same as insert (upsert)
        ApplyInsert(db, entry);

    internal static void ApplyDelete(SqliteConnection db, SyncLogEntry entry)
    {
        var pk = Assert.IsType<Dictionary<string, System.Text.Json.JsonElement>>(
            System.Text.Json.JsonSerializer.Deserialize<
                Dictionary<string, System.Text.Json.JsonElement>
            >(entry.PkValue)
        );

        using var cmd = db.CreateCommand();
        cmd.CommandText = $"DELETE FROM {entry.TableName} WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", pk["Id"].GetString());
        cmd.ExecuteNonQuery();
    }

    private static string ComputePersonTableHash(SqliteConnection db)
    {
        var rows = new List<Dictionary<string, object?>>();

        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM Person ORDER BY Id";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(
                new Dictionary<string, object?>
                {
                    ["Id"] = reader.GetString(0),
                    ["Name"] = reader.GetString(1),
                }
            );
        }

        return HashVerifier.ComputeDatabaseHash(["Person"], _ => rows);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyUnifiedWorkflow101(
        SyncLogEntry input0,
        SqliteConnection input1,
        string value10,
        string value13
    )
    {
        var payload = Assert.IsType<Dictionary<string, System.Text.Json.JsonElement>>(
            System.Text.Json.JsonSerializer.Deserialize<
                Dictionary<string, System.Text.Json.JsonElement>
            >(Assert.IsType<string>(input0.Payload))
        );

        using var cmd = input1.CreateCommand();

        switch (input0.TableName)
        {
            case "Person":
            case "Parent":
                cmd.CommandText =
                    $"INSERT OR REPLACE INTO {input0.TableName} (Id, Name) VALUES ($id, $name)";
                cmd.Parameters.AddWithValue("$id", payload["Id"].GetString());
                cmd.Parameters.AddWithValue("$name", payload["Name"].GetString());
                break;
            case "Child":
                cmd.CommandText = value10;
                cmd.Parameters.AddWithValue("$id", payload["Id"].GetString());
                cmd.Parameters.AddWithValue(value13, payload["ParentId"].GetString());
                cmd.Parameters.AddWithValue("$name", payload["Name"].GetString());
                break;
        }

        cmd.ExecuteNonQuery();
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow1(
        Func<SqliteConnection, string, string?> input0,
        SqliteConnection input1,
        string value0,
        string value1,
        string value2,
        string value3
    )
    {
        Assert.Equal(value0, input0(arg1: input1, arg2: value1));
        Assert.Equal(value2, input0(arg1: input1, arg2: value3));
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void VerifyReusableWorkflow2(
        Func<SqliteConnection, SqliteConnection, string, int> input0,
        SqliteConnection input1,
        SqliteConnection input2,
        global::System.String value0
    )
    {
        // Sync: Pull from server to client
        var pullResult = input0(arg1: input1, arg2: input2, arg3: value0);

        Assert.True(pullResult > 0);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static SqliteConnection VerifyReusableWorkflow10(
        string input0,
        global::System.Action<global::System.Data.Common.DbCommand> configure0
    )
    {
        var conn = new SqliteConnection($"Data Source={input0}");
        conn.Open();

        DatabaseCommands.Execute(connection: conn, configure: configure0);

        return conn;
    }
}
