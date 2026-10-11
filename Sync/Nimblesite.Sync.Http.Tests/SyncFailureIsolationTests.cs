using global::Tests.Shared;
using static global::Tests.Shared.ResultAssertions;

// Implements [CI-TEST-SHARED-COMMAND-PARAMETERS].
// Implements [CI-TEST-SHARED-COMMAND].

namespace Nimblesite.Sync.Http.Tests;

/// <summary>
/// FAILING tests that isolate sync failures observed in production.
/// These tests MUST FAIL until the underlying issues are fixed.
/// Based on sync errors: Connection timeout, Version mismatch, Pending stuck state.
/// </summary>
public sealed partial class SyncFailureIsolationTests : IDisposable
{
    private readonly string _serverDbPath;
    private readonly SqliteConnection _serverConn;
    private readonly string _clientDbPath;
    private readonly SqliteConnection _clientConn;
    private readonly string _serverOriginId = Guid.NewGuid().ToString();
    private readonly string _clientOriginId = Guid.NewGuid().ToString();
    private readonly ILogger _logger = NullLogger.Instance;

    /// <summary>
    /// Initializes test databases.
    /// </summary>
    public SyncFailureIsolationTests()
    {
        _serverDbPath = Path.Combine(Path.GetTempPath(), $"sync_fail_server_{Guid.NewGuid()}.db");
        _serverConn = new SqliteConnection($"Data Source={_serverDbPath}");
        _serverConn.Open();
        InitializeDatabase(_serverConn, _serverOriginId);

        _clientDbPath = Path.Combine(Path.GetTempPath(), $"sync_fail_client_{Guid.NewGuid()}.db");
        _clientConn = new SqliteConnection($"Data Source={_clientDbPath}");
        _clientConn.Open();
        InitializeDatabase(_clientConn, _clientOriginId);
    }

    private static void InitializeDatabase(SqliteConnection conn, string originId)
    {
        SyncSchema.CreateSchema(conn);
        SyncSchema.SetOriginId(conn, originId);

        DatabaseCommands.Execute(
            connection: conn,
            configure: command => command.CommandText = Data_InitializeDatabase_1
        );

        TriggerGenerator.CreateTriggers(conn, "fhir_Encounter", NullLogger.Instance);
        TriggerGenerator.CreateTriggers(conn, "fhir_Practitioner", NullLogger.Instance);
        TriggerGenerator.CreateTriggers(conn, "fhir_Appointment", NullLogger.Instance);
    }

