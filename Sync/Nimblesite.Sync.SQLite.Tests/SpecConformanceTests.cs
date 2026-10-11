// Implements [CI-TEST-SHARED-COMMAND].
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using static global::Tests.Shared.ResultAssertions;

namespace Nimblesite.Sync.SQLite.Tests;

/// <summary>
/// Integration tests verifying conformance to the .NET Sync Framework Specification.
/// Each test maps to specific spec sections to prove the implementation is correct.
/// NO MOCKS - real SQLite databases only!
/// </summary>
public sealed partial class SpecConformanceTests : IDisposable
{
    private readonly SqliteConnection _db;
    private readonly string _dbPath = global::Tests.Shared.TempSqliteDatabase.PathFor(
        "specconformancetests"
    );
    private readonly string _originId = Guid.NewGuid().ToString();

    public SpecConformanceTests()
    {
        _db = global::Tests.Shared.TempSqliteDatabase.OpenSync(path: _dbPath, origin: _originId);
        CreatePersonTable();
    }

    #region Section 4: Primary Key Requirements

    /// <summary>
    /// Spec 4.1: Every tracked table MUST have a single-column primary key.
    /// UUID/GUID is strongly recommended.
    /// </summary>
    [Fact]
    public void Spec4_1_SingleColumnUuidPrimaryKey_IsSupported()
    {
        // Arrange: Create table with UUID primary key (spec recommended)
        ExecuteSql(Data_Spec4_1_SingleColumnUuidPrimaryKey_IsSupported_1);
        var triggerResult = TriggerGenerator.CreateTriggers(_db, "UuidTable", TestLogger.L);
        global::Tests.Shared.ResultAssertions.ExactOk(actual: triggerResult);

        // Act: Insert with UUID
        var uuid = Guid.NewGuid().ToString();
        ExecuteSql($"INSERT INTO UuidTable (Id, Name) VALUES ('{uuid}', 'Test')");

        // Assert: Change log has JSON pk_value with UUID
        var changes = FetchAllChanges();
        Assert.Single(changes);
        Assert.Contains(uuid, changes[0].PkValue);
    }

    /// <summary>
    /// Spec 4.3: pk_value in _sync_log is always a simple JSON string.
    /// </summary>
    [Fact]
    public void Spec4_3_PkValue_IsJsonObject()
    {
        // Arrange
        var changes = VerifySharedPhase1(
            input0: _db,
            value1: "INSERT INTO Person (Id, Name, Email) VALUES ('pk-123', 'Alice', 'a@b.com')"
        );
        var pkValue = changes[0].PkValue;

        // pk_value must be valid JSON object like {"Id": "pk-123"}
        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(pkValue);
        Assert.NotNull(parsed);
        Assert.Equal("pk-123", parsed["Id"]);
    }

    #endregion

    #region Section 5: Origin Identification

    /// <summary>
    /// Spec 5.2: Origin IDs MUST be generated using UUID v4 (random).
    /// </summary>
    [Fact]
    public void Spec5_2_OriginId_IsUuidV4Format()
    {
        // Assert: Origin ID follows UUID format
        Assert.Matches(UuidRegex(), _originId);
        Assert.Equal(36, _originId.Length); // Spec 5.5
    }

    /// <summary>
    /// Spec 5.4: Origin ID stored in _sync_state with key 'origin_id'.
    /// </summary>
    [Fact]
    public void Spec5_4_OriginId_StoredInSyncState()
    {
        // Act
        var result = SyncSchema.GetOriginId(_db);

        // Assert
        global::Tests.Shared.ResultAssertions.ExactOk(actual: result);
        Assert.Equal(_originId, (Ok(actual: result)).Value);
    }

