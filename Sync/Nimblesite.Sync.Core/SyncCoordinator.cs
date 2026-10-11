using Microsoft.Extensions.Logging;

namespace Nimblesite.Sync.Core;

/// <summary>
/// Result of a sync pull operation.
/// </summary>
/// <param name="ChangesApplied">Number of changes pulled and applied.</param>
/// <param name="FromVersion">Starting version of the pull.</param>
/// <param name="ToVersion">Ending version after the pull.</param>
/// <example>
/// <code>
/// // After a successful pull operation
/// var result = new PullResult(ChangesApplied: 42, FromVersion: 100, ToVersion: 142);
/// Console.WriteLine($"Pulled {result.ChangesApplied} changes");
/// </code>
/// </example>
public sealed record PullResult(int ChangesApplied, long FromVersion, long ToVersion);

/// <summary>
/// Result of a sync push operation.
/// </summary>
/// <param name="ChangesPushed">Number of changes pushed.</param>
/// <param name="FromVersion">Starting version of the push.</param>
/// <param name="ToVersion">Ending version after the push.</param>
/// <example>
/// <code>
/// // After a successful push operation
/// var result = new PushResult(ChangesPushed: 15, FromVersion: 50, ToVersion: 65);
/// Console.WriteLine($"Pushed {result.ChangesPushed} changes to server");
/// </code>
/// </example>
public sealed record PushResult(int ChangesPushed, long FromVersion, long ToVersion);

/// <summary>
/// Result of a full bidirectional sync operation.
/// </summary>
/// <param name="Pull">Result of pulling changes from remote.</param>
/// <param name="Push">Result of pushing changes to remote.</param>
/// <example>
/// <code>
/// // After a full sync operation
/// var syncResult = new SyncResult(
///     Pull: new PullResult(ChangesApplied: 42, FromVersion: 100, ToVersion: 142),
///     Push: new PushResult(ChangesPushed: 15, FromVersion: 50, ToVersion: 65)
/// );
/// Console.WriteLine($"Sync complete: pulled {syncResult.Pull.ChangesApplied}, pushed {syncResult.Push.ChangesPushed}");
/// </code>
/// </example>
public sealed record SyncResult(PullResult Pull, PushResult Push);

/// <summary>
/// Coordinates sync operations between replicas.
/// Main entry point for pull/push sync operations.
/// Implements spec Section 11 (Bi-Directional Sync Protocol).
/// </summary>
/// <example>
/// <code>
/// // Perform a bidirectional sync
/// using var connection = new SqliteConnection("Data Source=local.db");
/// connection.Open();
///
/// var result = SyncCoordinator.Sync(
///     myOriginId: "client-uuid-123",
///     lastServerVersion: 100,
///     lastPushVersion: 50,
///     config: new BatchConfig(BatchSize: 1000),
///     fetchRemoteChanges: (version, limit) => httpClient.GetChanges(version, limit),
///     applyLocalChange: entry => ApplyChange(connection, entry),
///     enableTriggerSuppression: () => EnableSuppression(connection),
///     disableTriggerSuppression: () => DisableSuppression(connection),
///     updateLastServerVersion: v => UpdateServerVersion(connection, v),
///     fetchLocalChanges: (version, limit) => GetLocalChanges(connection, version, limit),
///     sendToRemote: entries => httpClient.PushChanges(entries),
///     updateLastPushVersion: v => UpdatePushVersion(connection, v),
///     logger: logger
/// );
/// </code>
/// </example>
public static class SyncCoordinator
{
    /// <summary>
    /// Pulls changes from a remote source and applies them locally.
    /// </summary>
    /// <param name="myOriginId">This replica's origin ID.</param>
    /// <param name="lastSyncedVersion">Last synced version to start from.</param>
    /// <param name="config">Batch configuration.</param>
    /// <param name="fetchRemoteChanges">Function to fetch changes from remote.</param>
    /// <param name="applyLocalChange">Function to apply a change locally.</param>
    /// <param name="enableTriggerSuppression">Function to enable trigger suppression.</param>
    /// <param name="disableTriggerSuppression">Function to disable trigger suppression.</param>
    /// <param name="updateLastSyncedVersion">Action to update last synced version.</param>
    /// <param name="logger">Logger for sync operations.</param>
    /// <returns>Pull result or sync error.</returns>
    public static PullResultResult Pull(
        string myOriginId,
        long lastSyncedVersion,
        BatchConfig config,
        Func<long, int, SyncLogListResult> fetchRemoteChanges,
        Func<SyncLogEntry, BoolSyncResult> applyLocalChange,
        Func<BoolSyncResult> enableTriggerSuppression,
        Func<BoolSyncResult> disableTriggerSuppression,
        Action<long> updateLastSyncedVersion,
        ILogger logger
    )
    {
        logger.LogInformation(
            "PULL: Starting pull from version {FromVersion}, origin={OriginId}, batchSize={BatchSize}",
            lastSyncedVersion,
            myOriginId,
            config.BatchSize
        );

        // Enable trigger suppression to prevent echo
        var suppressResult = enableTriggerSuppression();
        if (suppressResult is BoolSyncError suppressFailure)
        {
            logger.LogError(
                "PULL: Failed to enable trigger suppression: {Error}",
                suppressFailure.Value
            );
            return new PullResultError(suppressFailure.Value);
        }

        logger.LogDebug("PULL: Trigger suppression enabled");

        try
        {
            return RunBatches<PullResultResult>(
                pulling: true,
                startVersion: lastSyncedVersion,
                config: config,
                fetchChanges: fetchRemoteChanges,
                applyBatch: batch =>
                    ChangeApplier.ApplyBatch(
                        batch: batch,
                        myOriginId: myOriginId,
                        maxRetryPasses: config.MaxRetryPasses,
                        applyChange: applyLocalChange,
                        logger: logger
                    ),
                updateVersion: updateLastSyncedVersion,
                logger: logger,
                complete: (total, from, to) =>
                    new PullResultOk(
                        new PullResult(ChangesApplied: total, FromVersion: from, ToVersion: to)
                    ),
                failure: error => new PullResultError(error)
            );
        }
        finally
        {
            logger.LogDebug("PULL: Disabling trigger suppression");
            _ = disableTriggerSuppression();
        }
    }

