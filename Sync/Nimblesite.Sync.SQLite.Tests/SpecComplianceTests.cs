// Implements [CI-TEST-SHARED-COMMAND-PARAMETERS].
// Implements [CI-TEST-SHARED-COMMAND].
using System.Diagnostics.CodeAnalysis;
using static global::Tests.Shared.ResultAssertions;
using static global::Tests.Shared.StringAssertions;
using static global::Tests.Shared.SyncFixtureSqliteHelpers;

namespace Nimblesite.Sync.SQLite.Tests;

/// <summary>
/// Integration tests proving docs/specs/sync-spec.md compliance.
/// Every spec section with testable requirements is covered.
/// NO MOCKS - REAL SQLITE DATABASES ONLY!
/// </summary>
public sealed partial class SpecComplianceTests : SqliteSyncTestBase
{
    public SpecComplianceTests()
        : base(name: "speccompliancetests", createSchema: CreateTestTable) { }

    #region Section 4: Primary Key Requirements

    [Fact]
    public void Spec_S4_UuidPrimaryKey_StoredInPkValueAsJson()
    {
        // Spec S4.1: Every tracked table MUST have single-column PK
        // Spec S4.3: pk_value is JSON: {"Id": "uuid-here"}
        var personId = Guid.NewGuid().ToString();
        InsertPerson(personId, "Alice", "alice@example.com");

        var changes = FetchChanges(0);
        Assert.Single(changes);
        Assert.Contains($"\"Id\":\"{personId}\"", changes[0].PkValue);
    }

    #endregion

    #region Section 5: Origin Identification

    [Fact]
    public void Spec_S5_OriginId_Is36CharUuid()
    {
        // Spec S5.4: Origin ID MUST be 36 characters (standard UUID format)
        var result = SyncSchema.GetOriginId(_db);

        var originId = (global::Tests.Shared.ResultAssertions.ExactOk(actual: result)).Value;
        Assert.Equal(36, originId.Length);
        Assert.True(Guid.TryParse(originId, out _));
    }

    [Fact]
    public void Spec_S5_OriginId_IncludedInEveryChangeLogEntry()
    {
        // Spec S5.5: Origin ID MUST be included in every change log entry
        SeedInsertUpdateDeleteP1();

        var changes = FetchChanges(0);
        Assert.Equal(3, changes.Count);
        Assert.All(changes, c => Assert.Equal(_originId, c.Origin));
    }

    #endregion

    #region Section 6: Timestamps

    [Fact]
    public void Spec_S6_Timestamps_Iso8601UtcWithMilliseconds()
    {
        // Spec S6.1: All timestamps MUST be ISO 8601 UTC with millisecond precision
        // Spec S6.2: MUST be UTC (Z suffix), MUST include milliseconds
        var changes = InsertAliceAndFetchSingle();
        var timestamp = changes[0].Timestamp;

        // Must end with Z (UTC)
        Assert.EndsWith("Z", timestamp);

        // Must be parseable as ISO 8601
        Assert.True(DateTime.TryParse(timestamp, out _));

        // Must have milliseconds (format: yyyy-MM-ddTHH:mm:ss.fffZ)
        Assert.Contains(".", timestamp);
    }

    #endregion

    #region Section 7: Unified Change Log

    [Fact]
    public void Spec_S7_UnifiedChangeLog_AllOperationsInSingleTable()
    {
        // Spec S7.1: All changes in single unified table with JSON payloads
        SeedInsertUpdateDeleteP1();

        var changes = FetchChanges(0);
        Assert.Equal(3, changes.Count);

        // Insert
        Assert.Equal(SyncOperation.Insert, changes[0].Operation);
        Assert.NotNull(changes[0].Payload);

        // Update
        Assert.Equal(SyncOperation.Update, changes[1].Operation);
        Assert.NotNull(changes[1].Payload);

        // Delete (tombstone)
        Assert.Equal(SyncOperation.Delete, changes[2].Operation);
        Assert.Null(changes[2].Payload); // Delete has NULL payload
    }