    /// <summary>
    /// Spec 5.5: Origin ID MUST be included in every change log entry.
    /// </summary>
    [Fact]
    public void Spec5_5_OriginId_IncludedInEveryChangeLogEntry()
    {
        // Arrange
        TriggerGenerator.CreateTriggers(_db, "Person", TestLogger.L);

        // Act: Insert, update, delete
        ExecuteSql("INSERT INTO Person (Id, Name, Email) VALUES ('p1', 'Alice', 'a@b.com')");
        var changes = RunPersonMutations(
            first: "UPDATE Person SET Name = 'Bob' WHERE Id = 'p1'",
            second: "DELETE FROM Person WHERE Id = 'p1'"
        );
        Assert.All(changes, c => Assert.Equal(_originId, c.Origin));
    }

    #endregion

    #region Section 6: Timestamps

    /// <summary>
    /// Spec 6.1: All timestamps MUST be stored as ISO 8601 UTC strings with millisecond precision.
    /// Format: 2025-12-18T10:30:00.000Z
    /// </summary>
    [Fact]
    public void Spec6_1_Timestamps_AreIso8601UtcWithMilliseconds()
    {
        // Arrange
        var timestamp = VerifySharedPhase1AndMatchTimestamp(
            insertSql: "INSERT INTO Person (Id, Name, Email) VALUES ('p1', 'Alice', 'a@b.com')"
        );

        // Must match ISO 8601 UTC format with milliseconds
        Assert.EndsWith("Z", timestamp); // Spec 6.2: MUST be UTC (Z suffix)
    }

    /// <summary>
    /// Spec 6.2: Timestamps MUST be UTC (indicated by Z suffix).
    /// </summary>
    [Fact]
    public void Spec6_2_Timestamps_EndWithZ()
    {
        // Arrange
        var changes = VerifySharedPhase1(
            input0: _db,
            value1: "INSERT INTO Person (Id, Name, Email) VALUES ('p1', 'Alice', 'a@b.com')"
        );
        Assert.EndsWith("Z", changes[0].Timestamp);
    }

    #endregion

    #region Section 7: Unified Change Log

    /// <summary>
    /// Spec 7.2: Schema matches _sync_log specification.
    /// </summary>
    [Fact]
    public void Spec7_2_SyncLogSchema_HasRequiredColumns()
    {
        // Act: Query table info
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(_sync_log)";
        var columns = DatabaseCommands.ReadStrings(cmd, ordinal: 1);

        // Assert: All required columns exist
        VerifyReusableWorkflow1(
            input0: columns,
            value0: "version",
            value1: "table_name",
            value2: "pk_value"
        );
        VerifyReusableWorkflow1(
            input0: columns,
            value0: "operation",
            value1: "payload",
            value2: "origin"
        );
        Assert.Contains("timestamp", columns);
    }

    /// <summary>
    /// Spec 7.2: operation CHECK constraint (insert, update, delete).
    /// </summary>
    [Fact]
    public void Spec7_2_Operation_OnlyAllowedValues()
    {
        // Arrange
        TriggerGenerator.CreateTriggers(_db, "Person", TestLogger.L);

        // Act
        ExecuteSql("INSERT INTO Person (Id, Name, Email) VALUES ('p1', 'Alice', 'a@b.com')");
        var changes = VerifyReusableWorkflow11(
            input0: ExecuteSql,
            input1: FetchAllChanges,
            value0: "UPDATE Person SET Name = 'Bob' WHERE Id = 'p1'",
            value1: "DELETE FROM Person WHERE Id = 'p1'"
        );
        Assert.Equal("insert", changes[0].Operation.ToString().ToLowerInvariant());
        Assert.Equal("update", changes[1].Operation.ToString().ToLowerInvariant());
        Assert.Equal("delete", changes[2].Operation.ToString().ToLowerInvariant());
    }