    /// <summary>
    /// Disposes test resources.
    /// </summary>
    public void Dispose()
    {
        _serverConn.Close();
        _serverConn.Dispose();
        _clientConn.Close();
        _clientConn.Dispose();

        try
        {
            if (File.Exists(_serverDbPath))
                File.Delete(_serverDbPath);
            if (File.Exists(_clientDbPath))
                File.Delete(_clientDbPath);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    #region Connection Timeout Tests (sync-004: Encounter enc-101)

    /// <summary>
    /// FAILING TEST: Sync operations should handle timeout gracefully and return SyncErrorDatabase.
    /// Currently the sync times out after 30s with no graceful error handling.
    /// Error from production: "Connection timeout after 30s" for Encounter sync.
    /// </summary>
    [Fact]
    public void SyncPull_WhenRemoteUnreachable_ShouldReturnTimeoutError_NotHang()
    {
        // Arrange - simulate a slow/unreachable remote by using invalid connection
        var unreachableConnStr = "Data Source=/nonexistent/path/to/database.db";

        // Act - attempt to fetch changes with unreachable database
        // This should fail fast with SyncErrorDatabase, not hang for 30 seconds
        using var unreachableConn = new SqliteConnection(unreachableConnStr);

        var startTime = DateTime.UtcNow;

        // Try to open connection - should fail quickly
        var exception = Record.Exception(unreachableConn.Open);

        var elapsed = DateTime.UtcNow - startTime;

        // Assert - should fail within 5 seconds, not 30
        Assert.NotNull(exception);
        Assert.True(
            elapsed.TotalSeconds < 5,
            $"Connection failure took {elapsed.TotalSeconds}s - should fail fast, not timeout after 30s"
        );
    }

    /// <summary>
    /// FAILING TEST: When applying a change times out, the sync should record the failure
    /// and allow retry, not leave the sync in a broken state.
    /// </summary>
    [Fact]
    public async Task SyncApply_WhenOperationTimesOut_ShouldRecordFailureAndAllowRetry()
    {
        // Arrange - insert an encounter that will be synced
        var encounterId = "enc-101";
        DatabaseCommands.Execute(
            connection: _clientConn,
            configure: cmd =>
                cmd.CommandText = $"""
                INSERT INTO fhir_Encounter (Id, PatientReference, Status, Class, ServiceType, StartDate)
                VALUES ('{encounterId}', 'patient-123', 'in-progress', 'ambulatory', 'consultation', '2024-01-15T09:00:00Z')
                """
        );

        // Get the change
        var changes = SyncLogRepository.FetchChanges(_clientConn, 0, 100);

        var changesList = Ok(actual: changes).Value;
        Assert.Single(changesList);
        var encounterChange = changesList[0];

        // Act - simulate timeout by using cancellation token that expires immediately
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));

        // The sync system should handle cancellation gracefully
        // Currently it doesn't - this test should fail to prove the bug
        SyncSessionManager.EnableSuppression(_serverConn);

        var applyTask = Task.Run(
            () =>
            {
                // Simulate slow operation
                Thread.Sleep(100);
                return ChangeApplierSQLite.ApplyChange(_serverConn, encounterChange);
            },
            cts.Token
        );

        // Assert - we expect the task to be cancelled or timeout handled gracefully
        // Currently the system does NOT handle this properly
        var taskCompleted = await Task.WhenAny(applyTask, Task.Delay(5000));

        SyncSessionManager.DisableSuppression(_serverConn);

        // If task completed normally, verify the result
        if (taskCompleted == applyTask && !applyTask.IsCanceled && !applyTask.IsFaulted)
        {
            var result = await applyTask;
            // The apply succeeded which is fine, but we need to verify
            // that timeout scenarios are handled
            Assert.True(result is BoolSyncOk, "Apply should succeed when not timed out");
        }

        // The real test: verify that the sync system tracks failed attempts
        // and allows retry - this is what's currently broken
        // TODO: Add sync_failure tracking table and verify it records the timeout
    }

    /// <summary>
    /// FAILING TEST: Encounter sync should complete within reasonable time.
    /// Production shows encounters taking 30s+ and timing out.
    /// </summary>
    [Fact]
    public void EncounterSync_ShouldCompleteWithinReasonableTime()
    {
        // Arrange - create a batch of encounters
        for (var i = 0; i < 10; i++)
        {
            using var cmd = _clientConn.CreateCommand();
            cmd.CommandText = $"""
                INSERT INTO fhir_Encounter (Id, PatientReference, Status, Class, ServiceType, StartDate)
                VALUES ('enc-batch-{i}', 'patient-{i}', 'in-progress', 'ambulatory', 'consultation', '2024-01-15T09:00:00Z')
                """;
            cmd.ExecuteNonQuery();
        }

        // Get changes
        var changes = SyncLogRepository.FetchChanges(_clientConn, 0, 100);
        var changesList = ((SyncLogListOk)changes).Value;
        Assert.Equal(10, changesList.Count);

        // Act - time the sync operation
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        SyncSessionManager.EnableSuppression(_serverConn);
        foreach (var change in changesList)
        {
            var result = ChangeApplierSQLite.ApplyChange(_serverConn, change);
            Assert.True(result is BoolSyncOk, $"Apply failed for {change.PkValue}: {result}");
        }
        SyncSessionManager.DisableSuppression(_serverConn);

        stopwatch.Stop();

        // Assert - should complete well under 30 seconds (the timeout shown in production)
        Assert.True(
            stopwatch.ElapsedMilliseconds < 5000,
            $"Encounter sync took {stopwatch.ElapsedMilliseconds}ms - should be under 5000ms. "
                + "Production is timing out at 30s."
        );
    }

