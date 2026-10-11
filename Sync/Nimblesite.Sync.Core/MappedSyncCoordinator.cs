using Microsoft.Extensions.Logging;

namespace Nimblesite.Sync.Core;

/// <summary>
/// Result of mapped sync pull operation.
/// </summary>
/// <param name="ChangesApplied">Number of changes applied (after mapping).</param>
/// <param name="ChangesSkipped">Number of changes skipped (filtered or no mapping).</param>
/// <param name="FromVersion">Starting version.</param>
/// <param name="ToVersion">Ending version.</param>
public sealed record MappedPullResult(
    int ChangesApplied,
    int ChangesSkipped,
    long FromVersion,
    long ToVersion
);

/// <summary>
/// Result of mapped sync push operation.
/// </summary>
/// <param name="ChangesPushed">Number of changes pushed (after mapping).</param>
/// <param name="ChangesSkipped">Number of changes skipped (filtered or no mapping).</param>
/// <param name="FromVersion">Starting version.</param>
/// <param name="ToVersion">Ending version.</param>
public sealed record MappedPushResult(
    int ChangesPushed,
    int ChangesSkipped,
    long FromVersion,
    long ToVersion
);

/// <summary>
/// Sync coordinator with data mapping support.
/// Applies mappings (Section 7) during sync operations.
/// </summary>
public static class MappedSyncCoordinator
{
    /// <summary>
    /// Pulls changes from remote and applies them with mapping transformations.
    /// Per spec Section 12.2 - Sync Session Protocol.
    /// </summary>
    /// <param name="myOriginId">This replica's origin ID.</param>
    /// <param name="lastSyncedVersion">Last synced version.</param>
    /// <param name="mappingConfig">Mapping configuration.</param>
    /// <param name="config">Batch configuration.</param>
    /// <param name="fetchRemoteChanges">Fetches changes from remote.</param>
    /// <param name="applyMappedChange">Applies a mapped change locally.</param>
    /// <param name="enableTriggerSuppression">Enables trigger suppression.</param>
    /// <param name="disableTriggerSuppression">Disables trigger suppression.</param>
    /// <param name="updateLastSyncedVersion">Updates last synced version.</param>
    /// <param name="getMappingState">Gets mapping state for tracking.</param>
    /// <param name="updateMappingState">Updates mapping state after sync.</param>
    /// <param name="getRecordHash">Gets record hash for hash tracking.</param>
    /// <param name="saveRecordHash">Saves record hash after sync.</param>
    /// <param name="logger">Logger.</param>
    /// <returns>Mapped pull result or error.</returns>
    public static PullResultResult PullWithMapping(
        string myOriginId,
        long lastSyncedVersion,
        SyncMappingConfig mappingConfig,
        BatchConfig config,
        Func<long, int, SyncLogListResult> fetchRemoteChanges,
        Func<MappedEntry, SyncOperation, BoolSyncResult> applyMappedChange,
        Func<BoolSyncResult> enableTriggerSuppression,
        Func<BoolSyncResult> disableTriggerSuppression,
        Action<long> updateLastSyncedVersion,
        Func<string, MappingSyncState?> getMappingState,
        Action<MappingSyncState> updateMappingState,
        Func<string, string, RecordHash?> getRecordHash,
        Action<RecordHash> saveRecordHash,
        ILogger logger
    )
    {
        logger.LogInformation(
            "MAPPED PULL: Starting from version {FromVersion}, origin={OriginId}",
            lastSyncedVersion,
            myOriginId
        );

        var suppressResult = enableTriggerSuppression();
        if (suppressResult is BoolSyncError suppressError)
        {
            logger.LogError(
                "MAPPED PULL: Failed to enable suppression: {Error}",
                suppressError.Value
            );
            return new PullResultError(suppressError.Value);
        }

        try
        {
            var startVersion = lastSyncedVersion;

            var totalApplied = 0;
            var totalSkipped = 0;
            var mappingCounts = new Dictionary<string, int>();

            return RunMappedBatches<PullResultResult>(
                startVersion: startVersion,
                config: config,
                fetchChanges: fetchRemoteChanges,
                processBatch: batch =>
                {
                    logger.LogDebug(
                        "MAPPED PULL: Processing batch with {Count} changes",
                        batch.Changes.Count
                    );

                    foreach (var entry in batch.Changes)
                    {
                        // Skip changes from our own origin (echo prevention)
                        if (entry.Origin == myOriginId)
                        {
                            totalSkipped++;
                            continue;
                        }

                        // Find and apply mapping
                        var mapping = MappingEngine.FindMapping(
                            entry.TableName,
                            mappingConfig,
                            MappingDirection.Pull
                        );

                        // Check sync tracking
                        if (
                            mapping is not null
                            && !SyncTrackingManager.ShouldSync(
                                entry,
                                mapping,
                                getMappingState,
                                getRecordHash
                            )
                        )
                        {
                            logger.LogDebug(
                                "MAPPED PULL: Skipping already-synced entry {Version}",
                                entry.Version
                            );
                            totalSkipped++;
                            continue;
                        }

                        // Apply mapping
                        var mappingResult = MappingEngine.ApplyMapping(
                            entry,
                            mappingConfig,
                            MappingDirection.Pull,
                            logger
                        );

                        switch (mappingResult)
                        {
                            case MappingSkipped skip:
                                logger.LogDebug(
                                    "MAPPED PULL: Skipped {TableName}: {Reason}",
                                    entry.TableName,
                                    skip.Reason
                                );
                                totalSkipped++;
                                continue;

                            case MappingFailed fail:
                                logger.LogError(
                                    "MAPPED PULL: Mapping failed for {TableName}: {Error}",
                                    entry.TableName,
                                    fail.Error
                                );
                                return fail.Error;

                            case MappingSuccess success:
                                foreach (var mapped in success.Entries)
                                {
                                    var applyResult = applyMappedChange(mapped, entry.Operation);
                                    if (applyResult is BoolSyncError applyError)
                                    {
                                        logger.LogError(
                                            "MAPPED PULL: Apply failed for {Table}: {Error}",
                                            mapped.TargetTable,
                                            applyError.Value
                                        );
                                        return applyError.Value;
                                    }

                                    totalApplied++;
                                    TrackMapping(mapped.MappingId, mappingCounts);

                                    // Save record hash if using hash strategy
                                    if (
                                        mapping?.SyncTracking.Strategy == SyncTrackingStrategy.Hash
                                        && mapped.MappedPayload is not null
                                    )
                                    {
                                        saveRecordHash(
                                            SyncTrackingManager.CreateRecordHash(
                                                mapped.MappingId,
                                                entry.PkValue,
                                                mapped.MappedPayload
                                            )
                                        );
                                    }
                                }
                                break;
                        }
                    }
                    return null;
                },
                updateVersion: updateLastSyncedVersion,
                logger: logger,
                complete: currentVersion =>
                {
                    SaveMappingStates(
                        counts: mappingCounts,
                        version: currentVersion,
                        getState: getMappingState,
                        updateState: updateMappingState
                    );

                    logger.LogInformation(
                        "MAPPED PULL: Complete. Applied {Applied}, skipped {Skipped}, version {From}->{To}",
                        totalApplied,
                        totalSkipped,
                        startVersion,
                        currentVersion
                    );

                    return new PullResultOk(
                        new PullResult(totalApplied, startVersion, currentVersion)
                    );
                },
                failure: failure => new PullResultError(failure)
            );
        }
        finally
        {
            _ = disableTriggerSuppression();
        }
    }