    [Fact]
    public void Spec_S7_JsonPayload_ContainsAllColumns()
    {
        // Spec S7.3: Payloads generated with explicit column lists
        var changes = InsertAliceAndFetchSingle();

        var payload = changes[0].Payload!;
        ContainsAll(
            actual: payload,
            expected: ["\"Id\":", "\"Name\":", "\"Email\":", "\"Alice\"", "\"alice@example.com\""]
        );
    }

    #endregion

    #region Section 8: Trigger Suppression

    [Fact]
    public void Spec_S8_TriggerSuppression_PreventsLoggingWhenActive()
    {
        // Spec S8.3: When sync_active = 1, triggers don't log
        SyncSessionManager.EnableSuppression(_db);

        InsertPerson("p1", "Alice", "alice@example.com");

        SyncSessionManager.DisableSuppression(_db);

        var changes = FetchChanges(0);
        Assert.Empty(changes); // No changes logged when suppression active
    }

    [Fact]
    public void Spec_S8_TriggerSuppression_LogsWhenDisabled()
    {
        // Verify normal behavior - triggers log when suppression disabled
        var suppressionResult = SyncSessionManager.IsSuppressionActive(_db);
        global::Tests.Shared.ResultAssertions.ExactOk(actual: suppressionResult);
        Assert.False((Ok(actual: suppressionResult)).Value);

        _ = InsertAliceAndFetchSingle();
    }

    #endregion

    #region Section 9: Trigger Generation

    [Fact]
    public void Spec_S9_TriggerGeneration_GeneratesAllThreeTriggers()
    {
        // Spec S9.4: Each tracked table MUST have INSERT, UPDATE, DELETE triggers
        // Create a new table and generate triggers
        using var cmd = _db.CreateCommand();
        cmd.CommandText =
            "CREATE TABLE Product (Id TEXT PRIMARY KEY, Name TEXT NOT NULL, Price REAL)";
        cmd.ExecuteNonQuery();

        var result = TriggerGenerator.CreateTriggers(_db, "Product", TestLogger.L);
        global::Tests.Shared.ResultAssertions.ExactOk(actual: result);

        // Verify triggers work by inserting/updating/deleting
        global::Nimblesite.Sync.SQLite.Tests.SyncRepositoryIntegrationTests.VerifyReusableWorkflow8(
            configure0: command =>
                command.CommandText =
                    "INSERT INTO Product (Id, Name, Price) VALUES ('prod1', 'Widget', 9.99)",
            configure1: command =>
                command.CommandText = "UPDATE Product SET Price = 19.99 WHERE Id = 'prod1'",
            input0: _db
        );

        using var deleteCmd = _db.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM Product WHERE Id = 'prod1'";
        deleteCmd.ExecuteNonQuery();

        var changes = FetchChanges(0).Where(c => c.TableName == "Product").ToList();
        Assert.Equal(3, changes.Count);
        Assert.Equal(SyncOperation.Insert, changes[0].Operation);
        Assert.Equal(SyncOperation.Update, changes[1].Operation);
        Assert.Equal(SyncOperation.Delete, changes[2].Operation);
    }

    [Fact]
    public void Spec_S9_TriggerGeneration_ChecksSuppressionFlag()
    {
        // Spec S9.4: Generated triggers MUST check suppression flag
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "CREATE TABLE Category (Id TEXT PRIMARY KEY, Name TEXT NOT NULL)";
        cmd.ExecuteNonQuery();

        TriggerGenerator.CreateTriggers(_db, "Category", TestLogger.L);

        // Enable suppression
        SyncSessionManager.EnableSuppression(_db);

        // Insert while suppression active
        using var insertCmd = _db.CreateCommand();
        insertCmd.CommandText = "INSERT INTO Category (Id, Name) VALUES ('cat1', 'Electronics')";
        insertCmd.ExecuteNonQuery();

        SyncSessionManager.DisableSuppression(_db);

        // No changes should be logged
        var changes = FetchChanges(0).Where(c => c.TableName == "Category").ToList();
        Assert.Empty(changes);
    }

    #endregion

    #region Section 10: Real-Time Subscriptions

