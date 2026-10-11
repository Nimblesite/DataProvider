// Implements [CI-TEST-SHARED-COMMAND-PARAMETERS].
// Implements [CI-TEST-SHARED-COMMAND].
using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Tests.Shared;
using static global::Tests.Shared.ResultAssertions;
using PullResultResult = Outcome.Result<
    Nimblesite.Sync.Core.PullResult,
    Nimblesite.Sync.Core.SyncError
>;
using PushResultResult = Outcome.Result<
    Nimblesite.Sync.Core.PushResult,
    Nimblesite.Sync.Core.SyncError
>;

namespace Nimblesite.Sync.Tests;

/// <summary>
/// Integration tests for SyncCoordinator.
/// Uses real SQLite databases - no mocks.
/// </summary>
public sealed partial class SyncCoordinatorTests : IDisposable
{
    private static readonly ILogger Logger = NullLogger.Instance;
    private readonly string _serverDbPath = global::Tests.Shared.TempSqliteDatabase.PathFor(
        "synccoord_server"
    );
    private readonly string _clientDbPath = global::Tests.Shared.TempSqliteDatabase.PathFor(
        "synccoord_client"
    );
    private readonly SqliteConnection _serverDb;
    private readonly SqliteConnection _clientDb;
    private const string ServerOrigin = "server-coord-001";
    private const string ClientOrigin = "client-coord-001";

    public SyncCoordinatorTests()
    {
        _serverDb = CreateSyncDatabase(ServerOrigin, _serverDbPath);
        _clientDb = CreateSyncDatabase(ClientOrigin, _clientDbPath);
    }

    #region Pull Tests

    [Fact]
    public void Pull_EmptyServer_ReturnsZeroChanges()
    {
        var lastVersion = 0L;

        var pull = PullAndAssert(fromVersion: lastVersion, batch: new BatchConfig(100), expected: 0);
        Assert.Equal(0, pull.FromVersion);
        Assert.Equal(0, pull.ToVersion);
    }

    [Fact]
    public void Pull_ServerHasChanges_AppliesAll()
    {
        // Server has data
        VerifyReusableWorkflow6(input0: _serverDb, value1: "Alice", value3: "Bob");

        var pull = PullAndAssert(fromVersion: 0, batch: new BatchConfig(100), expected: 2);
        Assert.Equal(0, pull.FromVersion);
        Assert.True(pull.ToVersion > 0);

        VerifyPersons(_clientDb, "Alice", "p1", "Bob", "p2");
    }

    [Fact]
    public void Pull_SkipsOwnOriginChanges_EchoPrevention()
    {
        // Insert on server
        global::Nimblesite.Sync.Tests.SyncIntegrationTests.InsertPerson(
            db: _serverDb,
            id: "p1",
            name: "Alice"
        );

        // Manually insert a change with CLIENT origin into server's sync log
        InsertSyncLogEntry(
            _serverDb,
            "Person",
            "{\"Id\":\"p2\"}",
            "insert",
            "{\"Id\":\"p2\",\"Name\":\"FromClient\"}",
            ClientOrigin
        );

        var pull = PullAndAssert(fromVersion: 0, batch: new BatchConfig(100), expected: 1);
        Assert.Equal("Alice", GetPersonName(_clientDb, "p1"));
        Assert.Null(GetPersonName(_clientDb, "p2")); // Should NOT be applied
    }

    [Fact]
    public void Pull_MultipleBatches_ProcessesAll()
    {
        // Create 25 records
        VerifySyncLoopWorkflow3(input0: _serverDb, value1: 25);

        _ = PullAndAssert(fromVersion: 0, batch: new BatchConfig(10), expected: 25);

        // Verify all records
        for (var i = 1; i <= 25; i++)
        {
            Assert.Equal($"Person {i}", GetPersonName(_clientDb, $"p{i}"));
        }
    }

    [Fact]
    public void Pull_WithForeignKeys_RetriesDeferred()
    {
        // Create Parent first, then Child (FK dependency)
        global::Nimblesite.Sync.Tests.SyncIntegrationTests.InsertParent(
            db: _serverDb,
            id: "parent1",
            name: "Parent One"
        );
        global::Nimblesite.Sync.Tests.SyncIntegrationTests.InsertChild(
            db: _serverDb,
            id: "child1",
            parentId: "parent1",
            name: "Child One"
        );

        var result = PullFromServer(fromVersion: 0, batch: new BatchConfig(100, 3)); // Allow retries

        var pull = (global::Tests.Shared.ResultAssertions.ExactOk(actual: result)).Value;
        Assert.Equal(2, pull.ChangesApplied);

        Assert.Equal("Parent One", GetParentName(_clientDb, "parent1"));
        Assert.Equal("Child One", GetChildName(_clientDb, "child1"));
    }

