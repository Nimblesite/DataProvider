// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.Sync.Http.Tests;

public partial class HttpMappingE2ETests
{
    private static global::Nimblesite.Sync.Core.SyncLogEntry Dataset_E2E_SyncWithMapping_TransformsData_AcrossDatabases_1(
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.Sync.Core.SyncLogEntry> changesList,
        global::Nimblesite.Sync.Core.MappedEntry entry,
        global::System.String mergedPayload
    ) =>
        Fixture_E2E_SyncWithMapping_TransformsData_AcrossDatabases_1(
            changesList: changesList,
            entry: entry,
            mergedPayload: mergedPayload
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Dataset_E2E_MultipleRecords_AllTransformedCorrectly_1(
        global::Nimblesite.Sync.Core.SyncLogEntry change,
        global::Nimblesite.Sync.Core.MappedEntry entry,
        global::System.String mergedPayload
    ) =>
        Fixture_E2E_MultipleRecords_AllTransformedCorrectly_1(
            change: change,
            entry: entry,
            mergedPayload: mergedPayload
        );

    private static global::Nimblesite.Sync.Core.SyncMappingConfig Dataset_MappingEngine_UnmappedTable_WithPassthrough_ReturnsIdentity_1() =>
        new SyncMappingConfig(
            Version: "1.0",
            UnmappedTableBehavior: UnmappedTableBehavior.Passthrough,
            Mappings: [] // No mappings defined
        );

    private static global::Nimblesite.Sync.Core.SyncMappingConfig Dataset_MappingEngine_UnmappedTable_WithStrict_ReturnsSkipped_1() =>
        new SyncMappingConfig(
            Version: "1.0",
            UnmappedTableBehavior: UnmappedTableBehavior.Strict,
            Mappings: [] // No mappings defined
        );
}