    /// <summary>
    /// Pushes local changes with mapping transformations.
    /// </summary>
    /// <param name="lastPushedVersion">Last pushed version.</param>
    /// <param name="mappingConfig">Mapping configuration.</param>
    /// <param name="config">Batch configuration.</param>
    /// <param name="fetchLocalChanges">Fetches local changes.</param>
    /// <param name="sendMappedChanges">Sends mapped changes to remote.</param>
    /// <param name="updateLastPushedVersion">Updates last pushed version.</param>
    /// <param name="getMappingState">Gets mapping state.</param>
    /// <param name="updateMappingState">Updates mapping state.</param>
    /// <param name="getRecordHash">Gets record hash.</param>
    /// <param name="saveRecordHash">Saves record hash.</param>
    /// <param name="logger">Logger.</param>
    /// <returns>Push result or error.</returns>
    public static PushResultResult PushWithMapping(
        long lastPushedVersion,
        SyncMappingConfig mappingConfig,
        BatchConfig config,
        Func<long, int, SyncLogListResult> fetchLocalChanges,
        Func<
            IReadOnlyList<(MappedEntry Entry, SyncLogEntry Original)>,
            BoolSyncResult
        > sendMappedChanges,
        Action<long> updateLastPushedVersion,
        Func<string, MappingSyncState?> getMappingState,
        Action<MappingSyncState> updateMappingState,
        Func<string, string, RecordHash?> getRecordHash,
        Action<RecordHash> saveRecordHash,
        ILogger logger
    )
    {
        logger.LogInformation(
            "MAPPED PUSH: Starting from version {FromVersion}",
            lastPushedVersion
        );

        var startVersion = lastPushedVersion;

        var totalPushed = 0;
        var totalSkipped = 0;
        var mappingCounts = new Dictionary<string, int>();

        return RunMappedBatches<PushResultResult>(
            startVersion: startVersion,
            config: config,
            fetchChanges: fetchLocalChanges,
            processBatch: batch =>
            {
                var mappedChanges = new List<(MappedEntry Entry, SyncLogEntry Original)>();

                foreach (var entry in batch.Changes)
                {
                    var mapping = MappingEngine.FindMapping(
                        entry.TableName,
                        mappingConfig,
                        MappingDirection.Push
                    );

                    if (
                        mapping is not null
                        && !SyncTrackingManager.ShouldSync(
                            entry,
                            mapping,
                            getMappingState,
                            getRecordHash
                        )
                    )
                    {
                        totalSkipped++;
                        continue;
                    }

                    var mappingResult = MappingEngine.ApplyMapping(
                        entry,
                        mappingConfig,
                        MappingDirection.Push,
                        logger
                    );

                    switch (mappingResult)
                    {
                        case MappingSkipped:
                            totalSkipped++;
                            continue;

                        case MappingFailed fail:
                            return fail.Error;

                        case MappingSuccess success:
                            foreach (var mapped in success.Entries)
                            {
                                mappedChanges.Add((mapped, entry));
                                TrackMapping(mapped.MappingId, mappingCounts);
                            }
                            break;
                    }
                }

                if (mappedChanges.Count > 0)
                {
                    var sendResult = sendMappedChanges(mappedChanges);
                    if (sendResult is BoolSyncError sendError)
                    {
                        return sendError.Value;
                    }

                    totalPushed += mappedChanges.Count;

                    // Save record hashes
                    foreach (var (mapped, original) in mappedChanges)
                    {
                        var mapping = MappingEngine.FindMapping(
                            original.TableName,
                            mappingConfig,
                            MappingDirection.Push
                        );

                        if (
                            mapping?.SyncTracking.Strategy == SyncTrackingStrategy.Hash
                            && mapped.MappedPayload is not null
                        )
                        {
                            saveRecordHash(
                                SyncTrackingManager.CreateRecordHash(
                                    mapped.MappingId,
                                    original.PkValue,
                                    mapped.MappedPayload
                                )
                            );
                        }
                    }
                }
                return null;
            },
            updateVersion: updateLastPushedVersion,
            logger: logger,
            complete: currentVersion =>
            {
                SaveMappingStates(
                    counts: mappingCounts,
                    version: currentVersion,
                    getState: getMappingState,
                    updateState: updateMappingState
                );

                logger.LogInformation(
                    "MAPPED PUSH: Complete. Pushed {Pushed}, skipped {Skipped}, version {From}->{To}",
                    totalPushed,
                    totalSkipped,
                    startVersion,
                    currentVersion
                );

                return new PushResultOk(new PushResult(totalPushed, startVersion, currentVersion));
            },
            failure: failure => new PushResultError(failure)
        );
    }