    [Fact]
    public void Spec_S10_Subscriptions_RecordLevelMatching()
    {
        // Spec S10.2: Record subscription watches specific PK(s)
        var sub = SubscriptionManager.CreateRecordSubscription(
            "sub1",
            _originId,
            "Person",
            "[\"p1\", \"p2\"]",
            DateTime.UtcNow.ToString("O")
        );

        var matchingChange = Fixture_Spec_S10_Subscriptions_RecordLevelMatching_1();
        var nonMatchingChange = Fixture_Spec_S10_Subscriptions_RecordLevelMatching_2();

        AssertMatches(sub, matchingChange, nonMatchingChange);
    }

    [Fact]
    public void Spec_S10_Subscriptions_TableLevelMatchesAllChanges()
    {
        // Spec S10.2: Table subscription watches all changes in table
        var sub = SubscriptionManager.CreateTableSubscription(
            "sub1",
            _originId,
            "Person",
            DateTime.UtcNow.ToString("O")
        );

        var change1 = Fixture_Spec_S10_Subscriptions_TableLevelMatchesAllChanges_1();
        var change2 = Fixture_Spec_S10_Subscriptions_TableLevelMatchesAllChanges_2();
        var wrongTable = Fixture_Spec_S10_Subscriptions_TableLevelMatchesAllChanges_3();

        Assert.True(SubscriptionManager.MatchesChange(sub, change1));
        AssertMatches(sub, change2, wrongTable);
    }

    [Fact]
    public void Spec_S10_Subscriptions_ExpirationFiltering()
    {
        // Spec S10.7: Subscriptions can expire
        var now = DateTime.UtcNow;
        var expired = SubscriptionManager.CreateTableSubscription(
            "sub1",
            _originId,
            "Person",
            now.AddHours(-2).ToString("O"),
            now.AddHours(-1).ToString("O")
        );
        var active = SubscriptionManager.CreateTableSubscription(
            "sub2",
            _originId,
            "Person",
            now.AddHours(-1).ToString("O"),
            now.AddHours(1).ToString("O")
        );

        var currentTimestamp = now.ToString("O");
        Assert.True(SubscriptionManager.IsExpired(expired, currentTimestamp));
        Assert.False(SubscriptionManager.IsExpired(active, currentTimestamp));

        var filtered = SubscriptionManager.FilterExpired([expired, active], currentTimestamp);
        Assert.Single(filtered);
        Assert.Equal("sub2", filtered[0].SubscriptionId);
    }

    [Fact]
    public void Spec_S10_SubscriptionRepository_CrudOperations()
    {
        // Test full CRUD on _sync_subscriptions table
        var sub = SubscriptionManager.CreateTableSubscription(
            Guid.NewGuid().ToString(),
            _originId,
            "Person",
            DateTime.UtcNow.ToString("O")
        );

        // Insert
        var insertResult = SubscriptionRepository.Insert(_db, sub);
        global::Tests.Shared.ResultAssertions.ExactOk(actual: insertResult);

        // GetById
        var getResult = SubscriptionRepository.GetById(_db, sub.SubscriptionId);

        var retrieved = (global::Tests.Shared.ResultAssertions.ExactOk(actual: getResult)).Value;
        Assert.NotNull(retrieved);
        Assert.Equal(sub.TableName, retrieved!.TableName);

        // GetByTable
        var byTableResult = SubscriptionRepository.GetByTable(_db, "Person");
        Assert.IsType<SubscriptionListOk>(byTableResult);
        Assert.Contains(
            (Ok(actual: byTableResult)).Value,
            s => s.SubscriptionId == sub.SubscriptionId
        );

        // Delete
        var deleteResult = SubscriptionRepository.Delete(_db, sub.SubscriptionId);
        global::Tests.Shared.ResultAssertions.ExactOk(actual: deleteResult);

        // Verify deleted
        var afterDelete = SubscriptionRepository.GetById(_db, sub.SubscriptionId);
        global::Tests.Shared.ResultAssertions.ExactOk(actual: afterDelete);
        Assert.Null((Ok(actual: afterDelete)).Value);
    }

    #endregion

    #region Section 11: Bi-Directional Sync Protocol

