// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Pure immutable fixture definitions; assertions and database workflows remain analysed.

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class PostgresUpgradeBlockersTests
{
    private static SchemaDefinition NullableSchema(bool nullable) =>
        SchemaTestData.Schema(
            name: "nullable_upgrade",
            tables:
            [
                SchemaTestData.Table(
                    name: "documents",
                    columns:
                    [
                        Id(),
                        SchemaTestData.Column(
                            name: "notes",
                            type: PortableTypes.Text,
                            isNullable: nullable
                        ),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] }
                ),
            ]
        );

    private static SchemaDefinition ForeignKeySchema(bool hasReferenceColumn) =>
        SchemaTestData.Schema(
            name: "foreign_key_upgrade",
            tables:
            [
                Parent("parents"),
                SchemaTestData.Table(
                    name: "children",
                    columns: hasReferenceColumn ? [Id(), UuidColumn("parent_id")] : [Id()],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
                    foreignKeys: hasReferenceColumn ? [ForeignKey("parent_id", "parents")] : []
                ),
            ]
        );

    private static SchemaDefinition PolicySchema(string predicate) =>
        SchemaTestData.Schema(
            name: "policy_upgrade",
            tables:
            [
                SchemaTestData.Table(
                    name: "documents",
                    columns: [Id(), UuidColumn("owner_id")],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
                    rowLevelSecurity: new RlsPolicySetDefinition
                    {
                        Policies =
                        [
                            SchemaTestData.RlsPolicy(
                                name: "owner_policy",
                                usingSql: $"owner_id {predicate}",
                                withCheckSql: $"owner_id {predicate}"
                            ),
                        ],
                    }
                ),
            ]
        );

    private static TableDefinition Parent(string name) =>
        SchemaTestData.Table(
            name: name,
            columns: [Id()],
            primaryKey: new PrimaryKeyDefinition { Columns = ["id"] }
        );

    private static ForeignKeyDefinition ForeignKey(string column, string table) =>
        SchemaTestData.ForeignKey(
            columns: [column],
            referencedTable: table,
            referencedColumns: ["id"]
        );
}
