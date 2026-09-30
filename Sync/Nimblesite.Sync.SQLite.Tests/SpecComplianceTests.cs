using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Nimblesite.Sync.SQLite.Tests;

/// <summary>
/// Integration tests proving docs/specs/sync-spec.md compliance.
/// Every spec section with testable requirements is covered.
/// NO MOCKS - REAL SQLITE DATABASES ONLY!
/// </summary>
public sealed class SpecComplianceTests : IDisposable
{
    private readonly SqliteConnection _db;
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"speccompliancetests_{Guid.NewGuid()}.db"
    );
    private readonly string _originId = Guid.NewGuid().ToString();

    public SpecComplianceTests()
    {
        _db = new SqliteConnection($"Data Source={_dbPath}");
        _db.Open();
        SyncSchema.CreateSchema(_db);
        SyncSchema.SetOriginId(_db, _originId);
        CreateTestTable();
    }

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
        Assert.IsType<StringSyncOk>(result);
        var originId = ((StringSyncOk)result).Value;
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
        InsertPerson("p1", "Alice", "alice@example.com");

        var changes = FetchChanges(0);
        Assert.Single(changes);
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
        InsertPerson("p1", "Alice", "alice@example.com");

        var changes = FetchChanges(0);
        Assert.Single(changes);

        var payload = changes[0].Payload!;
        Assert.Contains("\"Id\":", payload);
        Assert.Contains("\"Name\":", payload);
        Assert.Contains("\"Email\":", payload);
        Assert.Contains("\"Alice\"", payload);
        Assert.Contains("\"alice@example.com\"", payload);
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
        Assert.IsType<BoolSyncOk>(suppressionResult);
        Assert.False(((BoolSyncOk)suppressionResult).Value);

        InsertPerson("p1", "Alice", "alice@example.com");

        var changes = FetchChanges(0);
        Assert.Single(changes);
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
        Assert.IsType<BoolSyncOk>(result);

        // Verify triggers work by inserting/updating/deleting
        using var insertCmd = _db.CreateCommand();
        insertCmd.CommandText =
            "INSERT INTO Product (Id, Name, Price) VALUES ('prod1', 'Widget', 9.99)";
        insertCmd.ExecuteNonQuery();

        using var updateCmd = _db.CreateCommand();
        updateCmd.CommandText = "UPDATE Product SET Price = 19.99 WHERE Id = 'prod1'";
        updateCmd.ExecuteNonQuery();

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

        var matchingChange = new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Update,
            "{}",
            _originId,
            ""
        );
        var nonMatchingChange = new SyncLogEntry(
            2,
            "Person",
            "{\"Id\":\"p3\"}",
            SyncOperation.Update,
            "{}",
            _originId,
            ""
        );

        Assert.True(SubscriptionManager.MatchesChange(sub, matchingChange));
        Assert.False(SubscriptionManager.MatchesChange(sub, nonMatchingChange));
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

        var change1 = new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Insert,
            "{}",
            _originId,
            ""
        );
        var change2 = new SyncLogEntry(
            2,
            "Person",
            "{\"Id\":\"p999\"}",
            SyncOperation.Delete,
            null,
            _originId,
            ""
        );
        var wrongTable = new SyncLogEntry(
            3,
            "Order",
            "{\"Id\":\"o1\"}",
            SyncOperation.Insert,
            "{}",
            _originId,
            ""
        );

        Assert.True(SubscriptionManager.MatchesChange(sub, change1));
        Assert.True(SubscriptionManager.MatchesChange(sub, change2));
        Assert.False(SubscriptionManager.MatchesChange(sub, wrongTable));
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
        Assert.IsType<BoolSyncOk>(insertResult);

        // GetById
        var getResult = SubscriptionRepository.GetById(_db, sub.SubscriptionId);
        Assert.IsType<SubscriptionOk>(getResult);
        var retrieved = ((SubscriptionOk)getResult).Value;
        Assert.NotNull(retrieved);
        Assert.Equal(sub.TableName, retrieved!.TableName);

        // GetByTable
        var byTableResult = SubscriptionRepository.GetByTable(_db, "Person");
        Assert.IsType<SubscriptionListOk>(byTableResult);
        Assert.Contains(
            ((SubscriptionListOk)byTableResult).Value,
            s => s.SubscriptionId == sub.SubscriptionId
        );

        // Delete
        var deleteResult = SubscriptionRepository.Delete(_db, sub.SubscriptionId);
        Assert.IsType<BoolSyncOk>(deleteResult);

        // Verify deleted
        var afterDelete = SubscriptionRepository.GetById(_db, sub.SubscriptionId);
        Assert.IsType<SubscriptionOk>(afterDelete);
        Assert.Null(((SubscriptionOk)afterDelete).Value);
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

        Assert.IsType<BatchApplyResultOk>(result);
        var applied = ((BatchApplyResultOk)result).Value;
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

        var batch = BatchManager.FetchBatch(
            0,
            10,
            (from, size) => SyncLogRepository.FetchChanges(_db, from, size),
            NullLogger.Instance
        );

        Assert.IsType<SyncBatchOk>(batch);
        var batchData = ((SyncBatchOk)batch).Value;
        Assert.Equal(10, batchData.Changes.Count);
        Assert.True(batchData.HasMore);
    }

    [Fact]
    public void Spec_S12_Batching_HasMoreFalseWhenExhausted()
    {
        // HasMore should be false when all changes fetched
        InsertPerson("p1", "Alice", "alice@example.com");

        var batch = BatchManager.FetchBatch(
            0,
            100,
            (from, size) => SyncLogRepository.FetchChanges(_db, from, size),
            NullLogger.Instance
        );

        Assert.IsType<SyncBatchOk>(batch);
        var batchData = ((SyncBatchOk)batch).Value;
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
        Assert.IsType<BoolSyncOk>(upsertResult);

        // GetByOrigin
        var getResult = SyncClientRepository.GetByOrigin(_db, client.OriginId);
        Assert.IsType<SyncClientOk>(getResult);
        var retrieved = ((SyncClientOk)getResult).Value;
        Assert.NotNull(retrieved);
        Assert.Equal(100, retrieved!.LastSyncVersion);

        // Upsert (update)
        var updated = client with
        {
            LastSyncVersion = 200,
        };
        SyncClientRepository.Upsert(_db, updated);

        var afterUpdate = SyncClientRepository.GetByOrigin(_db, client.OriginId);
        Assert.Equal(200, ((SyncClientOk)afterUpdate).Value!.LastSyncVersion);

        // Delete
        var deleteResult = SyncClientRepository.Delete(_db, client.OriginId);
        Assert.IsType<BoolSyncOk>(deleteResult);

        var afterDelete = SyncClientRepository.GetByOrigin(_db, client.OriginId);
        Assert.Null(((SyncClientOk)afterDelete).Value);
    }

    #endregion

    #region Section 14: Conflict Resolution

    [Fact]
    public void Spec_S14_ConflictDetection_SameTablePkDifferentOrigin()
    {
        // Spec S14.1: Conflict when same table+PK, different origin
        var local = new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Update,
            "{}",
            "origin-A",
            "2025-01-01T00:00:00.000Z"
        );
        var remote = new SyncLogEntry(
            2,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Update,
            "{}",
            "origin-B",
            "2025-01-01T00:00:01.000Z"
        );
        var noConflict = new SyncLogEntry(
            3,
            "Person",
            "{\"Id\":\"p2\"}",
            SyncOperation.Update,
            "{}",
            "origin-B",
            "2025-01-01T00:00:01.000Z"
        );

        Assert.True(ConflictResolver.IsConflict(local, remote));
        Assert.False(ConflictResolver.IsConflict(local, noConflict)); // Different PK
    }

    [Fact]
    public void Spec_S14_LastWriteWins_HigherTimestampWins()
    {
        // Spec S14.2: LWW - highest timestamp wins
        var older = new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Update,
            "{\"Name\":\"Old\"}",
            "origin-A",
            "2025-01-01T00:00:00.000Z"
        );
        var newer = new SyncLogEntry(
            2,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Update,
            "{\"Name\":\"New\"}",
            "origin-B",
            "2025-01-01T00:00:01.000Z"
        );

        var resolution = ConflictResolver.ResolveLastWriteWins(older, newer);
        Assert.Equal(newer, resolution.Winner);
        Assert.Equal(ConflictStrategy.LastWriteWins, resolution.Strategy);
    }

    [Fact]
    public void Spec_S14_ServerWins_AlwaysChoosesRemote()
    {
        var local = new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Update,
            "{}",
            "origin-A",
            "2025-01-01T00:00:01.000Z"
        );
        var remote = new SyncLogEntry(
            2,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Update,
            "{}",
            "origin-B",
            "2025-01-01T00:00:00.000Z"
        );

        var resolution = ConflictResolver.Resolve(local, remote, ConflictStrategy.ServerWins);
        Assert.Equal(remote, resolution.Winner);
    }

    [Fact]
    public void Spec_S14_ClientWins_AlwaysChoosesLocal()
    {
        var local = new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Update,
            "{}",
            "origin-A",
            "2025-01-01T00:00:00.000Z"
        );
        var remote = new SyncLogEntry(
            2,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Update,
            "{}",
            "origin-B",
            "2025-01-01T00:00:01.000Z"
        );

        var resolution = ConflictResolver.Resolve(local, remote, ConflictStrategy.ClientWins);
        Assert.Equal(local, resolution.Winner);
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
        var hash2 = HashVerifier.ComputeBatchHash(changes);

        // Same input = same hash
        Assert.Equal(hash1, hash2);

        // Hash is 64 hex chars (SHA-256)
        Assert.Equal(64, hash1.Length);
        Assert.True(hash1.All(char.IsLetterOrDigit));
    }

    [Fact]
    public void Spec_S15_BatchHash_IncludedInBatch()
    {
        // Batches now include hash for verification
        InsertPerson("p1", "Alice", "alice@example.com");

        var batch = BatchManager.FetchBatch(
            0,
            100,
            (from, size) => SyncLogRepository.FetchChanges(_db, from, size),
            NullLogger.Instance
        );

        Assert.IsType<SyncBatchOk>(batch);
        var batchData = ((SyncBatchOk)batch).Value;
        Assert.NotNull(batchData.Hash);

        // Verify the hash matches
        var verifyResult = BatchManager.VerifyBatchHash(batchData, NullLogger.Instance);
        Assert.IsType<BoolSyncOk>(verifyResult);
        Assert.True(((BoolSyncOk)verifyResult).Value);
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
        var data = new Dictionary<string, object?>
        {
            ["Zebra"] = "last",
            ["Alpha"] = "first",
            ["Middle"] = "middle",
        };

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

        Assert.Contains("_sync_state", tables);
        Assert.Contains("_sync_session", tables);
        Assert.Contains("_sync_log", tables);
        Assert.Contains("_sync_clients", tables);
        Assert.Contains("_sync_subscriptions", tables);
    }

    [Fact]
    public void Spec_AppendixA_SyncLog_HasRequiredColumns()
    {
        var columns = GetTableColumns("_sync_log");

        Assert.Contains("version", columns);
        Assert.Contains("table_name", columns);
        Assert.Contains("pk_value", columns);
        Assert.Contains("operation", columns);
        Assert.Contains("payload", columns);
        Assert.Contains("origin", columns);
        Assert.Contains("timestamp", columns);
    }

    [Fact]
    public void Spec_AppendixA_SyncState_InitializedCorrectly()
    {
        // Spec: origin_id, last_server_version, last_push_version initialized
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT key FROM _sync_state";
        var keys = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            keys.Add(reader.GetString(0));
        }

        Assert.Contains("origin_id", keys);
        Assert.Contains("last_server_version", keys);
        Assert.Contains("last_push_version", keys);
    }

    #endregion

    #region Helper Methods

    private void CreateTestTable()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Person (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Email TEXT NOT NULL
            );

            CREATE TRIGGER Person_sync_insert
            AFTER INSERT ON Person
            WHEN (SELECT sync_active FROM _sync_session) = 0
            BEGIN
                INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
                VALUES (
                    'Person',
                    json_object('Id', NEW.Id),
                    'insert',
                    json_object('Id', NEW.Id, 'Name', NEW.Name, 'Email', NEW.Email),
                    (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                    strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
                );
            END;

            CREATE TRIGGER Person_sync_update
            AFTER UPDATE ON Person
            WHEN (SELECT sync_active FROM _sync_session) = 0
            BEGIN
                INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
                VALUES (
                    'Person',
                    json_object('Id', NEW.Id),
                    'update',
                    json_object('Id', NEW.Id, 'Name', NEW.Name, 'Email', NEW.Email),
                    (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                    strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
                );
            END;

            CREATE TRIGGER Person_sync_delete
            AFTER DELETE ON Person
            WHEN (SELECT sync_active FROM _sync_session) = 0
            BEGIN
                INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
                VALUES (
                    'Person',
                    json_object('Id', OLD.Id),
                    'delete',
                    NULL,
                    (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                    strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
                );
            END;
            """;
        cmd.ExecuteNonQuery();
    }

    private void InsertPerson(string id, string name, string email)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "INSERT INTO Person (Id, Name, Email) VALUES (@id, @name, @email)";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@email", email);
        cmd.ExecuteNonQuery();
    }

    private void UpdatePerson(string id, string name, string email)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "UPDATE Person SET Name = @name, Email = @email WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@email", email);
        cmd.ExecuteNonQuery();
    }

    private void DeletePerson(string id)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM Person WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    private void SeedInsertUpdateDeleteP1()
    {
        InsertPerson("p1", "Alice", "alice@example.com");
        UpdatePerson("p1", "Alice Updated", "alice2@example.com");
        DeletePerson("p1");
    }

    private List<SyncLogEntry> FetchChanges(long fromVersion)
    {
        var result = SyncLogRepository.FetchChanges(_db, fromVersion, 1000);
        Assert.IsType<SyncLogListOk>(result);
        return [.. ((SyncLogListOk)result).Value];
    }

    private BoolSyncResult ApplySingleChange(SyncLogEntry entry) =>
        ChangeApplierSQLite.ApplyChange(_db, entry);

    private List<string> GetTables()
    {
        var tables = new List<string>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }
        return tables;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "PRAGMA with hardcoded test table names"
    )]
    private List<string> GetTableColumns(string tableName)
    {
        var columns = new List<string>();
        using var cmd = _db.CreateCommand();
#pragma warning disable CA2100 // PRAGMA table_info is safe
        cmd.CommandText = $"PRAGMA table_info({tableName})";
#pragma warning restore CA2100
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }
        return columns;
    }

    #endregion

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch
            {
                /* File may be locked */
            }
        }
    }
}
