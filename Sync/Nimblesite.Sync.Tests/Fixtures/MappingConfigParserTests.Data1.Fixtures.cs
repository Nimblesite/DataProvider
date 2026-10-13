// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.Tests;

public partial class MappingConfigParserTests
{
    private const string Data_Parse_SimpleMapping_ParsesCorrectly_1 = """
        {
            "version": "1.0",
            "mappings": [
                {
                    "id": "user-mapping",
                    "source_table": "User",
                    "target_table": "Customer",
                    "direction": "push",
                    "enabled": true
                }
            ]
        }
        """;

    private const string Data_Parse_PkMapping_ParsesCorrectly_1 = """
        {
            "version": "1.0",
            "mappings": [
                {
                    "id": "user-mapping",
                    "source_table": "User",
                    "target_table": "Customer",
                    "direction": "push",
                    "pk_mapping": {
                        "source_column": "Id",
                        "target_column": "CustomerId"
                    }
                }
            ]
        }
        """;

    private const string Data_Parse_ColumnMappings_ParsesCorrectly_1 = """
        {
            "version": "1.0",
            "mappings": [
                {
                    "id": "user-mapping",
                    "source_table": "User",
                    "target_table": "Customer",
                    "direction": "push",
                    "column_mappings": [
                        {"source": "FullName", "target": "Name"},
                        {"source": "EmailAddress", "target": "Email"}
                    ]
                }
            ]
        }
        """;

    private const string Data_Parse_ConstantTransform_ParsesCorrectly_1 = """
        {
            "version": "1.0",
            "mappings": [
                {
                    "id": "user-mapping",
                    "source_table": "User",
                    "target_table": "Customer",
                    "direction": "push",
                    "column_mappings": [
                        {"source": null, "target": "Source", "transform": "constant", "value": "mobile-app"}
                    ]
                }
            ]
        }
        """;

    private const string Data_Parse_LqlTransform_ParsesCorrectly_1 = """
        {
            "version": "1.0",
            "mappings": [
                {
                    "id": "user-mapping",
                    "source_table": "User",
                    "target_table": "Customer",
                    "direction": "push",
                    "column_mappings": [
                        {"source": "CreatedAt", "target": "RegisteredDate", "transform": "lql", "lql": "CreatedAt |> dateFormat('yyyy-MM-dd')"}
                    ]
                }
            ]
        }
        """;

    private const string Data_Parse_ExcludedColumns_ParsesCorrectly_1 = """
        {
            "version": "1.0",
            "mappings": [
                {
                    "id": "user-mapping",
                    "source_table": "User",
                    "target_table": "Customer",
                    "direction": "push",
                    "excluded_columns": ["PasswordHash", "SecurityStamp"]
                }
            ]
        }
        """;

    private const string Data_Parse_Filter_ParsesCorrectly_1 = """
        {
            "version": "1.0",
            "mappings": [
                {
                    "id": "user-mapping",
                    "source_table": "User",
                    "target_table": "Customer",
                    "direction": "push",
                    "filter": {"lql": "IsActive = true AND DeletedAt IS NULL"}
                }
            ]
        }
        """;

    private const string Data_Parse_SyncTracking_ParsesCorrectly_1 = """
        {
            "version": "1.0",
            "mappings": [
                {
                    "id": "user-mapping",
                    "source_table": "User",
                    "target_table": "Customer",
                    "direction": "push",
                    "sync_tracking": {
                        "enabled": true,
                        "strategy": "hash",
                        "tracking_column": "_synced_version"
                    }
                }
            ]
        }
        """;

    private const string Data_Parse_MultiTarget_ParsesCorrectly_1 = """
        {
            "version": "1.0",
            "mappings": [
                {
                    "id": "order-split",
                    "source_table": "Order",
                    "direction": "push",
                    "multi_target": true,
                    "targets": [
                        {
                            "table": "OrderHeader",
                            "column_mappings": [
                                {"source": "Id", "target": "OrderId"},
                                {"source": "Total", "target": "Amount"}
                            ]
                        },
                        {
                            "table": "OrderAudit",
                            "column_mappings": [
                                {"source": "Id", "target": "OrderId"},
                                {"source": null, "target": "EventType", "transform": "constant", "value": "created"}
                            ]
                        }
                    ]
                }
            ]
        }
        """;

    private const string Data_Parse_DirectionPull_ParsesCorrectly_1 = """
        {
            "version": "1.0",
            "mappings": [
                {
                    "id": "pull-mapping",
                    "source_table": "Customer",
                    "target_table": "User",
                    "direction": "pull"
                }
            ]
        }
        """;

    private const string Data_Parse_DirectionBoth_ParsesCorrectly_1 = """
        {
            "version": "1.0",
            "mappings": [
                {
                    "id": "bi-dir-mapping",
                    "source_table": "User",
                    "target_table": "Customer",
                    "direction": "both"
                }
            ]
        }
        """;

    private const string Data_Parse_UnmappedTableBehaviorPassthrough_ParsesCorrectly_1 = """
        {
            "version": "1.0",
            "unmapped_table_behavior": "passthrough",
            "mappings": []
        }
        """;

    private const string Data_Parse_MissingRequiredFields_SkipsInvalidMapping_1 = """
        {
            "version": "1.0",
            "mappings": [
                {"id": "", "source_table": ""},
                {"id": "valid", "source_table": "User", "target_table": "Customer", "direction": "push"}
            ]
        }
        """;

    private const string Data_ToJson_RoundTrip_PreservesData_1 = """
        {
            "version": "1.0",
            "unmapped_table_behavior": "strict",
            "mappings": [
                {
                    "id": "test-mapping",
                    "source_table": "User",
                    "target_table": "Customer",
                    "direction": "push",
                    "enabled": true,
                    "column_mappings": [
                        {"source": "Name", "target": "FullName"}
                    ]
                }
            ]
        }
        """;

    private const string Data_Parse_FullSpecExample_ParsesCorrectly_1 = """
        {
            "version": "1.0",
            "mappings": [
                {
                    "id": "user-to-customer",
                    "source_table": "User",
                    "target_table": "Customer",
                    "direction": "push",
                    "enabled": true,
                    "pk_mapping": {
                        "source_column": "Id",
                        "target_column": "CustomerId"
                    },
                    "column_mappings": [
                        {
                            "source": "FullName",
                            "target": "Name",
                            "transform": null
                        },
                        {
                            "source": "EmailAddress",
                            "target": "Email",
                            "transform": null
                        },
                        {
                            "source": null,
                            "target": "Source",
                            "transform": "constant",
                            "value": "mobile-app"
                        },
                        {
                            "source": "CreatedAt",
                            "target": "RegisteredDate",
                            "transform": "lql",
                            "lql": "CreatedAt |> dateFormat('yyyy-MM-dd')"
                        }
                    ],
                    "excluded_columns": ["PasswordHash", "SecurityStamp"],
                    "filter": {
                        "lql": "IsActive = true AND DeletedAt IS NULL"
                    },
                    "sync_tracking": {
                        "enabled": true,
                        "tracking_column": "_synced_version",
                        "strategy": "version"
                    }
                }
            ]
        }
        """;
}