    [Fact]
    public void Spec_S11_EchoPrevention_SkipsOwnOriginChanges()
    {
        // Spec S14.3: Changes MUST NOT be re-applied to their origin
        InsertPerson("p1", "Alice", "alice@example.com");
        var changes = FetchChanges(0);

        // Try to apply own change back
        var batch = new SyncBatch(changes, 0, changes[0].Version, false);
        var result = ChangeApplier.ApplyBatch(
            batch,
            _originId,
            3,
            ApplySingleChange,
            NullLogger.Instance
        );

        var applied = (global::Tests.Shared.ResultAssertions.ExactOk(actual: result)).Value;
        Assert.Equal(0, applied.AppliedCount); // Should skip own changes
    }

    [Fact]
    public void Spec_S11_ChangesAppliedInVersionOrder()
    {
        // Spec S11.3: Changes applied in global version order (ascending)
        InsertPerson("p1", "Alice", "alice@example.com");
        InsertPerson("p2", "Bob", "bob@example.com");
        InsertPerson("p3", "Charlie", "charlie@example.com");

        var changes = FetchChanges(0);
        Assert.Equal(3, changes.Count);

        // Verify ascending order
        Assert.True(changes[0].Version < changes[1].Version);
        Assert.True(changes[1].Version < changes[2].Version);
    }

    #endregion

    #region Section 12: Batching

    [Fact]
    public void Spec_S12_Batching_RespectsLimit()
    {
        // Spec S12.2: Batch query uses LIMIT
        for (int i = 0; i < 25; i++)
        {
            InsertPerson($"p{i}", $"Person{i}", $"p{i}@example.com");
        }

        var batchData = VerifySharedChecks2(input0: _db, value1: 10);
        Assert.Equal(10, batchData.Changes.Count);
        Assert.True(batchData.HasMore);
    }

    [Fact]
    public void Spec_S12_Batching_HasMoreFalseWhenExhausted()
    {
        // HasMore should be false when all changes fetched
        InsertPerson("p1", "Alice", "alice@example.com");

        var batchData = VerifySharedChecks2(input0: _db, value1: 100);
        Assert.Single(batchData.Changes);
        Assert.False(batchData.HasMore);
    }

    #endregion

    #region Section 13: Tombstone Retention

    [Fact]
    public void Spec_S13_TombstoneRetention_CalculatesSafePurgeVersion()
    {
        // Spec S13.4: Safe purge = MIN(last_sync_version) across all clients
        var clients = new List<SyncClient>
        {
            new("client1", 100, DateTime.UtcNow.ToString("O"), DateTime.UtcNow.ToString("O")),
            new("client2", 50, DateTime.UtcNow.ToString("O"), DateTime.UtcNow.ToString("O")),
            new("client3", 200, DateTime.UtcNow.ToString("O"), DateTime.UtcNow.ToString("O")),
        };

        var safePurge = TombstoneManager.CalculateSafePurgeVersion(clients);
        Assert.Equal(50, safePurge); // Minimum across all clients
    }

    [Fact]
    public void Spec_S13_FullResync_RequiredWhenBehind()
    {
        // Spec S13.6: Client behind oldest version needs full resync
        Assert.True(
            TombstoneManager.RequiresFullResync(clientLastVersion: 10, oldestAvailableVersion: 100)
        );
        Assert.False(
            TombstoneManager.RequiresFullResync(clientLastVersion: 150, oldestAvailableVersion: 100)
        );
    }

    [Fact]
    public void Spec_S13_StaleClients_IdentifiedByInactivity()
    {
        // Spec S13.5: Clients inactive > 90 days are stale
        var now = DateTime.UtcNow;
        var clients = new List<SyncClient>
        {
            new("active", 100, now.AddDays(-10).ToString("O"), now.AddDays(-100).ToString("O")),
            new("stale", 50, now.AddDays(-100).ToString("O"), now.AddDays(-100).ToString("O")),
        };

        var stale = TombstoneManager.FindStaleClients(clients, now, TimeSpan.FromDays(90));
        Assert.Single(stale);
        Assert.Equal("stale", stale[0]);
    }