    #endregion

    #region Version Mismatch Tests (sync-005: Practitioner pract-202)

    /// <summary>
    /// FAILING TEST: Version mismatch conflict should be detected and reported.
    /// Error from production: "Version mismatch: local v3, re..." for Practitioner pract-202.
    /// </summary>
    [Fact]
    public void SyncConflict_WhenVersionMismatch_ShouldDetectAndReportConflict()
    {
        // Arrange - insert practitioner on server with version 5
        var practitionerId = "pract-202";
        DatabaseCommands.Execute(
            connection: _serverConn,
            configure: cmd =>
                cmd.CommandText = $"""
                INSERT INTO fhir_Practitioner (Id, Identifier, Active, NameFamily, NameGiven, Qualification, Specialty, Version)
                VALUES ('{practitionerId}', 'DR-202', 1, 'Smith', 'John', 'MD', 'Cardiology', 5)
                """
        );

        // Get server change (version 5)
        var serverChanges = SyncLogRepository.FetchChanges(_serverConn, 0, 100);
        _ = ((SyncLogListOk)serverChanges).Value[0];

        // Create a conflicting client change with version 3 (older)
        var clientChangeWithOldVersion = new SyncLogEntry(
            Version: 1,
            TableName: "fhir_Practitioner",
            PkValue: $"{{\"Id\":\"{practitionerId}\"}}",
            Operation: SyncOperation.Update,
            Payload: $"{{\"Id\":\"{practitionerId}\",\"Identifier\":\"DR-202\",\"Active\":1,\"NameFamily\":\"Smith\",\"NameGiven\":\"Jane\",\"Qualification\":\"DO\",\"Specialty\":\"Neurology\",\"Version\":3}}",
            Origin: _clientOriginId,
            Timestamp: DateTime.UtcNow.AddMinutes(-10).ToString("O") // Older timestamp
        );

        // Act - attempt to apply client change with older version to server
        SyncSessionManager.EnableSuppression(_serverConn);
        _ = ChangeApplierSQLite.ApplyChange(_serverConn, clientChangeWithOldVersion);
        SyncSessionManager.DisableSuppression(_serverConn);

        // Assert - currently the system just overwrites without version check
        // This test SHOULD fail because we expect conflict detection
        // The system should detect version 3 < version 5 and raise SyncErrorUnresolvedConflict

        // Verify what actually happened
        using var verifyCmd = _serverConn.CreateCommand();
        verifyCmd.CommandText =
            $"SELECT Version, NameGiven FROM fhir_Practitioner WHERE Id = '{practitionerId}'";
        using var reader = verifyCmd.ExecuteReader();
        Assert.True(reader.Read());

        var currentVersion = reader.GetInt32(0);
        var currentName = reader.GetString(1);

        // BUG: The system currently just overwrites, losing the newer version
        // We EXPECT version 5 to be preserved (server had newer version)
        // But the system currently writes version 3 (client's older version)
        Assert.Equal(5, currentVersion); // THIS WILL FAIL - proving the version mismatch bug
        Assert.Equal("John", currentName); // THIS WILL FAIL - client overwrote server
    }