    [Fact]
    public void Pull_TriggerSuppressionFailure_ReturnsError()
    {
        var result = SyncCoordinator.Pull(
            myOriginId: ClientOrigin,
            lastSyncedVersion: 0,
            config: new BatchConfig(100),
            fetchRemoteChanges: FetchServerChanges,
            applyLocalChange: ApplyClientChange,
            enableTriggerSuppression: () =>
                new BoolSyncError(new SyncErrorDatabase("Suppression failed")),
            disableTriggerSuppression: DisableClientSuppression,
            updateLastSyncedVersion: SaveClientSyncVersion,
            logger: Logger
        );

        var error = (Assert.IsType<PullResultError>(result)).Value;
        Assert.IsType<SyncErrorDatabase>(error);
    }

    [Fact]
    public void Pull_FetchError_ReturnsError()
    {
        var result = SyncCoordinator.Pull(
            myOriginId: ClientOrigin,
            lastSyncedVersion: 0,
            config: new BatchConfig(100),
            fetchRemoteChanges: (from, limit) =>
                new SyncLogListError(new SyncErrorDatabase("Fetch failed")),
            applyLocalChange: ApplyClientChange,
            enableTriggerSuppression: EnableClientSuppression,
            disableTriggerSuppression: DisableClientSuppression,
            updateLastSyncedVersion: SaveClientSyncVersion,
            logger: Logger
        );

        Assert.IsType<PullResultError>(result);
    }

    [Fact]
    public void Pull_UpdatesVersion_Correctly()
    {
        global::Nimblesite.Sync.Tests.SyncIntegrationTests.InsertPerson(
            db: _serverDb,
            id: "p1",
            name: "Alice"
        );

        var capturedVersion = 0L;
        _ = SyncCoordinator.Pull(
            myOriginId: ClientOrigin,
            lastSyncedVersion: 0,
            config: new BatchConfig(100),
            fetchRemoteChanges: FetchServerChanges,
            applyLocalChange: ApplyClientChange,
            enableTriggerSuppression: EnableClientSuppression,
            disableTriggerSuppression: DisableClientSuppression,
            updateLastSyncedVersion: v => capturedVersion = v,
            logger: Logger
        );

        Assert.True(capturedVersion > 0);
    }

    #endregion

    #region Push Tests

    [Fact]
    public void Push_EmptyClient_ReturnsZeroChanges()
    {
        _ = PushAndAssert(fromVersion: 0, batch: new BatchConfig(100), expected: 0);
    }

    [Fact]
    public void Push_ClientHasChanges_PushesAll()
    {
        VerifyReusableWorkflow6(input0: _clientDb, value1: "Charlie", value3: "Diana");

        _ = PushAndAssert(fromVersion: 0, batch: new BatchConfig(100), expected: 2);

        VerifyPersons(_serverDb, "Charlie", "p1", "Diana", "p2");
    }

    [Fact]
    public void Push_MultipleBatches_PushesAll()
    {
        VerifySyncLoopWorkflow3(input0: _clientDb, value1: 30);

        _ = PushAndAssert(fromVersion: 0, batch: new BatchConfig(10), expected: 30);
    }

    [Fact]
    public void Push_SendFailure_ReturnsError()
    {
        global::Nimblesite.Sync.Tests.SyncIntegrationTests.InsertPerson(
            db: _clientDb,
            id: "p1",
            name: "Alice"
        );

        var result = SyncCoordinator.Push(
            lastPushedVersion: 0,
            config: new BatchConfig(100),
            fetchLocalChanges: FetchClientChanges,
            sendToRemote: changes => new BoolSyncError(new SyncErrorDatabase("Send failed")),
            updateLastPushedVersion: SaveClientPushVersion,
            logger: Logger
        );

        Assert.IsType<PushResultError>(result);
    }

    #endregion

    #region Bidirectional Sync Tests