    [Fact]
    public void Spec_S13_SyncClientRepository_CrudOperations()
    {
        // Test full CRUD on _sync_clients table
        var client = new SyncClient(
            Guid.NewGuid().ToString(),
            100,
            DateTime.UtcNow.ToString("O"),
            DateTime.UtcNow.ToString("O")
        );

        // Upsert (insert)
        var upsertResult = SyncClientRepository.Upsert(_db, client);
        global::Tests.Shared.ResultAssertions.ExactOk(actual: upsertResult);

        // GetByOrigin
        var getResult = SyncClientRepository.GetByOrigin(_db, client.OriginId);

        var retrieved = (global::Tests.Shared.ResultAssertions.ExactOk(actual: getResult)).Value;
        Assert.NotNull(retrieved);
        Assert.Equal(100, retrieved!.LastSyncVersion);

        // Upsert (update)
        var updated = client with
        {
            LastSyncVersion = 200,
        };
        SyncClientRepository.Upsert(_db, updated);

        var afterUpdate = SyncClientRepository.GetByOrigin(_db, client.OriginId);
        Assert.Equal(200, (Ok(actual: afterUpdate)).Value!.LastSyncVersion);

        // Delete
        var deleteResult = SyncClientRepository.Delete(_db, client.OriginId);
        global::Tests.Shared.ResultAssertions.ExactOk(actual: deleteResult);

        var afterDelete = SyncClientRepository.GetByOrigin(_db, client.OriginId);
        Assert.Null((Ok(actual: afterDelete)).Value);
    }

    #endregion

    #region Section 14: Conflict Resolution

    [Fact]
    public void Spec_S14_ConflictDetection_SameTablePkDifferentOrigin()
    {
        // Spec S14.1: Conflict when same table+PK, different origin
        var local = Fixture_Spec_S14_ConflictDetection_SameTablePkDifferentOrigin_1();
        var remote = Fixture_Spec_S14_ConflictDetection_SameTablePkDifferentOrigin_2();
        var noConflict = Fixture_Spec_S14_ConflictDetection_SameTablePkDifferentOrigin_3();

        Assert.True(ConflictResolver.IsConflict(local, remote));
        Assert.False(ConflictResolver.IsConflict(local, noConflict)); // Different PK
    }

    [Fact]
    public void Spec_S14_LastWriteWins_HigherTimestampWins()
    {
        // Spec S14.2: LWW - highest timestamp wins
        var older = Fixture_Spec_S14_LastWriteWins_HigherTimestampWins_1();
        var newer = Fixture_Spec_S14_LastWriteWins_HigherTimestampWins_2();

        var resolution = ConflictResolver.ResolveLastWriteWins(older, newer);
        Assert.Equal(newer, resolution.Winner);
        Assert.Equal(ConflictStrategy.LastWriteWins, resolution.Strategy);
    }

    [Fact]
    public void Spec_S14_ServerWins_AlwaysChoosesRemote()
    {
        VerifyConflictWinner(
            localFactory: Fixture_Spec_S14_ServerWins_AlwaysChoosesRemote_1,
            remoteFactory: Fixture_Spec_S14_ServerWins_AlwaysChoosesRemote_2,
            strategy: ConflictStrategy.ServerWins,
            expectRemote: true
        );
    }

    [Fact]
    public void Spec_S14_ClientWins_AlwaysChoosesLocal()
    {
        VerifyConflictWinner(
            localFactory: Fixture_Spec_S14_ClientWins_AlwaysChoosesLocal_1,
            remoteFactory: Fixture_Spec_S14_ClientWins_AlwaysChoosesLocal_2,
            strategy: ConflictStrategy.ClientWins,
            expectRemote: false
        );
    }

    #endregion

    #region Section 15: Hash Verification

    [Fact]
    public void Spec_S15_BatchHash_DeterministicAndVerifiable()
    {
        // Spec S15.4: Batch hash computed from changes
        InsertPerson("p1", "Alice", "alice@example.com");
        InsertPerson("p2", "Bob", "bob@example.com");

        var changes = FetchChanges(0);
        var hash1 = HashVerifier.ComputeBatchHash(changes);
        AssertBatchHashStable(changes);

        // Hash is 64 hex chars (SHA-256)
        Assert.Equal(64, hash1.Length);
        Assert.True(hash1.All(char.IsLetterOrDigit));
    }

