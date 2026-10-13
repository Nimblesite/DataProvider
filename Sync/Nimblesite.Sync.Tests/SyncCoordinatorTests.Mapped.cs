// Implements [SYNC-MAPPED-SHARED-BATCHES].
using Nimblesite.Sync.SQLite;
using Tests.Shared;
using static global::Tests.Shared.ResultAssertions;
using static global::Tests.Shared.SyncFixtureSqliteHelpers;
using MappingStateOk = Outcome.Result<
    Nimblesite.Sync.Core.MappingStateEntry?,
    Nimblesite.Sync.Core.SyncError
>.Ok<Nimblesite.Sync.Core.MappingStateEntry?, Nimblesite.Sync.Core.SyncError>;
using PullResultResult = Outcome.Result<
    Nimblesite.Sync.Core.PullResult,
    Nimblesite.Sync.Core.SyncError
>;
using PushResultResult = Outcome.Result<
    Nimblesite.Sync.Core.PushResult,
    Nimblesite.Sync.Core.SyncError
>;
using RecordHashOk = Outcome.Result<
    Nimblesite.Sync.Core.RecordHashEntry?,
    Nimblesite.Sync.Core.SyncError
>.Ok<Nimblesite.Sync.Core.RecordHashEntry?, Nimblesite.Sync.Core.SyncError>;

namespace Nimblesite.Sync.Tests;

/// <summary>
/// Mapped pull and push between real SQLite replicas, with tracking state stored
/// by the SQLite mapping state repository.
/// </summary>
public sealed partial class SyncCoordinatorTests
{
    private const string PullMappingId = "person-pull";
    private const string PushMappingId = "person-push";

    /// <summary>Version tracking applies each change once and records the mapping state.</summary>
    [Fact]
    public void PullWithMapping_VersionTracking_AppliesOnceAndRecordsState()
    {
        CreateClientMappingTables();
        InsertPerson(db: _serverDb, id: "p1", name: "Alice");
        InsertPerson(db: _serverDb, id: "p2", name: "Bob");

        Assert.Equal(2, Ok(actual: MappedPull(origin: ClientOrigin)).Value.ChangesApplied);
        Assert.Equal(0, Ok(actual: MappedPull(origin: ClientOrigin)).Value.ChangesApplied);

        Assert.Equal("Alice", GetPersonName(_clientDb, "p1"));
        Assert.Equal("Bob", GetPersonName(_clientDb, "p2"));
        Assert.Equal(2, GetClientMappingState(mappingId: PullMappingId)?.RecordsSynced);
    }

    /// <summary>Hash tracking pushes a record once and skips it while its payload is unchanged.</summary>
    [Fact]
    public void PushWithMapping_HashTracking_SkipsUnchangedRecords()
    {
        CreateClientMappingTables();
        InsertPerson(db: _clientDb, id: "p1", name: "Carol");

        Assert.Equal(1, Ok(actual: MappedPush(send: SendMappedToServer)).Value.ChangesPushed);
        Assert.Equal(0, Ok(actual: MappedPush(send: SendMappedToServer)).Value.ChangesPushed);

        Assert.Equal("Carol", GetPersonName(_serverDb, "p1"));
        Assert.NotNull(GetClientRecordHash(mappingId: PushMappingId, pk: "{\"Id\":\"p1\"}"));
        Assert.Equal(1, GetClientMappingState(mappingId: PushMappingId)?.RecordsSynced);
    }

    /// <summary>Changes from this replica and tables without a strict mapping are not applied.</summary>
    [Fact]
    public void PullWithMapping_OwnOriginAndUnmappedTable_AreSkipped()
    {
        CreateClientMappingTables();
        InsertPerson(db: _serverDb, id: "p1", name: "Alice");

        Assert.Equal(0, Ok(actual: MappedPull(origin: ServerOrigin)).Value.ChangesApplied);
        Assert.Equal(
            0,
            Ok(actual: MappedPull(origin: ClientOrigin, mappings: [])).Value.ChangesApplied
        );
        Assert.Null(GetPersonName(_clientDb, "p1"));
    }

    /// <summary>An apply failure stops the pull and surfaces the database error.</summary>
    [Fact]
    public void PullWithMapping_ApplyFailure_ReturnsError()
    {
        CreateClientMappingTables();
        InsertPerson(db: _serverDb, id: "p1", name: "Alice");

        var result = MappedPull(origin: ClientOrigin, apply: (_, _) => FailedWrite("apply"));

        Assert.Equal(
            new SyncErrorDatabase("apply failed"),
            Assert.IsType<PullResultError>(result).Value
        );
    }

    /// <summary>A send failure stops the push and surfaces the database error.</summary>
    [Fact]
    public void PushWithMapping_SendFailure_ReturnsError()
    {
        CreateClientMappingTables();
        InsertPerson(db: _clientDb, id: "p1", name: "Carol");

        var result = MappedPush(send: _ => FailedWrite("send"));

        Assert.Equal(
            new SyncErrorDatabase("send failed"),
            Assert.IsType<PushResultError>(result).Value
        );
        Assert.Null(GetPersonName(_serverDb, "p1"));
    }

    /// <summary>A trigger suppression failure aborts the pull before any change is fetched.</summary>
    [Fact]
    public void PullWithMapping_SuppressionFailure_ReturnsError()
    {
        CreateClientMappingTables();
        InsertPerson(db: _serverDb, id: "p1", name: "Alice");

        var result = MappedPull(origin: ClientOrigin, enable: () => FailedWrite("suppress"));

        Assert.IsType<PullResultError>(result);
        Assert.Null(GetPersonName(_clientDb, "p1"));
    }