    [Fact]
    public void Sync_PullThenPush_Both()
    {
        // Server has Alice
        VerifyReusableWorkflow7(input0: _serverDb, input1: _clientDb);

        var result = SyncCoordinator.Sync(
            myOriginId: ClientOrigin,
            lastServerVersion: 0,
            lastPushVersion: 0,
            config: new BatchConfig(100),
            fetchRemoteChanges: FetchServerChanges,
            applyLocalChange: ApplyClientChange,
            enableTriggerSuppression: EnableClientSuppression,
            disableTriggerSuppression: DisableClientSuppression,
            updateLastServerVersion: SaveClientSyncVersion,
            fetchLocalChanges: FetchClientChanges,
            sendToRemote: SendChangesToServer,
            updateLastPushVersion: SaveClientPushVersion,
            logger: Logger
        );

        var sync = (global::Tests.Shared.ResultAssertions.ExactOk(actual: result)).Value;
        Assert.Equal(1, sync.Pull.ChangesApplied);
        Assert.Equal(1, sync.Push.ChangesPushed);

        // Client has both
        VerifyPersons(_clientDb, "ServerAlice", "s1", "ClientBob", "c1");

        // Server has both
        VerifyPersons(_serverDb, "ServerAlice", "s1", "ClientBob", "c1");
    }

    [Fact]
    public void Sync_PullFailure_ReturnsError()
    {
        var result = SyncCoordinator.Sync(
            ClientOrigin,
            0,
            0,
            new BatchConfig(100),
            fetchRemoteChanges: (from, limit) =>
                new SyncLogListError(new SyncErrorDatabase("Pull failed")),
            applyLocalChange: ApplyClientChange,
            enableTriggerSuppression: EnableClientSuppression,
            disableTriggerSuppression: DisableClientSuppression,
            updateLastServerVersion: SaveClientSyncVersion,
            fetchLocalChanges: FetchClientChanges,
            sendToRemote: SendChangesToServer,
            updateLastPushVersion: SaveClientPushVersion,
            Logger
        );

        Assert.IsType<SyncResultError>(result);
    }

    [Fact]
    public void Sync_PushFailure_AfterSuccessfulPull_ReturnsError()
    {
        VerifyReusableWorkflow7(input0: _serverDb, input1: _clientDb);

        var result = SyncCoordinator.Sync(
            ClientOrigin,
            0,
            0,
            new BatchConfig(100),
            fetchRemoteChanges: FetchServerChanges,
            applyLocalChange: ApplyClientChange,
            enableTriggerSuppression: EnableClientSuppression,
            disableTriggerSuppression: DisableClientSuppression,
            updateLastServerVersion: SaveClientSyncVersion,
            fetchLocalChanges: FetchClientChanges,
            sendToRemote: changes => new BoolSyncError(new SyncErrorDatabase("Push failed")),
            updateLastPushVersion: SaveClientPushVersion,
            Logger
        );

        Assert.IsType<SyncResultError>(result);
        // Pull should have succeeded - client has server's data
        Assert.Equal("ServerAlice", GetPersonName(_clientDb, "s1"));
    }

    [Fact]
    public void Sync_IncrementalSync_OnlyNewChanges()
    {
        // Initial sync
        global::Nimblesite.Sync.Tests.SyncIntegrationTests.InsertPerson(
            db: _serverDb,
            id: "s1",
            name: "ServerAlice"
        );

        var firstPull = (
            Ok(read: () => PullFromServer(fromVersion: 0, batch: new BatchConfig(100)))
        ).Value;
        Assert.Equal(1, firstPull.ChangesApplied);

        // Add more data on server
        global::Nimblesite.Sync.Tests.SyncIntegrationTests.InsertPerson(
            db: _serverDb,
            id: "s2",
            name: "ServerBob"
        );

        // Second sync from last version
        // Second sync from last version
        var secondPull = (
            Ok(read: () =>
                PullFromServer(fromVersion: firstPull.ToVersion, batch: new BatchConfig(100))
            )
        ).Value;
        Assert.Equal(1, secondPull.ChangesApplied); // Only Bob
        Assert.Equal("ServerBob", GetPersonName(_clientDb, "s2"));
    }

    #endregion

    #region Helper Methods

    // Shared wiring: the delegate bundle bound to the client/server test databases
    // that every pull/push test passes to SyncCoordinator.

    private SyncLogListResult FetchServerChanges(long from, int limit) =>
        FetchChanges(_serverDb, from, limit);

    private SyncLogListResult FetchClientChanges(long from, int limit) =>
        FetchChanges(_clientDb, from, limit);