    [Fact]
    public void Spec_S15_BatchHash_IncludedInBatch()
    {
        // Batches now include hash for verification
        InsertPerson("p1", "Alice", "alice@example.com");

        var batchData = VerifySharedChecks2(input0: _db, value1: 100);
        Assert.NotNull(batchData.Hash);

        // Verify the hash matches
        var verifyResult = BatchManager.VerifyBatchHash(batchData, NullLogger.Instance);
        global::Tests.Shared.ResultAssertions.ExactOk(actual: verifyResult);
        Assert.True((Ok(actual: verifyResult)).Value);
    }

    [Fact]
    public void Spec_S15_HashMismatch_DetectsCorruption()
    {
        // Spec S15.5: Mismatch indicates corruption/bug
        InsertPerson("p1", "Alice", "alice@example.com");
        var changes = FetchChanges(0);

        // Create batch with wrong hash
        var batch = new SyncBatch(changes, 0, changes[0].Version, false, "wrong_hash_value");

        var verifyResult = BatchManager.VerifyBatchHash(batch, NullLogger.Instance);
        Assert.IsType<BoolSyncError>(verifyResult);
    }

    [Fact]
    public void Spec_S15_CanonicalJson_SortedKeys()
    {
        // Spec S15.2: Keys sorted alphabetically
        var data = Dataset_Spec_S15_CanonicalJson_SortedKeys_1();

        var json = HashVerifier.ToCanonicalJson(data);

        // Alpha should come before Middle which comes before Zebra
        var alphaIndex = json.IndexOf("Alpha", StringComparison.Ordinal);
        var middleIndex = json.IndexOf("Middle", StringComparison.Ordinal);
        var zebraIndex = json.IndexOf("Zebra", StringComparison.Ordinal);

        Assert.True(alphaIndex < middleIndex);
        Assert.True(middleIndex < zebraIndex);
    }

