// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.Sync.Tests;

public partial class BatchManagerTests
{
    private static global::Outcome.Result<
        global::Nimblesite.Sync.Core.BatchApplyResult,
        global::Nimblesite.Sync.Core.SyncError
    >.Ok<
        global::Nimblesite.Sync.Core.BatchApplyResult,
        global::Nimblesite.Sync.Core.SyncError
    > Dataset_ProcessAllBatches_ProcessesMultipleBatches_1(
        global::Nimblesite.Sync.Core.SyncBatch batch
    ) => new BatchApplyResultOk(new BatchApplyResult(batch.Changes.Count, 0, batch.ToVersion));
}
