using Microsoft.Extensions.Logging;

namespace Nimblesite.Sync.Tests;

// Implements [SYNC-MAPPING-TEST-DATA].
internal static class MappingTestData
{
    internal static TableMapping Mapping(
        string id,
        string sourceTable,
        string? targetTable,
        IReadOnlyList<ColumnMapping>? columnMappings = null,
        MappingDirection direction = MappingDirection.Push
    ) =>
        TableMapping.Identity(tableName: sourceTable, direction: direction) with
        {
            Id = id,
            TargetTable = targetTable,
            ColumnMappings = columnMappings ?? [],
        };

    internal static SyncLogEntry Entry(string table, string pk, string? payload) =>
        new(
            Version: 1,
            TableName: table,
            PkValue: pk,
            Operation: SyncOperation.Insert,
            Payload: payload,
            Origin: "test-origin",
            Timestamp: "2024-01-01T00:00:00Z"
        );

    internal static SyncMappingConfig Strict(IReadOnlyList<TableMapping> mappings) =>
        new(
            Version: "1.0",
            UnmappedTableBehavior: UnmappedTableBehavior.Strict,
            Mappings: mappings
        );

    internal static TExpected Expected<TExpected>(
        SyncLogEntry entry,
        SyncMappingConfig config,
        ILogger logger,
        MappingDirection direction = MappingDirection.Push
    )
        where TExpected : MappingResult =>
        Assert.IsType<TExpected>(
            @object: MappingEngine.ApplyMapping(
                entry: entry,
                config: config,
                direction: direction,
                logger: logger
            )
        );

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static MappingSuccess ExpectNullMappedPayload(
        Func<TableMapping> mapping,
        Func<SyncLogEntry> entry,
        ILogger logger
    )
    {
        var config = Strict(mappings: [mapping()]);
        var success = Expected<MappingSuccess>(entry: entry(), config: config, logger: logger);
        Assert.Null(success.Entries[0].MappedPayload);
        return success;
    }
}
