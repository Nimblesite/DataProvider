// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.Sync.Tests;

public partial class ChangeApplierTests
{
    private static global::Nimblesite.Sync.Core.SyncBatch Dataset_ApplyBatch_SkipsOwnOriginChanges_1(
        global::System.String myOrigin
    ) =>
        new SyncBatch(
            [
                Fixture_ApplyBatch_SkipsOwnOriginChanges_1(myOrigin: myOrigin),
                Fixture_ApplyBatch_SkipsOwnOriginChanges_2(),
            ],
            0,
            2,
            false
        );

    private static global::Nimblesite.Sync.Core.SyncBatch Dataset_ApplyBatch_DefersAndRetriesFkViolations_1() =>
        new SyncBatch(
            [
                Fixture_ApplyBatch_DefersAndRetriesFkViolations_1(),
                Fixture_ApplyBatch_DefersAndRetriesFkViolations_2(),
            ],
            0,
            2,
            false
        );

    private static global::Nimblesite.Sync.Core.SyncBatch Dataset_ApplyBatch_WithRealSqlite_HandlesInsertUpdateDelete_1() =>
        new SyncBatch(
            [
                Fixture_ApplyBatch_WithRealSqlite_HandlesInsertUpdateDelete_1(),
                Fixture_ApplyBatch_WithRealSqlite_HandlesInsertUpdateDelete_2(),
            ],
            0,
            2,
            false
        );
}
