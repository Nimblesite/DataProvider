// Implements [CI-DESLOP-INDEPENDENT-DATA].
using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.Sqlite;
using Tests.Shared;
using Xunit;
using PersonCommand = global::Tests.Shared.PersonCommandParameters;

namespace Nimblesite.Sync.SQLite.Tests;

public partial class SpecComplianceTests
{
    private global::Nimblesite.Sync.Core.SyncLogEntry Fixture_Spec_S10_Subscriptions_RecordLevelMatching_1() =>
        new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Update,
            "{}",
            _originId,
            ""
        );

    private global::Nimblesite.Sync.Core.SyncLogEntry Fixture_Spec_S10_Subscriptions_RecordLevelMatching_2() =>
        new SyncLogEntry(
            2,
            "Person",
            "{\"Id\":\"p3\"}",
            SyncOperation.Update,
            "{}",
            _originId,
            ""
        );

    private global::Nimblesite.Sync.Core.SyncLogEntry Fixture_Spec_S10_Subscriptions_TableLevelMatchesAllChanges_1() =>
        new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Insert,
            "{}",
            _originId,
            ""
        );

    private global::Nimblesite.Sync.Core.SyncLogEntry Fixture_Spec_S10_Subscriptions_TableLevelMatchesAllChanges_2() =>
        new SyncLogEntry(
            2,
            "Person",
            "{\"Id\":\"p999\"}",
            SyncOperation.Delete,
            null,
            _originId,
            ""
        );

    private global::Nimblesite.Sync.Core.SyncLogEntry Fixture_Spec_S10_Subscriptions_TableLevelMatchesAllChanges_3() =>
        new SyncLogEntry(
            3,
            "Order",
            "{\"Id\":\"o1\"}",
            SyncOperation.Insert,
            "{}",
            _originId,
            ""
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_Spec_S14_ConflictDetection_SameTablePkDifferentOrigin_1() =>
        new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Update,
            "{}",
            "origin-A",
            "2025-01-01T00:00:00.000Z"
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_Spec_S14_ConflictDetection_SameTablePkDifferentOrigin_2() =>
        new SyncLogEntry(
            2,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Update,
            "{}",
            "origin-B",
            "2025-01-01T00:00:01.000Z"
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_Spec_S14_ConflictDetection_SameTablePkDifferentOrigin_3() =>
        new SyncLogEntry(
            3,
            "Person",
            "{\"Id\":\"p2\"}",
            SyncOperation.Update,
            "{}",
            "origin-B",
            "2025-01-01T00:00:01.000Z"
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_Spec_S14_LastWriteWins_HigherTimestampWins_1() =>
        new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Update,
            "{\"Name\":\"Old\"}",
            "origin-A",
            "2025-01-01T00:00:00.000Z"
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_Spec_S14_LastWriteWins_HigherTimestampWins_2() =>
        new SyncLogEntry(
            2,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Update,
            "{\"Name\":\"New\"}",
            "origin-B",
            "2025-01-01T00:00:01.000Z"
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_Spec_S14_ServerWins_AlwaysChoosesRemote_1() =>
        new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Update,
            "{}",
            "origin-A",
            "2025-01-01T00:00:01.000Z"
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_Spec_S14_ServerWins_AlwaysChoosesRemote_2() =>
        new SyncLogEntry(
            2,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Update,
            "{}",
            "origin-B",
            "2025-01-01T00:00:00.000Z"
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_Spec_S14_ClientWins_AlwaysChoosesLocal_1() =>
        new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Update,
            "{}",
            "origin-A",
            "2025-01-01T00:00:00.000Z"
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_Spec_S14_ClientWins_AlwaysChoosesLocal_2() =>
        new SyncLogEntry(
            2,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Update,
            "{}",
            "origin-B",
            "2025-01-01T00:00:01.000Z"
        );
}