    private BoolSyncResult ApplyClientChange(SyncLogEntry entry) => ApplyChange(_clientDb, entry);

    private BoolSyncResult EnableClientSuppression() => EnableSuppression(_clientDb);

    private BoolSyncResult DisableClientSuppression() => DisableSuppression(_clientDb);

    private void SaveClientSyncVersion(long version) => SetLastSyncVersion(_clientDb, version);

    private BoolSyncResult SendChangesToServer(IReadOnlyList<SyncLogEntry> changes) =>
        ApplyChangesToTarget(_serverDb, changes, ServerOrigin);

    private void SaveClientPushVersion(long version) => SetLastPushVersion(_clientDb, version);

    private PullResultResult PullFromServer(long fromVersion, BatchConfig batch) =>
        SyncCoordinator.Pull(
            myOriginId: ClientOrigin,
            lastSyncedVersion: fromVersion,
            config: batch,
            fetchRemoteChanges: FetchServerChanges,
            applyLocalChange: ApplyClientChange,
            enableTriggerSuppression: EnableClientSuppression,
            disableTriggerSuppression: DisableClientSuppression,
            updateLastSyncedVersion: SaveClientSyncVersion,
            logger: Logger
        );

    private PullResult PullAndAssert(long fromVersion, BatchConfig batch, int expected)
    {
        var result = PullFromServer(fromVersion: fromVersion, batch: batch);
        var pull = (global::Tests.Shared.ResultAssertions.ExactOk(actual: result)).Value;
        Assert.Equal(expected, pull.ChangesApplied);
        return pull;
    }

    private PushResult PushAndAssert(long fromVersion, BatchConfig batch, int expected)
    {
        var result = PushFromClient(fromVersion: fromVersion, batch: batch);
        var push = (global::Tests.Shared.ResultAssertions.ExactOk(actual: result)).Value;
        Assert.Equal(expected, push.ChangesPushed);
        return push;
    }

    private PushResultResult PushFromClient(long fromVersion, BatchConfig batch) =>
        SyncCoordinator.Push(
            lastPushedVersion: fromVersion,
            config: batch,
            fetchLocalChanges: FetchClientChanges,
            sendToRemote: SendChangesToServer,
            updateLastPushedVersion: SaveClientPushVersion,
            logger: Logger
        );

    private static SqliteConnection CreateSyncDatabase(string originId, string dbPath)
    {
        var conn = global::Nimblesite.Sync.Tests.SyncIntegrationTests.VerifyReusableWorkflow10(
            configure0: command =>
                command.CommandText = SqlData_CreateSyncDatabase_1(originId: originId),
            input0: dbPath
        );

        return conn;
    }

    private static SyncLogListResult FetchChanges(SqliteConnection db, long fromVersion, int limit)
    {
        try
        {
            return new SyncLogListOk(
                TestDb.FetchChanges(db: db, fromVersion: fromVersion, limit: limit)
            );
        }
        catch (SqliteException ex)
        {
            return new SyncLogListError(new SyncErrorDatabase(ex.Message));
        }
    }

    private static BoolSyncResult EnableSuppression(SqliteConnection db) =>
        SetSuppression(db: db, active: true);

    private static BoolSyncResult DisableSuppression(SqliteConnection db) =>
        SetSuppression(db: db, active: false);