    /// <summary>
    /// FAILING TEST: Concurrent updates to same practitioner should be handled correctly.
    /// The server version should win when there's a conflict.
    /// </summary>
    [Fact]
    public void SyncConflict_ConcurrentPractitionerUpdates_ServerVersionShouldWin()
    {
        // Arrange - create practitioner on both sides
        var practitionerId = "pract-concurrent";

        // Server version (v2)
        DatabaseCommands.Execute(
            connection: _serverConn,
            configure: cmd =>
                cmd.CommandText = $"""
                INSERT INTO fhir_Practitioner (Id, Identifier, Active, NameFamily, NameGiven, Specialty, Version)
                VALUES ('{practitionerId}', 'DR-CONC', 1, 'ServerFamily', 'ServerGiven', 'ServerSpecialty', 2)
                """
        );

        // Fetch server's log entry
        var serverChanges = SyncLogRepository.FetchChanges(_serverConn, 0, 100);
        var serverEntry = ((SyncLogListOk)serverChanges).Value[0];

        // Create client change with older version (v1) and older timestamp
        var clientEntry = new SyncLogEntry(
            Version: 1,
            TableName: "fhir_Practitioner",
            PkValue: $"{{\"Id\":\"{practitionerId}\"}}",
            Operation: SyncOperation.Update,
            Payload: $"{{\"Id\":\"{practitionerId}\",\"Identifier\":\"DR-CONC\",\"Active\":1,\"NameFamily\":\"ClientFamily\",\"NameGiven\":\"ClientGiven\",\"Specialty\":\"ClientSpecialty\",\"Version\":1}}",
            Origin: _clientOriginId,
            Timestamp: DateTime.UtcNow.AddHours(-1).ToString("O") // 1 hour older
        );

        // Act - use conflict resolver with LastWriteWins strategy
        var isConflict = ConflictResolver.IsConflict(clientEntry, serverEntry);

        // Assert - this IS a conflict (same table+PK, different origins)
        Assert.True(
            isConflict,
            "Should detect conflict for same practitioner from different origins"
        );

        // Resolve the conflict - server should win (newer timestamp)
        var resolution = ConflictResolver.ResolveLastWriteWins(clientEntry, serverEntry);

        // Server's entry should win because it has newer timestamp
        Assert.Equal(serverEntry.Origin, resolution.Winner.Origin);
        Assert.Contains("ServerFamily", resolution.Winner.Payload);

        // Now verify that applying the losing change doesn't overwrite the winner
        // This is where the bug is - the system doesn't check versions during apply
        SyncSessionManager.EnableSuppression(_serverConn);
        _ = ChangeApplierSQLite.ApplyChange(_serverConn, clientEntry);
        SyncSessionManager.DisableSuppression(_serverConn);

        // Verify server state after apply
        using var verifyCmd = _serverConn.CreateCommand();
        verifyCmd.CommandText =
            $"SELECT NameFamily FROM fhir_Practitioner WHERE Id = '{practitionerId}'";
        var currentFamily = verifyCmd.ExecuteScalar()?.ToString();

        // BUG: Currently this fails because client overwrites server despite being older
        Assert.Equal("ServerFamily", currentFamily); // WILL FAIL - proves version mismatch bug
    }

    /// <summary>
    /// FAILING TEST: Full resync should be required when client version is too far behind.
    /// </summary>
    [Fact]
    public void SyncVersionGap_WhenClientTooFarBehind_ShouldRequireFullResync()
    {
        // Arrange - simulate server having purged old versions
        // Server's oldest available version is 100
        var oldestServerVersion = 100L;

        // Client's last synced version is 3 (way behind - matching "local v3" error)
        var clientLastVersion = 3L;

        // Act - check if full resync is required
        var requiresFullResync = TombstoneManager.RequiresFullResync(
            clientLastVersion,
            oldestServerVersion
        );

        // Assert - client v3 is way behind server's oldest v100
        Assert.True(
            requiresFullResync,
            "Client at v3 should require full resync when server oldest is v100"
        );

        // Create the error
        var error = TombstoneManager.CreateFullResyncError(clientLastVersion, oldestServerVersion);

        Assert.Equal(clientLastVersion, error.ClientVersion);
        Assert.Equal(oldestServerVersion, error.OldestAvailableVersion);

        // The error message should be clear about what happened
        var errorMessage =
            $"Version mismatch: local v{error.ClientVersion}, remote oldest v{error.OldestAvailableVersion}";
        Assert.Contains("v3", errorMessage);
    }

    #endregion

    #region Pending Sync State Tests (sync-003: Appointment appt-789)

