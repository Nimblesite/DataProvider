// Implements [CI-TEST-SHARED-COMMAND].

using static global::Tests.Shared.ResultAssertions;
using static global::Tests.Shared.SyncFixtureSqliteHelpers;

namespace Nimblesite.Sync.SQLite.Tests;

/// <summary>
/// Integration tests for SQLite repository classes.
/// Tests SyncClientRepository, SyncLogRepository, and SubscriptionRepository.
/// NO MOCKS - real SQLite databases only!
/// </summary>
public sealed partial class SyncRepositoryIntegrationTests : SqliteSyncTestBase
{
    private const string Timestamp = "2025-01-01T00:00:00.000Z";

    public SyncRepositoryIntegrationTests()
        : base(name: "syncrepositoryintegrationtests", createSchema: CreateSchema) { }

    private static void CreateSchema(SqliteConnection db)
    {
        // Create test table with triggers
        DatabaseCommands.Execute(
            connection: db,
            configure: command => command.CommandText = Data_Shared_1
        );
        TriggerGenerator.CreateTriggers(db, "Person", NullLogger.Instance);
    }

    #region SyncClientRepository Tests

    [Fact]
    public void SyncClientRepository_GetAll_EmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var result = SyncClientRepository.GetAll(_db);

        // Assert
        EmptyList(actual: result);
    }

    [Fact]
    public void SyncClientRepository_Upsert_NewClient_Inserts()
    {
        // Arrange
        var client = new SyncClient("client-1", 100, Timestamp, Timestamp);

        // Act
        global::Tests.Shared.ResultAssertions.Ok(actual: SyncClientRepository.Upsert(_db, client));

        var allClients = SyncClientRepository.GetAll(_db);
        Assert.Single((Ok(actual: allClients)).Value);
    }

    [Fact]
    public void SyncClientRepository_Upsert_ExistingClient_Updates()
    {
        // Arrange
        var client1 = new SyncClient("client-1", 100, Timestamp, Timestamp);
        SyncClientRepository.Upsert(_db, client1);

        var client2 = new SyncClient("client-1", 200, "2025-02-01T00:00:00Z", Timestamp);

        // Act
        global::Tests.Shared.ResultAssertions.Ok(actual: SyncClientRepository.Upsert(_db, client2));

        var client = (Ok(read: () => SyncClientRepository.GetByOrigin(_db, "client-1"))).Value;
        Assert.NotNull(client);
        Assert.Equal(200, client.LastSyncVersion);
    }

    [Fact]
    public void SyncClientRepository_GetByOrigin_ExistingClient_ReturnsClient()
    {
        // Arrange
        var client = new SyncClient("client-1", 100, Timestamp, Timestamp);
        SyncClientRepository.Upsert(_db, client);

        // Act
        // Act
        // Assert
        var retrieved = Ok(read: () => SyncClientRepository.GetByOrigin(_db, "client-1")).Value;
        Assert.NotNull(retrieved);
        Assert.Equal("client-1", retrieved.OriginId);
        Assert.Equal(100, retrieved.LastSyncVersion);
    }

    [Fact]
    public void SyncClientRepository_GetByOrigin_NonExistingClient_ReturnsNull()
    {
        // Act
        var result = SyncClientRepository.GetByOrigin(_db, "nonexistent");

        // Assert
        NullValue(actual: result);
    }

    [Fact]
    public void SyncClientRepository_Delete_ExistingClient_Removes()
    {
        // Arrange
        SyncClientRepository.Upsert(_db, new SyncClient("client-1", 100, Timestamp, Timestamp));

        // Act
        global::Tests.Shared.ResultAssertions.Ok(
            actual: SyncClientRepository.Delete(_db, "client-1")
        );

        var retrieved = SyncClientRepository.GetByOrigin(_db, "client-1");
        Assert.Null((Ok(actual: retrieved)).Value);
    }

    [Fact]
    public void SyncClientRepository_Delete_NonExistingClient_StillSucceeds()
    {
        // Act
        global::Tests.Shared.ResultAssertions.Ok(
            actual: SyncClientRepository.Delete(_db, "nonexistent")
        );
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(
        nameof(SyncClientRepository_GetMinVersion_EmptyDatabase_ReturnsZero_ContextCasesData)
    )]
    public void SyncClientRepository_GetMinVersion_EmptyDatabase_ReturnsZero_ContextCases(
        string caseName,
        global::System.Func<
            global::Microsoft.Data.Sqlite.SqliteConnection,
            global::Outcome.Result<global::System.Int64, global::Nimblesite.Sync.Core.SyncError>
        > value0
    )
    {
        NamedCase(caseName: caseName);
        ExerciseOperationCaseMatrix1(input0: _db, input1: VerifyReusableWorkflow6, value0: value0);
    }

    [Fact]
    public void SyncClientRepository_GetMinVersion_WithClients_ReturnsMinimum()
    {
        // Arrange
        SyncClientRepository.Upsert(_db, new SyncClient("client-a", 300, Timestamp, Timestamp));
        SyncClientRepository.Upsert(_db, new SyncClient("client-b", 100, Timestamp, Timestamp));
        SyncClientRepository.Upsert(_db, new SyncClient("client-c", 200, Timestamp, Timestamp));

        // Act
        var result = SyncClientRepository.GetMinVersion(_db);

        // Assert
        VerifyReusableWorkflow6(input0: result, value0: 100);
    }

    [Fact]
    public void SyncClientRepository_DeleteMultiple_RemovesSpecified()
    {
        // Arrange
        SyncClientRepository.Upsert(_db, new SyncClient("client-1", 100, Timestamp, Timestamp));
        SyncClientRepository.Upsert(_db, new SyncClient("client-2", 200, Timestamp, Timestamp));
        SyncClientRepository.Upsert(_db, new SyncClient("client-3", 300, Timestamp, Timestamp));

        // Act
        var result = SyncClientRepository.DeleteMultiple(_db, ["client-1", "client-2"]);

        // Assert
        VerifyReusableWorkflow7(input0: result, value0: 2);

        var remaining = SyncClientRepository.GetAll(_db);
        Assert.Single((Ok(actual: remaining)).Value);
    }

    #endregion

    #region SyncLogRepository Tests

    [Fact]
    public void SyncLogRepository_FetchChanges_EmptyLog_ReturnsEmptyList()
    {
        // Act
        var result = SyncLogRepository.FetchChanges(_db, 0, 100);

        // Assert
        EmptyList(actual: result);
    }

    [Fact]
    public void SyncLogRepository_FetchChanges_WithEntries_ReturnsEntries()
    {
        // Arrange - Insert via user table to trigger logging
        var entries = VerifyReusableWorkflow10(configure0: InsertAlice, input0: _db);
        Assert.Single(entries);
        Assert.Equal("Person", entries[0].TableName);
    }

    [Fact]
    public void SyncLogRepository_FetchChanges_RespectsBatchSize()
    {
        // Arrange
        VerifySyncLoopWorkflow2(input0: _db, value1: 10);

        // Act
        var result = SyncLogRepository.FetchChanges(_db, 0, 5);

        // Assert
        _ = global::Tests.Shared.ResultAssertions.Expected<SyncLogListOk>(actual: result);
        Assert.Equal(5, (Ok(actual: result)).Value.Count);
    }

    [Fact]
    public void SyncLogRepository_FetchChanges_FromVersion_FiltersCorrectly()
    {
        // Arrange
        VerifySyncLoopWorkflow2(input0: _db, value1: 5);

        // Get the max version
        // Get the max version
        var maxVersion = (Ok(read: () => SyncLogRepository.GetMaxVersion(_db))).Value;

        // Act - Fetch from version 3
        var result = SyncLogRepository.FetchChanges(_db, maxVersion - 2, 100);

        // Assert
        _ = global::Tests.Shared.ResultAssertions.Expected<SyncLogListOk>(actual: result);
        Assert.Equal(2, (Ok(actual: result)).Value.Count);
    }

    [Fact]
    public void SyncLogRepository_GetMaxVersion_WithEntries_ReturnsMax()
    {
        // Arrange
        VerifySyncLoopWorkflow2(input0: _db, value1: 5);

        // Act
        var result = SyncLogRepository.GetMaxVersion(_db);

        // Assert
        _ = global::Tests.Shared.ResultAssertions.Expected<LongSyncOk>(actual: result);
        Assert.True((Ok(actual: result)).Value >= 5);
    }

    [Fact]
    public void SyncLogRepository_Insert_LogsUpdate()
    {
        // Arrange
        var entries = VerifyPersonMutation(command =>
            command.CommandText = "UPDATE Person SET Name = 'Alice Updated' WHERE Id = 'p1'"
        );
        Assert.Equal(SyncOperation.Update, entries[0].Operation);
    }

    [Fact]
    public void SyncLogRepository_Delete_LogsTombstone()
    {
        // Arrange
        var entries = VerifyPersonMutation(command =>
            command.CommandText = "DELETE FROM Person WHERE Id = 'p1'"
        );
        Assert.Equal(SyncOperation.Delete, entries[0].Operation);
        Assert.Null(entries[0].Payload); // Tombstone has no payload
    }

    [Fact]
    public void SyncLogRepository_UpdateLastServerVersion_UpdatesValue()
    {
        // Act
        global::Tests.Shared.ResultAssertions.Ok(
            actual: SyncLogRepository.UpdateLastServerVersion(_db, 100)
        );

        var getResult = SyncLogRepository.GetLastServerVersion(_db);
        VerifyReusableWorkflow6(input0: getResult, value0: 100);
    }

    [Fact]
    public void SyncLogRepository_GetMinVersion_WithEntries_ReturnsMin()
    {
        // Arrange
        InsertPeople(_db, 5, "min");

        // Act
        var result = SyncLogRepository.GetMinVersion(_db);

        // Assert
        _ = global::Tests.Shared.ResultAssertions.Expected<LongSyncOk>(actual: result);
        Assert.True((Ok(actual: result)).Value >= 1);
    }

    [Fact]
    public void SyncLogRepository_GetEntryCount_WithEntries_ReturnsCount()
    {
        // Arrange
        InsertPeople(_db, 5, "cnt");

        // Act
        var result = SyncLogRepository.GetEntryCount(_db);

        // Assert
        VerifyReusableWorkflow6(input0: result, value0: 5);
    }

    [Fact]
    public void SyncLogRepository_FetchChanges_ContainsAllFields()
    {
        // Arrange
        var entries = VerifyReusableWorkflow10(
            configure0: command =>
                command.CommandText =
                    "INSERT INTO Person (Id, Name, Age) VALUES ('fld1', 'Alice', 30)",
            input0: _db
        );
        Assert.NotEmpty(entries);

        var entry = entries[^1];
        Assert.True(entry.Version > 0);
        Assert.Equal("Person", entry.TableName);
        Assert.Contains("fld1", entry.PkValue);
        Assert.Equal(SyncOperation.Insert, entry.Operation);
        Assert.NotNull(entry.Payload);
        Assert.Equal(_originId, entry.Origin);
        Assert.NotEmpty(entry.Timestamp);
    }

    #endregion

    #region SubscriptionRepository Tests

    [Fact]
    public void SubscriptionRepository_GetAll_EmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var result = SubscriptionRepository.GetAll(_db);

        // Assert
        EmptyList(actual: result);
    }

    [Fact]
    public void SubscriptionRepository_Insert_ValidSubscription_Succeeds()
    {
        // Arrange
        var subscription = Dataset_SubscriptionRepository_Insert_ValidSubscription_Succeeds_1();

        // Act
        global::Tests.Shared.ResultAssertions.Ok(
            actual: SubscriptionRepository.Insert(_db, subscription)
        );

        var all = SubscriptionRepository.GetAll(_db);
        Assert.Single((Ok(actual: all)).Value);
    }

    [Fact]
    public void SubscriptionRepository_GetById_ExistingSubscription_ReturnsSubscription()
    {
        // Arrange
        var subscription =
            Dataset_SubscriptionRepository_GetById_ExistingSubscription_ReturnsSubscription_1();
        SubscriptionRepository.Insert(_db, subscription);

        // Act
        // Act
        // Assert
        var retrieved = Ok(read: () => SubscriptionRepository.GetById(_db, "sub-1")).Value;
        Assert.NotNull(retrieved);
        Assert.Equal("sub-1", retrieved.SubscriptionId);
    }

    [Fact]
    public void SubscriptionRepository_GetById_NonExisting_ReturnsNull()
    {
        // Act
        var result = SubscriptionRepository.GetById(_db, "nonexistent");

        // Assert
        NullValue(actual: result);
    }

    [Fact]
    public void SubscriptionRepository_GetByTable_FiltersCorrectly()
    {
        // Arrange
        VerifyReusableWorkflow14(
            input0: _db,
            input1: Dataset_SubscriptionRepository_GetByTable_FiltersCorrectly_1,
            input2: Dataset_SubscriptionRepository_GetByTable_FiltersCorrectly_2
        );

        // Act
        // Act
        // Assert
        var subs = Ok(read: () => SubscriptionRepository.GetByTable(_db, "Person")).Value;
        Assert.Single(subs);
        Assert.Equal("sub-1", subs[0].SubscriptionId);
    }

    [Fact]
    public void SubscriptionRepository_GetByOrigin_FiltersCorrectly()
    {
        // Arrange
        var origin1 = "origin-1";
        var origin2 = "origin-2";
        InsertSubscriptions(
            () => Dataset_SubscriptionRepository_GetByOrigin_FiltersCorrectly_1(origin1: origin1),
            () => Dataset_SubscriptionRepository_GetByOrigin_FiltersCorrectly_2(origin2: origin2)
        );

        // Act
        // Act
        // Assert
        var subs = Ok(read: () => SubscriptionRepository.GetByOrigin(_db, origin1)).Value;
        Assert.Single(subs);
        Assert.Equal("sub-1", subs[0].SubscriptionId);
    }

    [Fact]
    public void SubscriptionRepository_Delete_ExistingSubscription_Removes()
    {
        // Arrange
        SubscriptionRepository.Insert(
            _db,
            Dataset_SubscriptionRepository_Delete_ExistingSubscription_Removes_1()
        );

        // Act
        global::Tests.Shared.ResultAssertions.Ok(
            actual: SubscriptionRepository.Delete(_db, "sub-1")
        );

        var all = SubscriptionRepository.GetAll(_db);
        Assert.Empty((Ok(actual: all)).Value);
    }

    [Fact]
    public void SubscriptionRepository_DeleteByOrigin_RemovesAll()
    {
        // Arrange
        var origin1 = "origin-1";
        InsertSubscriptions(
            () => Dataset_SubscriptionRepository_DeleteByOrigin_RemovesAll_1(origin1: origin1),
            () => Dataset_SubscriptionRepository_DeleteByOrigin_RemovesAll_2(origin1: origin1),
            Dataset_SubscriptionRepository_DeleteByOrigin_RemovesAll_3
        );

        // Act
        var result = SubscriptionRepository.DeleteByOrigin(_db, origin1);

        // Assert
        VerifyReusableWorkflow7(input0: result, value0: 2);

        var remaining = SubscriptionRepository.GetAll(_db);
        Assert.Single((Ok(actual: remaining)).Value);
    }

    [Fact]
    public void SubscriptionRepository_DeleteExpired_RemovesExpiredOnly()
    {
        // Arrange
        VerifyReusableWorkflow14(
            input0: _db,
            input1: Dataset_SubscriptionRepository_DeleteExpired_RemovesExpiredOnly_1,
            input2: Dataset_SubscriptionRepository_DeleteExpired_RemovesExpiredOnly_2
        );

        // Act
        var result = SubscriptionRepository.DeleteExpired(_db, "2025-07-01T00:00:00Z");

        // Assert
        VerifyReusableWorkflow7(input0: result, value0: 1);

        var remaining = SubscriptionRepository.GetAll(_db);
        Assert.Single((Ok(actual: remaining)).Value);
        Assert.Equal("sub-active", (Ok(actual: remaining)).Value[0].SubscriptionId);
    }

    #endregion

    #region Tombstone Integration Tests

    [Fact]
    public void TombstoneManager_CalculateSafeVersion_WithClients_ReturnsMinVersion()
    {
        // Arrange
        var clients =
            Dataset_TombstoneManager_CalculateSafeVersion_WithClients_ReturnsMinVersion_1();

        // Act
        var result = TombstoneManager.CalculateSafePurgeVersion(clients);

        // Assert
        Assert.Equal(100, result);
    }

    [Fact]
    public void TombstoneManager_PurgeTombstones_RemovesOldDeletes()
    {
        // Arrange - Insert and delete to create tombstones
        VerifyReusableWorkflow8(
            configure0: command =>
                command.CommandText = "INSERT INTO Person (Id, Name) VALUES ('p1', 'Alice')",
            configure1: command => command.CommandText = "DELETE FROM Person WHERE Id = 'p1'",
            input0: _db
        );

        // Get version
        var maxVersion = (Ok(actual: SyncLogRepository.GetMaxVersion(_db))).Value;

        // Use extension method to purge tombstones
        global::Tests.Shared.ResultAssertions.Ok(actual: _db.PurgeTombstones(maxVersion + 1));
        // Should have purged at least the delete tombstone
    }

    #endregion

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static IReadOnlyList<SyncLogEntry> VerifyReusableWorkflow3(
        SqliteConnection input0,
        global::System.Action<global::System.Data.Common.DbCommand> configure0,
        global::System.Action<global::System.Data.Common.DbCommand> configure1
    )
    { // Arrange
        DatabaseCommands.Execute(connection: input0, configure: configure0);

        var versionAfterInsert = (Ok(actual: SyncLogRepository.GetMaxVersion(input0))).Value;

        // Act - Update
        DatabaseCommands.Execute(connection: input0, configure: configure1);

        // Assert
        // Assert
        var entries = (
            Ok(read: () => SyncLogRepository.FetchChanges(input0, versionAfterInsert, 100))
        ).Value;
        Assert.Single(entries);

        return entries;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow6(
        Outcome.Result<long, SyncError> input0,
        int value0
    ) => EqualValue<long, SyncError>(expected: value0, actual: input0);

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow7(
        Outcome.Result<int, SyncError> input0,
        int value0
    ) => EqualValue(expected: value0, actual: input0);

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow8(
        SqliteConnection input0,
        global::System.Action<global::System.Data.Common.DbCommand> configure0,
        global::System.Action<global::System.Data.Common.DbCommand> configure1
    )
    { // Arrange - Insert and delete to create tombstones
        DatabaseCommands.Execute(connection: input0, configure: configure0);

        DatabaseCommands.Execute(connection: input0, configure: configure1);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static IReadOnlyList<SyncLogEntry> VerifyReusableWorkflow10(
        SqliteConnection input0,
        global::System.Action<global::System.Data.Common.DbCommand> configure0
    )
    { // Arrange - Insert via user table to trigger logging
        DatabaseCommands.Execute(connection: input0, configure: configure0);

        // Act
        // Act
        // Assert
        var entries = Ok(read: () => SyncLogRepository.FetchChanges(input0, 0, 100)).Value;

        return entries;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void VerifyReusableWorkflow14(
        SqliteConnection input0,
        Func<SyncSubscription> input1,
        Func<SyncSubscription> input2
    )
    { // Arrange
        SubscriptionRepository.Insert(input0, input1());
        SubscriptionRepository.Insert(input0, input2());
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseOperationCaseMatrix1(
        SqliteConnection input0,
        Action<Outcome.Result<long, SyncError>, int> input1,
        global::System.Func<
            global::Microsoft.Data.Sqlite.SqliteConnection,
            global::Outcome.Result<global::System.Int64, global::Nimblesite.Sync.Core.SyncError>
        > value0
    )
    { // Act
        var result = value0(arg: input0);

        // Assert
        input1(arg1: result, arg2: 0);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void InsertSubscriptions(params Func<SyncSubscription>[] subscriptions)
    {
        foreach (var subscription in subscriptions)
            SubscriptionRepository.Insert(_db, subscription());
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private IReadOnlyList<SyncLogEntry> VerifyPersonMutation(
        global::System.Action<global::System.Data.Common.DbCommand> configure1
    ) => VerifyReusableWorkflow3(configure0: InsertAlice, configure1: configure1, input0: _db);

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void InsertAlice(global::System.Data.Common.DbCommand command) =>
        command.CommandText = "INSERT INTO Person (Id, Name, Age) VALUES ('p1', 'Alice', 30)";

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void InsertPeople(SqliteConnection db, int count, string prefix)
    {
        for (var i = 0; i < count; i++)
        {
            DatabaseCommands.Execute(
                connection: db,
                configure: command =>
                    command.CommandText =
                        $"INSERT INTO Person (Id, Name, Age) VALUES ('{prefix}{i}', 'Person {i}', {20 + i})"
            );
        }
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifySyncLoopWorkflow2(SqliteConnection input0, int value1)
    { // Arrange
        InsertPeople(input0, value1, "p");
    }
}