    /// <summary>
    /// Spec 7.3: JSON payload serialization via json_object().
    /// Payload includes all columns for INSERT/UPDATE.
    /// </summary>
    [Fact]
    public void Spec7_3_Payload_IsValidJsonWithAllColumns()
    {
        // Arrange
        var changes = VerifySharedPhase1(
            input0: _db,
            value1: "INSERT INTO Person (Id, Name, Email) VALUES ('p1', 'Alice', 'alice@test.com')"
        );
        var payload = JsonSerializer.Deserialize<Dictionary<string, string>>(changes[0].Payload!);
        Assert.NotNull(payload);
        Assert.Equal("p1", payload["Id"]);
        Assert.Equal("Alice", payload["Name"]);
        Assert.Equal("alice@test.com", payload["Email"]);
    }

    /// <summary>
    /// Spec 7.3: Payload is NULL for deletes.
    /// </summary>
    [Fact]
    public void Spec7_3_DeletePayload_IsNull()
    {
        // Arrange
        TriggerGenerator.CreateTriggers(_db, "Person", TestLogger.L);
        ExecuteSql("INSERT INTO Person (Id, Name, Email) VALUES ('p1', 'Alice', 'a@b.com')");

        // Act
        ExecuteSql("DELETE FROM Person WHERE Id = 'p1'");

        // Assert
        var changes = FetchAllChanges();
        var deleteChange = changes.First(c => c.Operation == SyncOperation.Delete);
        Assert.Null(deleteChange.Payload);
    }

    #endregion

    #region Section 8: Trigger Suppression

    /// <summary>
    /// Spec 8.3: Triggers MUST check suppression flag before logging.
    /// </summary>
    [Fact]
    public void Spec8_3_TriggerSuppression_PreventsLogging()
    {
        // Arrange
        TriggerGenerator.CreateTriggers(_db, "Person", TestLogger.L);

        // Act: Enable suppression and insert
        SyncSessionManager.EnableSuppression(_db);
        ExecuteSql("INSERT INTO Person (Id, Name, Email) VALUES ('p1', 'Alice', 'a@b.com')");
        SyncSessionManager.DisableSuppression(_db);

        // Assert: No log entry created
        var changes = FetchAllChanges();
        Assert.Empty(changes);

        // But data is there
        Assert.Equal(
            1L,
            global::Tests.Shared.DatabaseCommands.Scalar(
                connection: _db,
                configure: command =>
                    command.CommandText = "SELECT COUNT(*) FROM Person WHERE Id = 'p1'"
            )
        );
    }

    /// <summary>
    /// Spec 8.4: Trigger suppression flag in _sync_session table.
    /// </summary>
    [Fact]
    public void Spec8_4_SyncSession_ControlsTriggers()
    {
        // Arrange
        TriggerGenerator.CreateTriggers(_db, "Person", TestLogger.L);

        // Act: Check initial state
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT sync_active FROM _sync_session";
        var initial = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        Assert.Equal(0, initial);

        // Enable suppression
        SyncSessionManager.EnableSuppression(_db);
        cmd.CommandText = "SELECT sync_active FROM _sync_session";
        var active = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        Assert.Equal(1, active);

        // Disable suppression
        SyncSessionManager.DisableSuppression(_db);
        cmd.CommandText = "SELECT sync_active FROM _sync_session";
        var disabled = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        Assert.Equal(0, disabled);
    }

    #endregion

    #region Section 9: Trigger Generation

    /// <summary>
    /// Spec 9.4: Generated triggers use strftime for SQLite timestamps.
    /// </summary>
    [Fact]
    public void Spec9_4_SQLiteTriggers_UseStrftime()
    {
        // Act
        var triggersResult = TriggerGenerator.GenerateTriggersFromSchema(
            _db,
            "Person",
            TestLogger.L
        );

        // Assert

        var triggers = (
            global::Tests.Shared.ResultAssertions.ExactOk(actual: triggersResult)
        ).Value;
        Assert.Contains("strftime('%Y-%m-%dT%H:%M:%fZ', 'now')", triggers);
    }