    private static BoolSyncResult SetSuppression(SqliteConnection db, bool active)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE _sync_session SET sync_active = $active";
        cmd.Parameters.AddWithValue("$active", active ? 1 : 0);
        cmd.ExecuteNonQuery();
        return new BoolSyncOk(true);
    }

    private static BoolSyncResult ApplyChange(SqliteConnection db, SyncLogEntry entry)
    {
        try
        {
            switch (entry.Operation)
            {
                case SyncOperation.Insert:
                case SyncOperation.Update:
                    ApplyUpsert(db, entry);
                    break;
                case SyncOperation.Delete:
                    global::Nimblesite.Sync.Tests.SyncIntegrationTests.ApplyDelete(
                        db: db,
                        entry: entry
                    );
                    break;
            }
            return new BoolSyncOk(true);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return new BoolSyncOk(false); // FK violation - defer
        }
        catch (SqliteException ex)
        {
            return new BoolSyncError(new SyncErrorDatabase(ex.Message));
        }
    }

    private static BoolSyncResult ApplyChangesToTarget(
        SqliteConnection db,
        IReadOnlyList<SyncLogEntry> changes,
        string targetOrigin
    )
    {
        EnableSuppression(db);
        try
        {
            foreach (var entry in changes)
            {
                // Skip echo
                if (entry.Origin == targetOrigin)
                    continue;
                ApplyChange(db, entry);
            }
            return new BoolSyncOk(true);
        }
        finally
        {
            DisableSuppression(db);
        }
    }

    private static void ApplyUpsert(SqliteConnection db, SyncLogEntry entry)
    {
        global::Nimblesite.Sync.Tests.SyncIntegrationTests.VerifyUnifiedWorkflow101(
            input0: entry,
            input1: db,
            value10: "INSERT OR REPLACE INTO Child (Id, ParentId, Name) VALUES ($id, $pid, $name)",
            value13: "$pid"
        );
    }

    private static void InsertSyncLogEntry(
        SqliteConnection db,
        string tableName,
        string pkValue,
        string operation,
        string payload,
        string origin
    )
    {
        DatabaseCommands.Execute(
            connection: db,
            configure: cmd =>
                cmd.CommandText = """
                INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
                VALUES ($table, $pk, $op, $payload, $origin, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                """,
            parameters: () =>
                [
                    ("$table", tableName),
                    ("$pk", pkValue),
                    ("$op", operation),
                    ("$payload", payload),
                    ("$origin", origin),
                ]
        );
    }

    private static string? GetName(SqliteConnection db, string table, string id)
    {
        return global::Tests.Shared.DatabaseCommands.Scalar(
                connection: db,
                configure: command =>
                    command.CommandText = $"SELECT Name FROM {table} WHERE Id = $id",
                parameters: () => [("$id", id)]
            ) as string;
    }

    private static string? GetPersonName(SqliteConnection db, string id) =>
        GetName(db, "Person", id);

    private static string? GetParentName(SqliteConnection db, string id) =>
        GetName(db, "Parent", id);

    private static string? GetChildName(SqliteConnection db, string id) => GetName(db, "Child", id);

    private static void VerifyPersons(
        SqliteConnection db,
        string firstName,
        string firstId,
        string secondName,
        string secondId
    ) =>
        global::Nimblesite.Sync.Tests.SyncIntegrationTests.VerifyReusableWorkflow1(
            input0: GetPersonName,
            input1: db,
            value0: firstName,
            value1: firstId,
            value2: secondName,
            value3: secondId
        );

    private static void SetLastSyncVersion(SqliteConnection db, long version) =>
        SetStateValue(db, "last_server_version", version);

    private static void SetLastPushVersion(SqliteConnection db, long version) =>
        SetStateValue(db, "last_push_version", version);

    private static void SetStateValue(SqliteConnection db, string key, long version)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE _sync_state SET value = $v WHERE key = $k";
        cmd.Parameters.AddWithValue("$v", version.ToString(CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$k", key);
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _serverDb.Dispose();
        _clientDb.Dispose();
        global::Tests.Shared.TempSqliteDatabase.DeleteIfExists(path: _serverDbPath);
        global::Tests.Shared.TempSqliteDatabase.DeleteIfExists(path: _clientDbPath);
    }

    #endregion
    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow6(
        SqliteConnection input0,
        string value1,
        string value3
    )
    { // Server has data
        global::Nimblesite.Sync.Tests.SyncIntegrationTests.InsertPerson(
            db: input0,
            id: "p1",
            name: value1
        );
        global::Nimblesite.Sync.Tests.SyncIntegrationTests.InsertPerson(
            db: input0,
            id: "p2",
            name: value3
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow7(SqliteConnection input0, SqliteConnection input1)
    { // Server has Alice
        global::Nimblesite.Sync.Tests.SyncIntegrationTests.InsertPerson(
            db: input0,
            id: "s1",
            name: "ServerAlice"
        );

        // Client has Bob
        global::Nimblesite.Sync.Tests.SyncIntegrationTests.InsertPerson(
            db: input1,
            id: "c1",
            name: "ClientBob"
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifySyncLoopWorkflow3(SqliteConnection input0, int value1)
    { // Create 25 records
        for (var i = 1; i <= value1; i++)
        {
            global::Nimblesite.Sync.Tests.SyncIntegrationTests.InsertPerson(
                db: input0,
                id: $"p{i}",
                name: $"Person {i}"
            );
        }
    }
}
