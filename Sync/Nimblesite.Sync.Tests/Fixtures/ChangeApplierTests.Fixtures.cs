// Implements [CI-DESLOP-INDEPENDENT-DATA].
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.Shared;

namespace Nimblesite.Sync.Tests;

public partial class ChangeApplierTests
{
    private global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyBatch_SkipsOwnOriginChanges_1(
        global::System.String myOrigin
    ) =>
        new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"1\"}",
            SyncOperation.Insert,
            "{\"Id\":\"1\",\"Name\":\"Alice\"}",
            myOrigin,
            "2025-01-01T00:00:00.000Z"
        );

    private global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyBatch_SkipsOwnOriginChanges_2() =>
        new SyncLogEntry(
            2,
            "Person",
            "{\"Id\":\"2\"}",
            SyncOperation.Insert,
            "{\"Id\":\"2\",\"Name\":\"Bob\"}",
            "other-origin",
            "2025-01-01T00:00:01.000Z"
        );

    private global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyBatch_DefersAndRetriesFkViolations_1() =>
        new SyncLogEntry(
            1,
            "Child",
            "{\"Id\":\"c1\"}",
            SyncOperation.Insert,
            "{\"Id\":\"c1\",\"ParentId\":\"p1\",\"Name\":\"Child1\"}",
            "other",
            "2025-01-01T00:00:00.000Z"
        );

    private global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyBatch_DefersAndRetriesFkViolations_2() =>
        new SyncLogEntry(
            2,
            "Parent",
            "{\"Id\":\"p1\"}",
            SyncOperation.Insert,
            "{\"Id\":\"p1\",\"Name\":\"Parent1\"}",
            "other",
            "2025-01-01T00:00:01.000Z"
        );

    private global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyBatch_FailsAfterMaxRetries_1() =>
        new SyncLogEntry(
            1,
            "Child",
            "{\"Id\":\"c1\"}",
            SyncOperation.Insert,
            "{\"Id\":\"c1\",\"ParentId\":\"missing\"}",
            "other",
            "2025-01-01T00:00:00.000Z"
        );

    private global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyBatch_WithRealSqlite_HandlesInsertUpdateDelete_1() =>
        new SyncLogEntry(
            1,
            "Child",
            "{\"Id\":\"c1\"}",
            SyncOperation.Insert,
            "{\"Id\":\"c1\",\"ParentId\":\"p1\",\"Name\":\"Child1\"}",
            "other",
            "2025-01-01T00:00:00.000Z"
        );

    private global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyBatch_WithRealSqlite_HandlesInsertUpdateDelete_2() =>
        new SyncLogEntry(
            2,
            "Child",
            "{\"Id\":\"c1\"}",
            SyncOperation.Update,
            "{\"Id\":\"c1\",\"ParentId\":\"p1\",\"Name\":\"UpdatedChild\"}",
            "other",
            "2025-01-01T00:00:01.000Z"
        );
}