    /// <summary>
    /// Pushes local changes to a remote destination.
    /// </summary>
    /// <param name="lastPushedVersion">Last pushed version to start from.</param>
    /// <param name="config">Batch configuration.</param>
    /// <param name="fetchLocalChanges">Function to fetch local changes.</param>
    /// <param name="sendToRemote">Function to send changes to remote.</param>
    /// <param name="updateLastPushedVersion">Action to update last pushed version.</param>
    /// <param name="logger">Logger for sync operations.</param>
    /// <returns>Push result or sync error.</returns>
    public static PushResultResult Push(
        long lastPushedVersion,
        BatchConfig config,
        Func<long, int, SyncLogListResult> fetchLocalChanges,
        Func<IReadOnlyList<SyncLogEntry>, BoolSyncResult> sendToRemote,
        Action<long> updateLastPushedVersion,
        ILogger logger
    )
    {
        logger.LogInformation(
            "PUSH: Starting push from version {FromVersion}, batchSize={BatchSize}",
            lastPushedVersion,
            config.BatchSize
        );

        return RunBatches<PushResultResult>(
            pulling: false,
            startVersion: lastPushedVersion,
            config: config,
            fetchChanges: fetchLocalChanges,
            applyBatch: batch =>
                SendBatch(batch: batch, sendToRemote: sendToRemote, logger: logger),
            updateVersion: updateLastPushedVersion,
            logger: logger,
            complete: (total, from, to) =>
                new PushResultOk(
                    new PushResult(ChangesPushed: total, FromVersion: from, ToVersion: to)
                ),
            failure: error => new PushResultError(error)
        );
    }

    /// <summary>
    /// Performs a full bidirectional sync: pull then push.
    /// </summary>
    /// <param name="myOriginId">This replica's origin ID.</param>
    /// <param name="lastServerVersion">Last server version to pull from.</param>
    /// <param name="lastPushVersion">Last push version to push from.</param>
    /// <param name="config">Batch configuration.</param>
    /// <param name="fetchRemoteChanges">Function to fetch remote changes.</param>
    /// <param name="applyLocalChange">Function to apply a change locally.</param>
    /// <param name="enableTriggerSuppression">Function to enable trigger suppression.</param>
    /// <param name="disableTriggerSuppression">Function to disable trigger suppression.</param>
    /// <param name="updateLastServerVersion">Action to update last server version.</param>
    /// <param name="fetchLocalChanges">Function to fetch local changes.</param>
    /// <param name="sendToRemote">Function to send changes to remote.</param>
    /// <param name="updateLastPushVersion">Action to update last push version.</param>
    /// <param name="logger">Logger for sync operations.</param>
    /// <returns>Sync result or sync error.</returns>
    public static SyncResultResult Sync(
        string myOriginId,
        long lastServerVersion,
        long lastPushVersion,
        BatchConfig config,
        Func<long, int, SyncLogListResult> fetchRemoteChanges,
        Func<SyncLogEntry, BoolSyncResult> applyLocalChange,
        Func<BoolSyncResult> enableTriggerSuppression,
        Func<BoolSyncResult> disableTriggerSuppression,
        Action<long> updateLastServerVersion,
        Func<long, int, SyncLogListResult> fetchLocalChanges,
        Func<IReadOnlyList<SyncLogEntry>, BoolSyncResult> sendToRemote,
        Action<long> updateLastPushVersion,
        ILogger logger
    )
    {
        logger.LogInformation(
            "SYNC: Starting bidirectional sync, origin={OriginId}, serverVersion={ServerVersion}, pushVersion={PushVersion}",
            myOriginId,
            lastServerVersion,
            lastPushVersion
        );

        // Pull first (get remote changes)
        logger.LogDebug("SYNC: Starting PULL phase");
        var pullResult = Pull(
            myOriginId,
            lastServerVersion,
            config,
            fetchRemoteChanges,
            applyLocalChange,
            enableTriggerSuppression,
            disableTriggerSuppression,
            updateLastServerVersion,
            logger
        );

        if (pullResult is PullResultError pullFailure)
        {
            logger.LogError("SYNC: Pull phase failed: {Error}", pullFailure.Value);
            return new SyncResultError(pullFailure.Value);
        }

        var pull = ((PullResultOk)pullResult).Value;
        logger.LogInformation(
            "SYNC: Pull phase complete. Applied {Count} changes",
            pull.ChangesApplied
        );

        // Push second (send local changes)
        logger.LogDebug("SYNC: Starting PUSH phase");
        var pushResult = Push(
            lastPushVersion,
            config,
            fetchLocalChanges,
            sendToRemote,
            updateLastPushVersion,
            logger
        );

        if (pushResult is PushResultError pushFailure)
        {
            logger.LogError("SYNC: Push phase failed: {Error}", pushFailure.Value);
            return new SyncResultError(pushFailure.Value);
        }

        var push = ((PushResultOk)pushResult).Value;
        logger.LogInformation(
            "SYNC: Push phase complete. Pushed {Count} changes",
            push.ChangesPushed
        );

        logger.LogInformation(
            "SYNC: Bidirectional sync complete. Pulled {PullCount}, pushed {PushCount}",
            pull.ChangesApplied,
            push.ChangesPushed
        );

        return new SyncResultOk(new SyncResult(pull, push));
    }

