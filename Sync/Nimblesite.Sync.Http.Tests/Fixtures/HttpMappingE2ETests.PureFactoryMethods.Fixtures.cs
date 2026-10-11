// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Pure immutable fixture definitions; assertions and database workflows remain analysed.

namespace Nimblesite.Sync.Http.Tests;

public sealed partial class HttpMappingE2ETests
{
    private static SyncMappingConfig CreateUserToCustomerMapping() =>
        new(
            Version: "1.0",
            UnmappedTableBehavior: UnmappedTableBehavior.Strict,
            Mappings:
            [
                new TableMapping(
                    Id: "user-to-customer",
                    SourceTable: "User",
                    TargetTable: "customer",
                    Direction: MappingDirection.Push,
                    Enabled: true,
                    PkMapping: new PkMapping("Id", "customer_id"),
                    ColumnMappings:
                    [
                        new ColumnMapping("FullName", "name"),
                        new ColumnMapping("EmailAddress", "email"),
                        new ColumnMapping(
                            Source: "FullName",
                            Target: "name_upper",
                            Transform: TransformType.Lql,
                            Lql: "upper(FullName)"
                        ),
                        new ColumnMapping(
                            Source: null,
                            Target: "source",
                            Transform: TransformType.Constant,
                            Value: "mobile-app"
                        ),
                        new ColumnMapping(
                            Source: "CreatedAt",
                            Target: "registered_date",
                            Transform: TransformType.Lql,
                            Lql: "CreatedAt |> dateFormat('yyyy-MM-dd')"
                        ),
                    ],
                    ExcludedColumns: ["PasswordHash", "SecurityStamp"],
                    Filter: null,
                    SyncTracking: new SyncTrackingConfig()
                ),
            ]
        );

    private static SyncMappingConfig MappingConfig(
        string mappingId,
        string sourceTable,
        string targetTable,
        params ColumnMapping[] mappings
    ) =>
        new(
            Version: "1.0",
            UnmappedTableBehavior: UnmappedTableBehavior.Strict,
            Mappings:
            [
                new TableMapping(
                    Id: mappingId,
                    SourceTable: sourceTable,
                    TargetTable: targetTable,
                    Direction: MappingDirection.Push,
                    Enabled: true,
                    PkMapping: null,
                    ColumnMappings: mappings,
                    ExcludedColumns: [],
                    Filter: null,
                    SyncTracking: new SyncTrackingConfig()
                ),
            ]
        );

    private static SyncLogEntry SourceEntry(
        string payload,
        string tableName = "Source",
        string pkValue = """{"Id":"1"}"""
    ) =>
        new(
            Version: 1,
            TableName: tableName,
            PkValue: pkValue,
            Operation: SyncOperation.Insert,
            Payload: payload,
            Origin: "test",
            Timestamp: "2024-01-01T00:00:00Z"
        );
}