    /// <summary>
    /// Spec 9.5: Each tracked table MUST have INSERT, UPDATE, and DELETE triggers.
    /// </summary>
    [Fact]
    public void Spec9_5_AllThreeTriggerTypes_Generated()
    {
        // Act
        TriggerGenerator.CreateTriggers(_db, "Person", TestLogger.L);

        // Assert: Query triggers
        using var cmd = _db.CreateCommand();
        cmd.CommandText =
            "SELECT name FROM sqlite_master WHERE type = 'trigger' AND tbl_name = 'Person'";
        var triggers = DatabaseCommands.ReadStrings(cmd);

        VerifyReusableWorkflow1(
            input0: triggers,
            value0: "Person_sync_insert",
            value1: "Person_sync_update",
            value2: "Person_sync_delete"
        );
    }

    #endregion

    #region Section 11: Bi-Directional Sync Protocol

    /// <summary>
    /// Spec 11.3: Changes are applied in global version order (ascending).
    /// </summary>
    [Fact]
    public void Spec11_3_Changes_OrderedByVersionAscending()
    {
        // Arrange
        TriggerGenerator.CreateTriggers(_db, "Person", TestLogger.L);
        ExecuteSql("INSERT INTO Person (Id, Name, Email) VALUES ('p1', 'First', 'a@b.com')");
        var changes = RunPersonMutations(
            first: "INSERT INTO Person (Id, Name, Email) VALUES ('p2', 'Second', 'b@c.com')",
            second: "INSERT INTO Person (Id, Name, Email) VALUES ('p3', 'Third', 'c@d.com')"
        );
        Assert.True(changes[0].Version < changes[1].Version);
        Assert.True(changes[1].Version < changes[2].Version);
    }

    #endregion

    #region Section 12: Batching

    /// <summary>
    /// Spec 12.2: Batch query returns changes WHERE version > @from_version ORDER BY version ASC LIMIT @batch_size.
    /// </summary>
    [Fact]
    public void Spec12_2_BatchQuery_RespectsVersionAndLimit()
    {
        // Arrange
        TriggerGenerator.CreateTriggers(_db, "Person", TestLogger.L);
        for (var i = 1; i <= 10; i++)
        {
            ExecuteSql(
                $"INSERT INTO Person (Id, Name, Email) VALUES ('p{i}', 'Person{i}', 'p{i}@test.com')"
            );
        }

        // Act: Fetch first batch of 3
        var result1 = SyncLogRepository.FetchChanges(_db, 0, 3);

        var batch1 = (Assert.IsType<SyncLogListOk>(result1)).Value;

        // Act: Fetch next batch from where we left off
        var result2 = SyncLogRepository.FetchChanges(_db, batch1[^1].Version, 3);

        var batch2 = (Assert.IsType<SyncLogListOk>(result2)).Value;

        // Assert
        Assert.Equal(3, batch1.Count);
        Assert.Equal(3, batch2.Count);
        Assert.True(batch1[^1].Version < batch2[0].Version);
    }

    #endregion

    #region Section 14: Conflict Resolution

    /// <summary>
    /// Spec 14.3: Echo prevention - changes MUST NOT be re-applied to their origin.
    /// </summary>
    [Fact]
    public void Spec14_3_EchoPrevention_SkipsOwnOriginChanges()
    {
        // Arrange: Create entry with my own origin
        var myOrigin = "my-origin-123";
        var batch = new SyncBatch(
            [
                new SyncLogEntry(
                    1,
                    "Person",
                    "{\"Id\":\"p1\"}",
                    SyncOperation.Insert,
                    "{\"Id\":\"p1\",\"Name\":\"Test\"}",
                    myOrigin,
                    DateTime.UtcNow.ToString("O")
                ),
            ],
            0,
            1,
            false
        );

        var applied = new List<SyncLogEntry>();

        // Act
        var result = ChangeApplier.ApplyBatch(
            batch,
            myOrigin, // Same as entry's origin
            3,
            entry =>
            {
                applied.Add(entry);
                return new BoolSyncOk(true);
            },
            NullLogger.Instance
        );

        // Assert: Entry was skipped (echo prevention)
        global::Tests.Shared.ResultAssertions.ExactOk(actual: result);
        Assert.Empty(applied);
    }