    // Implements [SYNC-COORDINATOR-SHARED-BATCHES].
    private static TResult RunBatches<TResult>(
        bool pulling,
        long startVersion,
        BatchConfig config,
        Func<long, int, SyncLogListResult> fetchChanges,
        Func<SyncBatch, BatchApplyResultResult> applyBatch,
        Action<long> updateVersion,
        ILogger logger,
        Func<int, long, long, TResult> complete,
        Func<SyncError, TResult> failure
    )
    {
        var phase = pulling ? "PULL" : "PUSH";
        var total = 0;
        var currentVersion = startVersion;
        var batchNumber = 0;
        while (true)
        {
            batchNumber++;
            logger.LogDebug(
                "{Phase}: Fetching batch {BatchNumber} from version {Version}",
                phase,
                batchNumber,
                currentVersion
            );
            var fetched = BatchManager.FetchBatch(
                fromVersion: currentVersion,
                batchSize: config.BatchSize,
                fetchChanges: fetchChanges,
                logger: logger
            );
            if (fetched is SyncBatchError(var fetchError))
            {
                logger.LogError("{Phase}: Batch fetch failed: {Error}", phase, fetchError);
                return failure(fetchError);
            }
            if (fetched is not SyncBatchOk(var batch))
            {
                // Result has only the two generated variants.
                return failure(new SyncErrorDatabase(Message: "Unexpected batch result"));
            }
            if (batch.Changes.Count == 0)
            {
                logger.LogDebug(
                    "{Phase}: No more changes to {Operation}",
                    phase,
                    pulling ? "pull" : "push"
                );
                break;
            }
            logger.LogInformation(
                "{Phase}: Batch {BatchNumber} contains {Count} changes, versions {From}-{To}, hasMore={HasMore}",
                phase,
                batchNumber,
                batch.Changes.Count,
                batch.FromVersion,
                batch.ToVersion,
                batch.HasMore
            );
            var processed = applyBatch(batch);
            if (processed is BatchApplyResultError(var applyError))
            {
                if (pulling)
                    logger.LogError("PULL: Batch apply failed: {Error}", applyError);
                else
                    logger.LogError("PUSH: Send to remote failed: {Error}", applyError);
                return failure(applyError);
            }
            if (processed is not BatchApplyResultOk(var applied))
                return failure(new SyncErrorDatabase(Message: "Unexpected batch apply result"));
            total += applied.AppliedCount;
            currentVersion = applied.ToVersion;
            updateVersion(currentVersion);
            logger.LogDebug(
                "{Phase}: {Operation} {Count} changes, updated version to {Version}",
                phase,
                pulling ? "Applied" : "Sent",
                applied.AppliedCount,
                currentVersion
            );
            if (!batch.HasMore)
                break;
        }
        logger.LogInformation(
            "{Phase}: Complete. {Operation} {Total} changes, version {From}->{To}",
            phase,
            pulling ? "Applied" : "Pushed",
            total,
            startVersion,
            currentVersion
        );
        return complete(total, startVersion, currentVersion);
    }

    private static BatchApplyResultResult SendBatch(
        SyncBatch batch,
        Func<IReadOnlyList<SyncLogEntry>, BoolSyncResult> sendToRemote,
        ILogger logger
    )
    {
        logger.LogDebug("PUSH: Sending {Count} changes to remote", batch.Changes.Count);
        return sendToRemote([.. batch.Changes]) is BoolSyncError(var error)
            ? new BatchApplyResultError(error)
            : new BatchApplyResultOk(
                new BatchApplyResult(
                    AppliedCount: batch.Changes.Count,
                    DeferredCount: 0,
                    ToVersion: batch.ToVersion
                )
            );
    }
}