    [Fact]
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Test code with hardcoded table names"
    )]
    public void Spec_S15_DatabaseHash_Deterministic()
    {
        // Spec S15.3: Full database hash is deterministic
        InsertPerson("p1", "Alice", "alice@example.com");
        InsertPerson("p2", "Bob", "bob@example.com");

        Func<string, IEnumerable<Dictionary<string, object?>>> getRows = tableName =>
        {
            var rows = new List<Dictionary<string, object?>>();
            using var cmd = _db.CreateCommand();
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities - test code
            cmd.CommandText = $"SELECT * FROM {tableName} ORDER BY Id";
#pragma warning restore CA2100
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                rows.Add(row);
            }
            return rows;
        };

        var hash1 = HashVerifier.ComputeDatabaseHash(["Person"], getRows);
        var hash2 = HashVerifier.ComputeDatabaseHash(["Person"], getRows);

        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length); // SHA-256 = 64 hex chars
    }

    #endregion

    #region Appendix A: Complete Schema

    [Fact]
    public void Spec_AppendixA_SyncStateTables_AllExist()
    {
        // Verify all spec-required tables exist
        var tables = GetTables();

        VerifyDatabaseWorkflow3(
            input0: tables,
            value0: "_sync_state",
            value1: "_sync_session",
            value2: "_sync_log",
            value3: "_sync_clients"
        );
    }

    [Fact]
    public void Spec_AppendixA_SyncLog_HasRequiredColumns()
    {
        var columns = GetTableColumns("_sync_log");

        VerifyDatabaseWorkflow2(
            input0: columns,
            value0: "version",
            value1: "table_name",
            value2: "pk_value",
            value3: "operation",
            value4: "payload",
            value5: "origin",
            value6: "timestamp"
        );
    }

    [Fact]
    public void Spec_AppendixA_SyncState_InitializedCorrectly()
    {
        // Spec: origin_id, last_server_version, last_push_version initialized
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT key FROM _sync_state";
        VerifySyncLoopWorkflow4(
            input0: cmd,
            value0: "origin_id",
            value1: "last_server_version",
            value2: "last_push_version"
        );
    }

    #endregion

    #region Helper Methods

    private static void CreateTestTable(SqliteConnection db)
    {
        DatabaseCommands.Execute(
            connection: db,
            configure: command => command.CommandText = Data_CreateTestTable_1
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void VerifyConflictWinner(
        Func<SyncLogEntry> localFactory,
        Func<SyncLogEntry> remoteFactory,
        ConflictStrategy strategy,
        bool expectRemote
    )
    {
        var local = localFactory();
        var remote = remoteFactory();
        var resolution = ConflictResolver.Resolve(local, remote, strategy);
        Assert.Equal(expectRemote ? remote : local, resolution.Winner);
    }

    private void InsertPerson(string id, string name, string email) =>
        SyncFixtureSqliteHelpers.InsertPerson(db: _db, id: id, name: name, email: email);

    private void UpdatePerson(string id, string name, string email) =>
        SyncFixtureSqliteHelpers.UpdatePerson(db: _db, id: id, name: name, email: email);

    private List<SyncLogEntry> InsertAliceAndFetchSingle()
    {
        InsertPerson("p1", "Alice", "alice@example.com");
        var changes = FetchChanges(0);
        Assert.Single(changes);
        return changes;
    }

    private void DeletePerson(string id) => DeletePersonWithEmail(db: _db, id: id);

    private void SeedInsertUpdateDeleteP1()
    {
        InsertPerson("p1", "Alice", "alice@example.com");
        UpdatePerson("p1", "Alice Updated", "alice2@example.com");
        DeletePerson("p1");
    }

    private List<SyncLogEntry> FetchChanges(long fromVersion) =>
        FetchSqliteChanges(_db, fromVersion, 1000);

    private BoolSyncResult ApplySingleChange(SyncLogEntry entry) =>
        ChangeApplierSQLite.ApplyChange(_db, entry);

    private List<string> GetTables()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
        return DatabaseCommands.ReadStrings(cmd);
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "PRAGMA with hardcoded test table names"
    )]
    private List<string> GetTableColumns(string tableName)
    {
        using var cmd = _db.CreateCommand();
#pragma warning disable CA2100 // PRAGMA table_info is safe
        cmd.CommandText = $"PRAGMA table_info({tableName})";
#pragma warning restore CA2100
        return DatabaseCommands.ReadStrings(cmd, ordinal: 1);
    }

    #endregion

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static SyncBatch VerifySharedChecks2(SqliteConnection input0, int value1)
    {
        var batch = BatchManager.FetchBatch(
            0,
            value1,
            (from, size) => SyncLogRepository.FetchChanges(input0, from, size),
            NullLogger.Instance
        );

        var batchData = (global::Tests.Shared.ResultAssertions.ExactOk(actual: batch)).Value;

        return batchData;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifySyncLoopWorkflow4(
        SqliteCommand input0,
        string value0,
        string value1,
        string value2
    )
    {
        var keys = DatabaseCommands.ReadStrings(input0);

        global::Nimblesite.Sync.SQLite.Tests.SpecConformanceTests.VerifyReusableWorkflow1(
            input0: keys,
            value0: value0,
            value1: value1,
            value2: value2
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyDatabaseWorkflow2(
        List<string> input0,
        string value0,
        string value1,
        string value2,
        string value3,
        string value4,
        string value5,
        string value6
    )
    {
        global::Nimblesite.Sync.SQLite.Tests.SpecConformanceTests.VerifyReusableWorkflow1(
            input0: input0,
            value0: value0,
            value1: value1,
            value2: value2
        );
        global::Nimblesite.Sync.SQLite.Tests.TombstoneIntegrationTests.VerifySyncLoopWorkflow1(
            input0: input0,
            value0: value3,
            value1: value4,
            value2: value5,
            value3: value6
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyDatabaseWorkflow3(
        List<string> input0,
        string value0,
        string value1,
        string value2,
        string value3
    )
    {
        global::Nimblesite.Sync.SQLite.Tests.TombstoneIntegrationTests.VerifySyncLoopWorkflow1(
            input0: input0,
            value0: value0,
            value1: value1,
            value2: value2,
            value3: value3
        );
        Assert.Contains("_sync_subscriptions", input0);
    }
}