    private static void TrackMapping(string mappingId, Dictionary<string, int> counts)
    {
        if (counts.TryGetValue(mappingId, out var count))
        {
            counts[mappingId] = count + 1;
        }
        else
        {
            counts[mappingId] = 1;
        }
    }

    // Implements [SYNC-MAPPED-SHARED-BATCHES].
    private static TResult RunMappedBatches<TResult>(
        long startVersion,
        BatchConfig config,
        Func<long, int, SyncLogListResult> fetchChanges,
        Func<SyncBatch, SyncError?> processBatch,
        Action<long> updateVersion,
        ILogger logger,
        Func<long, TResult> complete,
        Func<SyncError, TResult> failure
    )
    {
        var currentVersion = startVersion;
        while (true)
        {
            var fetched = BatchManager.FetchBatch(
                fromVersion: currentVersion,
                batchSize: config.BatchSize,
                fetchChanges: fetchChanges,
                logger: logger
            );
            if (fetched is SyncBatchError(var error))
                return failure(error);
            if (fetched is not SyncBatchOk(var batch))
                return failure(new SyncErrorDatabase(Message: "Unexpected batch result"));
            if (batch.Changes.Count == 0)
                break;
            if (processBatch(batch) is { } processingError)
                return failure(processingError);
            currentVersion = batch.ToVersion;
            updateVersion(currentVersion);
            if (!batch.HasMore)
                break;
        }
        return complete(currentVersion);
    }

    // Implements [SYNC-MAPPED-SHARED-BATCHES].
    private static void SaveMappingStates(
        IReadOnlyDictionary<string, int> counts,
        long version,
        Func<string, MappingSyncState?> getState,
        Action<MappingSyncState> updateState
    )
    {
        foreach (var (mappingId, count) in counts)
        {
            var existing = getState(mappingId);
            var updated = SyncTrackingManager.UpdateMappingState(
                mappingId: mappingId,
                version: version,
                recordCount: count,
                existingState: existing
            );
            updateState(updated);
        }
    }
}