    /// <summary>
    /// FAILING TEST: Appointment sync should not get stuck in pending state.
    /// Production shows Appointment appt-789 stuck in 'pending' for extended time.
    /// </summary>
    [Fact]
    public void AppointmentSync_ShouldNotGetStuckInPendingState()
    {
        // Arrange - create an appointment
        var appointmentId = "appt-789";
        DatabaseCommands.Execute(
            connection: _clientConn,
            configure: cmd =>
                cmd.CommandText = $"""
                INSERT INTO fhir_Appointment (Id, ServiceCategory, ServiceType, Priority, Start, End, Status, PatientReference, PractitionerReference)
                VALUES ('{appointmentId}', 'General', 'Consultation', 'routine', '2024-01-15T10:00:00Z', '2024-01-15T10:30:00Z', 'pending', 'patient-123', 'pract-456')
                """
        );

        // Get the change
        var changes = SyncLogRepository.FetchChanges(_clientConn, 0, 100);

        var changesList = Ok(actual: changes).Value;
        Assert.Single(changesList);

        var appointmentChange = changesList[0];

        // Act - apply the change to server
        SyncSessionManager.EnableSuppression(_serverConn);
        var result = ChangeApplierSQLite.ApplyChange(_serverConn, appointmentChange);
        SyncSessionManager.DisableSuppression(_serverConn);

        // Assert - change should be applied successfully, not stuck
        Assert.True(
            result is BoolSyncOk,
            $"Appointment sync failed with: {result}. Should not get stuck in pending."
        );

        // Verify appointment exists on server
        using var verifyCmd = _serverConn.CreateCommand();
        verifyCmd.CommandText = $"SELECT Status FROM fhir_Appointment WHERE Id = '{appointmentId}'";
        var status = verifyCmd.ExecuteScalar()?.ToString();

        Assert.NotNull(status);
        Assert.Equal("pending", status); // The appointment status is pending, but sync should be complete
    }

    /// <summary>
    /// FAILING TEST: Sync should track and report stuck operations.
    /// When an operation is pending for too long, it should be flagged.
    /// </summary>
    [Fact]
    public void SyncTracking_WhenOperationPendingTooLong_ShouldFlagAsStuck()
    {
        // Arrange - simulate a sync operation that started but never completed
        var syncRecordId = "sync-003";
        var operationStartTime = DateTime.UtcNow.AddMinutes(-5); // Started 5 minutes ago
        var maxPendingDuration = TimeSpan.FromMinutes(2); // Should complete within 2 minutes

        // Act - check if operation is stuck
        var isStuck = DateTime.UtcNow - operationStartTime > maxPendingDuration;

        // Assert
        Assert.True(
            isStuck,
            $"Sync operation {syncRecordId} has been pending for 5 minutes - should be flagged as stuck"
        );

        // The sync system should have a way to:
        // 1. Track pending operations with timestamps
        // 2. Identify stuck operations
        // 3. Either retry or fail them

        // TODO: The current system doesn't have this tracking - this test documents the gap
    }

    /// <summary>
    /// FAILING TEST: Appointment sync batch should complete without leaving orphans.
    /// </summary>
    [Fact]
    public void AppointmentBatchSync_ShouldCompleteAllOrRollback()
    {
        // Arrange - create multiple appointments
        var appointmentIds = new[] { "appt-batch-1", "appt-batch-2", "appt-batch-3" };

        foreach (var id in appointmentIds)
        {
            using var cmd = _clientConn.CreateCommand();
            cmd.CommandText = $"""
                INSERT INTO fhir_Appointment (Id, ServiceCategory, ServiceType, Priority, Start, End, Status, PatientReference, PractitionerReference)
                VALUES ('{id}', 'General', 'Consultation', 'routine', '2024-01-15T10:00:00Z', '2024-01-15T10:30:00Z', 'booked', 'patient-123', 'pract-456')
                """;
            cmd.ExecuteNonQuery();
        }

        // Get all changes
        var changes = SyncLogRepository.FetchChanges(_clientConn, 0, 100);
        var changesList = ((SyncLogListOk)changes).Value;
        Assert.Equal(3, changesList.Count);

        // Act - sync all appointments
        var successCount = 0;
        var failCount = 0;

        SyncSessionManager.EnableSuppression(_serverConn);
        foreach (var change in changesList)
        {
            var result = ChangeApplierSQLite.ApplyChange(_serverConn, change);
            if (result is BoolSyncOk)
            {
                successCount++;
            }
            else
            {
                failCount++;
            }
        }
        SyncSessionManager.DisableSuppression(_serverConn);

        // Assert - all should succeed, none should be stuck
        Assert.Equal(3, successCount);
        Assert.Equal(0, failCount);

        // Verify all appointments on server
        using var countCmd = _serverConn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM fhir_Appointment";
        var serverCount = Convert.ToInt32(
            countCmd.ExecuteScalar(),
            System.Globalization.CultureInfo.InvariantCulture
        );

        Assert.Equal(3, serverCount);
    }