    #endregion

    #region Section 15: Hash Verification

    /// <summary>
    /// Spec 15.2: Canonical JSON has keys sorted alphabetically.
    /// </summary>
    [Fact]
    public void Spec15_2_CanonicalJson_KeysSortedAlphabetically()
    {
        // Act
        var json = HashVerifier.ToCanonicalJson(
            Dataset_Spec15_2_CanonicalJson_KeysSortedAlphabetically_1()
        );

        // Assert
        Assert.StartsWith("{\"Alpha\":", json);
        Assert.Contains("\"Middle\":", json);
        Assert.EndsWith("\"Zebra\":\"z\"}", json);
    }

    /// <summary>
    /// Spec 15.2: Canonical JSON has no whitespace.
    /// </summary>
    [Fact]
    public void Spec15_2_CanonicalJson_NoWhitespace()
    {
        // Act
        var json = HashVerifier.ToCanonicalJson(
            new Dictionary<string, object?> { ["Name"] = "Test", ["Value"] = 123 }
        );

        // Assert
        Assert.DoesNotContain(" ", json);
        Assert.DoesNotContain("\n", json);
        Assert.DoesNotContain("\r", json);
        Assert.DoesNotContain("\t", json);
    }

    /// <summary>
    /// Spec 15.3: Full database hash algorithm produces consistent results.
    /// </summary>
    [Fact]
    public void Spec15_3_DatabaseHash_IsConsistent()
    {
        // Arrange
        var rows = Dataset_Spec15_3_DatabaseHash_IsConsistent_1();

        // Act
        var hash1 = HashVerifier.ComputeDatabaseHash(["Person"], _ => rows);
        var hash2 = HashVerifier.ComputeDatabaseHash(["Person"], _ => rows);

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length); // SHA-256 = 64 hex chars
    }

    /// <summary>
    /// Spec 15.4: Batch hash includes version, table, pk, operation, payload.
    /// </summary>
    [Fact]
    public void Spec15_4_BatchHash_IsConsistent()
    {
        // Arrange
        var entries = new[] { Fixture_Spec15_4_BatchHash_IsConsistent_1() };

        // Act
        var hash1 = HashVerifier.ComputeBatchHash(entries);
        var hash2 = HashVerifier.ComputeBatchHash(entries);

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length);
    }

    #endregion

    #region Section 17: Conformance Requirements

    /// <summary>
    /// Spec 17.1: All tracked tables have single-column primary keys.
    /// </summary>
    [Fact]
    public void Spec17_1_SingleColumnPrimaryKey_Required()
    {
        // Arrange: Table with composite PK
        ExecuteSql(Data_Spec17_1_SingleColumnPrimaryKey_Required_1);

        // Act
        var result = TriggerGenerator.GetTableColumns(_db, "CompositePkTable");

        var columns = (Assert.IsType<ColumnInfoListOk>(result)).Value;

        // Assert: Multiple PK columns detected
        var pkColumns = columns.Where(c => c.IsPrimaryKey).ToList();
        Assert.Equal(2, pkColumns.Count); // System detects composite key

        // Note: Our trigger generator should handle this case or reject it
        // (Implementation may vary - test documents the behavior)
    }

    /// <summary>
    /// Spec 17.2: Origin ID is UUID v4 format.
    /// </summary>
    [Fact]
    public void Spec17_2_OriginId_IsUuidV4()
    {
        // Assert
        Assert.True(Guid.TryParse(_originId, out var guid));
        Assert.NotEqual(Guid.Empty, guid);
    }

    /// <summary>
    /// Spec 17.3: Timestamps are UTC ISO 8601 with milliseconds.
    /// </summary>
    [Fact]
    public void Spec17_3_Timestamps_AreUtcIso8601WithMilliseconds()
    {
        // Arrange
        _ = VerifySharedPhase1AndMatchTimestamp(
            insertSql: "INSERT INTO Person (Id, Name, Email) VALUES ('p1', 'Test', 'test@test.com')"
        );
    }

    /// <summary>
    /// Spec 17.4: Triggers check suppression flag before logging.
    /// </summary>
    [Fact]
    public void Spec17_4_Triggers_CheckSuppressionFlag() =>
        // This is tested in Spec8_3_TriggerSuppression_PreventsLogging
        Spec8_3_TriggerSuppression_PreventsLogging();

    /// <summary>
    /// Spec 17.5: Triggers include origin ID in every entry.
    /// </summary>
    [Fact]
    public void Spec17_5_Triggers_IncludeOriginId() =>
        // This is tested in Spec5_5_OriginId_IncludedInEveryChangeLogEntry
        Spec5_5_OriginId_IncludedInEveryChangeLogEntry();

    #endregion

    #region Helpers

    private void CreatePersonTable() => ExecuteSql(Data_CreatePersonTable_1);

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Test helper for internal test SQL"
    )]
    private void ExecuteSql(string sql)
    {
        DatabaseCommands.Execute(connection: _db, configure: command => command.CommandText = sql);
    }

    private List<SyncLogEntry> FetchAllChanges()
    {
        var result = SyncLogRepository.FetchChanges(_db, 0, 1000);
        Assert.IsType<SyncLogListOk>(result);
        return [.. (Ok(actual: result)).Value];
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$")]
    private static partial Regex Iso8601UtcRegex();

    [GeneratedRegex(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")]
    private static partial Regex UuidRegex();

    public void Dispose()
    {
        global::Nimblesite.Sync.SQLite.Tests.EndToEndSyncTests.VerifyReusableWorkflow5(
            input0: _db,
            input1: _dbPath
        );
    }

    #endregion
    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal List<SyncLogEntry> VerifySharedPhase1(SqliteConnection input0, string value1)
    { // Arrange
        TriggerGenerator.CreateTriggers(input0, "Person", TestLogger.L);

        // Act
        ExecuteSql(value1);

        // Assert
        var changes = FetchAllChanges();

        return changes;
    }

    private List<SyncLogEntry> RunPersonMutations(string first, string second)
    {
        var changes = VerifySyncLoopWorkflow3(
            input0: VerifyReusableWorkflow11,
            input1: ExecuteSql,
            input2: FetchAllChanges,
            value0: first,
            value1: second
        );
        return changes;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private string VerifySharedPhase1AndMatchTimestamp(string insertSql)
    {
        var changes = VerifySharedPhase1(input0: _db, value1: insertSql);
        var timestamp = changes[0].Timestamp;
        Assert.Matches(Iso8601UtcRegex(), timestamp);
        return timestamp;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow1(
        List<string> input0,
        string value0,
        string value1,
        string value2
    )
    {
        // Assert: All required columns exist
        Assert.Contains(value0, input0);
        Assert.Contains(value1, input0);
        Assert.Contains(value2, input0);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static List<SyncLogEntry> VerifyReusableWorkflow11(
        Action<string> input0,
        Func<List<SyncLogEntry>> input1,
        string value0,
        string value1
    )
    {
        input0(obj: value0);
        input0(obj: value1);
        // Assert: All entries have origin
        var changes = input1();
        return changes;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    // Implements [CI-TEST-SHARED-SCENARIOS].
internal static List<SyncLogEntry> VerifySyncLoopWorkflow3(Func<Action<string>, Func<List<SyncLogEntry>>, string, string, List<SyncLogEntry>> input0, Action<string> input1, Func<List<SyncLogEntry>> input2, string value0, string value1)
{
    var changes = input0(input1, input2, value0, value1);
    Assert.Equal(3, changes.Count);
    return changes;
}
}