    private PullResultResult MappedPull(
        string origin,
        IReadOnlyList<TableMapping>? mappings = null,
        Func<MappedEntry, SyncOperation, BoolSyncResult>? apply = null,
        Func<BoolSyncResult>? enable = null
    ) =>
        MappedSyncCoordinator.PullWithMapping(
            myOriginId: origin,
            lastSyncedVersion: 0,
            mappingConfig: MappingTestData.Strict(
                mappings: mappings
                    ??
                    [
                        PersonMapping(
                            PullMappingId,
                            MappingDirection.Pull,
                            SyncTrackingStrategy.Version
                        ),
                    ]
            ),
            config: new BatchConfig(1),
            fetchRemoteChanges: FetchServerChanges,
            applyMappedChange: apply ?? ApplyMappedToClient,
            enableTriggerSuppression: enable ?? EnableClientSuppression,
            disableTriggerSuppression: DisableClientSuppression,
            updateLastSyncedVersion: SaveClientSyncVersion,
            getMappingState: GetClientMappingState,
            updateMappingState: SaveClientMappingState,
            getRecordHash: GetClientRecordHash,
            saveRecordHash: SaveClientRecordHash,
            logger: Logger
        );

    private PushResultResult MappedPush(
        Func<IReadOnlyList<(MappedEntry Entry, SyncLogEntry Original)>, BoolSyncResult> send
    ) =>
        MappedSyncCoordinator.PushWithMapping(
            lastPushedVersion: 0,
            mappingConfig: MappingTestData.Strict(
                mappings:
                [
                    PersonMapping(PushMappingId, MappingDirection.Push, SyncTrackingStrategy.Hash),
                ]
            ),
            config: new BatchConfig(100),
            fetchLocalChanges: FetchClientChanges,
            sendMappedChanges: send,
            updateLastPushedVersion: SaveClientPushVersion,
            getMappingState: GetClientMappingState,
            updateMappingState: SaveClientMappingState,
            getRecordHash: GetClientRecordHash,
            saveRecordHash: SaveClientRecordHash,
            logger: Logger
        );

    private static TableMapping PersonMapping(
        string id,
        MappingDirection direction,
        SyncTrackingStrategy strategy
    ) =>
        MappingTestData.Mapping(
            id: id,
            sourceTable: "Person",
            targetTable: "Person",
            direction: direction
        ) with
        {
            SyncTracking = new SyncTrackingConfig(Strategy: strategy),
        };

    private static BoolSyncError FailedWrite(string step) =>
        new(new SyncErrorDatabase($"{step} failed"));

    private BoolSyncResult ApplyMappedToClient(MappedEntry mapped, SyncOperation operation) =>
        ApplyChange(
            _clientDb,
            new SyncLogEntry(
                Version: 0,
                TableName: mapped.TargetTable,
                PkValue: mapped.TargetPkValue,
                Operation: operation,
                Payload: mapped.MappedPayload,
                Origin: ServerOrigin,
                Timestamp: "2024-01-01T00:00:00Z"
            )
        );

    private BoolSyncResult SendMappedToServer(
        IReadOnlyList<(MappedEntry Entry, SyncLogEntry Original)> changes
    ) =>
        SendChangesToServer([
            .. changes.Select(c =>
                c.Original with
                {
                    TableName = c.Entry.TargetTable,
                    PkValue = c.Entry.TargetPkValue,
                    Payload = c.Entry.MappedPayload,
                }
            ),
        ]);

    private void CreateClientMappingTables() =>
        DatabaseCommands.Execute(
            connection: _clientDb,
            configure: command =>
                command.CommandText =
                    $"{SyncSchema.CreateMappingStateTable}\n{SyncSchema.CreateRecordHashesTable}"
        );

    private MappingSyncState? GetClientMappingState(string mappingId) =>
        MappingStateRepository.GetMappingState(_clientDb, mappingId)
            is MappingStateOk { Value: { } state }
            ? new MappingSyncState(
                MappingId: state.MappingId,
                LastSyncedVersion: state.LastSyncedVersion,
                LastSyncTimestamp: state.LastSyncTimestamp,
                RecordsSynced: state.RecordsSynced
            )
            : null;

    private void SaveClientMappingState(MappingSyncState state) =>
        Assert.IsType<BoolSyncOk>(
            MappingStateRepository.UpsertMappingState(
                _clientDb,
                new MappingStateEntry(
                    MappingId: state.MappingId,
                    LastSyncedVersion: state.LastSyncedVersion,
                    LastSyncTimestamp: state.LastSyncTimestamp,
                    RecordsSynced: state.RecordsSynced
                )
            )
        );

    private RecordHash? GetClientRecordHash(string mappingId, string pk) =>
        MappingStateRepository.GetRecordHash(_clientDb, mappingId, pk)
            is RecordHashOk { Value: { } hash }
            ? new RecordHash(
                MappingId: hash.MappingId,
                SourcePk: hash.SourcePk,
                PayloadHash: hash.PayloadHash,
                SyncedAt: hash.SyncedAt
            )
            : null;

    private void SaveClientRecordHash(RecordHash hash) =>
        Assert.IsType<BoolSyncOk>(
            MappingStateRepository.UpsertRecordHash(
                _clientDb,
                new RecordHashEntry(
                    MappingId: hash.MappingId,
                    SourcePk: hash.SourcePk,
                    PayloadHash: hash.PayloadHash,
                    SyncedAt: hash.SyncedAt
                )
            )
        );
}