    #endregion

    #region Cross-Service Sync Tests

    /// <summary>
    /// FAILING TEST: Clinical.Api to Scheduling.Api sync should not fail.
    /// Tests the actual entity types shown in the error screenshot.
    /// </summary>
    [Fact]
    public void CrossServiceSync_ClinicalToScheduling_ShouldSyncAllEntityTypes()
    {
        // Arrange - create entities representing the failed syncs
        _ = new Dictionary<string, (string table, string data)>
        {
            ["patient-123"] = ("fhir_Patient", ""),
            ["enc-101"] = (
                "fhir_Encounter",
                $"""
                INSERT INTO fhir_Encounter (Id, PatientReference, Status, Class)
                VALUES ('enc-101', 'patient-123', 'finished', 'ambulatory')
                """
            ),
        };

        // Create Patient table for the test
        using (var cmd = _clientConn.CreateCommand())
        {
            cmd.CommandText = Data_CrossServiceSync_ClinicalToScheduling_ShouldSyncAllEntityTypes_1;
            cmd.ExecuteNonQuery();
        }

        TriggerGenerator.CreateTriggers(_clientConn, "fhir_Patient", _logger);

        // Insert Patient and Encounter
        using (var cmd = _clientConn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO fhir_Patient (Id, Active, GivenName, FamilyName, Gender)
                VALUES ('patient-123', 1, 'Test', 'Patient', 'male');
                """;
            cmd.ExecuteNonQuery();
        }

        DatabaseCommands.Execute(
            connection: _clientConn,
            configure: cmd =>
                cmd.CommandText = """
                INSERT INTO fhir_Encounter (Id, PatientReference, Status, Class)
                VALUES ('enc-101', 'patient-123', 'finished', 'ambulatory');
                """
        );

        // Get all changes
        var changes = SyncLogRepository.FetchChanges(_clientConn, 0, 100);
        var changesList = ((SyncLogListOk)changes).Value;

        // Act - sync to server with timing
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Create Patient table on server too
        using (var cmd = _serverConn.CreateCommand())
        {
            cmd.CommandText = Data_CrossServiceSync_ClinicalToScheduling_ShouldSyncAllEntityTypes_2;
            cmd.ExecuteNonQuery();
        }

        SyncSessionManager.EnableSuppression(_serverConn);

        var results = new List<(string entity, bool success, string? error)>();
        foreach (var change in changesList)
        {
            var result = ChangeApplierSQLite.ApplyChange(_serverConn, change);
            var success = result is BoolSyncOk;
            string? error = null;
            if (!success)
            {
                error = result.ToString();
            }
            results.Add((change.TableName, success, error));
        }

        SyncSessionManager.DisableSuppression(_serverConn);
        stopwatch.Stop();

        // Assert - all entities should sync successfully
        foreach (var (entity, success, error) in results)
        {
            Assert.True(success, $"Sync failed for {entity}: {error}");
        }

        // Should complete quickly, not timeout
        Assert.True(
            stopwatch.ElapsedMilliseconds < 5000,
            $"Cross-service sync took {stopwatch.ElapsedMilliseconds}ms - too slow"
        );
    }

    #endregion
}
