// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial record MigrationPlatformRegressionTests
{
    private static SchemaDefinition NullableSchema(
        bool stripeIdNullable,
        bool externalIdNullable
    ) =>
        SchemaTestData.Schema(
            name: "issue109",
            tables:
            [
                ParentTable("tenants"),
                SchemaTestData.Table(
                    name: "topup",
                    columns:
                    [
                        SchemaTestData.Column(
                            name: "id",
                            type: PortableTypes.Uuid,
                            isNullable: false
                        ),
                        SchemaTestData.Column(
                            name: "stripe_payment_intent_id",
                            type: PortableTypes.Text,
                            isNullable: stripeIdNullable
                        ),
                        SchemaTestData.Column(
                            name: "required_receipt_reference",
                            type: PortableTypes.Text,
                            isNullable: false
                        ),
                        SchemaTestData.Column(
                            name: "external_id",
                            type: PortableTypes.Uuid,
                            isNullable: externalIdNullable
                        ),
                        SchemaTestData.Column(
                            name: "optional_note",
                            type: PortableTypes.Text,
                            isNullable: true
                        ),
                        UuidColumn("tenant_id"),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
                    foreignKeys: [ForeignKey("tenant_id", "tenants")]
                ),
            ]
        );

    private static SchemaDefinition ForeignKeySchema(bool parent, bool column, bool fk)
    {
        var endUser = Fixture_ForeignKeySchema_1(
            name: name,
            columns: columns,
            name: name,
            type: type,
            isNullable: isNullable,
            primaryKey: primaryKey
        );
        var events = SchemaTestData.Table(
            name: "usage_events",
            columns: column
                ?
                [
                    SchemaTestData.Column(name: "id", type: PortableTypes.Uuid, isNullable: false),
                    SchemaTestData.Column(name: "end_user_id", type: PortableTypes.Uuid),
                ]
                : [SchemaTestData.Column(name: "id", type: PortableTypes.Uuid, isNullable: false)],
            primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
            foreignKeys: fk ? [EndUserForeignKey()] : []
        );
        return SchemaTestData.Schema(
            name: "issues79_80",
            tables: parent ? [endUser, events] : [events]
        );
    }

    private static ForeignKeyDefinition EndUserForeignKey() =>
        SchemaTestData.ForeignKey(
            columns: ["end_user_id"],
            referencedTable: "tenant_end_users",
            referencedColumns: ["id"],
            onDelete: ForeignKeyAction.SetNull
        );

    private static SchemaDefinition ForeignKeyCleanupSchema(
        bool includeTenantForeignKey,
        bool includeProjectForeignKey,
        bool includeObsoleteTable
    )
    {
        var tenants = ParentTable("tenants");
        var projects = ParentTable("projects");
        var events = SchemaTestData.Table(
            name: "usage_events",
            columns: [UuidColumn("id"), UuidColumn("tenant_id"), UuidColumn("project_id")],
            primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
            foreignKeys: includeTenantForeignKey && includeProjectForeignKey
                    ? [ForeignKey("tenant_id", "tenants"), ForeignKey("project_id", "projects")]
                : includeTenantForeignKey ? [ForeignKey("tenant_id", "tenants")]
                : [ForeignKey("project_id", "projects")]
        );
        var audit = SchemaTestData.Table(
            name: "audit_events",
            columns: [UuidColumn("id"), UuidColumn("tenant_id")],
            primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
            foreignKeys: [ForeignKey("tenant_id", "tenants")]
        );
        return SchemaTestData.Schema(
            name: "issue105",
            tables: includeObsoleteTable
                ? [tenants, projects, events, audit, ParentTable("obsolete_records")]
                : [tenants, projects, events, audit]
        );
    }

    private static TableDefinition ParentTable(string name) =>
        SchemaTestData.Table(
            name: name,
            columns: [UuidColumn("id")],
            primaryKey: new PrimaryKeyDefinition { Columns = ["id"] }
        );

    private static ColumnDefinition UuidColumn(string name) =>
        SchemaTestData.Column(name: name, type: PortableTypes.Uuid, isNullable: false);

    private static ForeignKeyDefinition ForeignKey(string column, string referencedTable) =>
        SchemaTestData.ForeignKey(
            columns: [column],
            referencedTable: referencedTable,
            referencedColumns: ["id"],
            onDelete: ForeignKeyAction.Cascade
        );

    private static SchemaDefinition PolicySchema(bool usingAllowed, bool checkAllowed) =>
        SchemaTestData.Schema(
            name: "issue98",
            tables:
            [
                SchemaTestData.Table(
                    name: "documents",
                    columns: [UuidColumn("id")],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
                    rowLevelSecurity: new RlsPolicySetDefinition
                    {
                        Policies =
                        [
                            SchemaTestData.RlsPolicy(
                                name: "documents_access",
                                operations: [RlsOperation.All],
                                usingLql: usingAllowed ? "id is not null" : "id is null",
                                withCheckLql: checkAllowed ? "id is not null" : "id is null"
                            ),
                        ],
                    }
                ),
            ]
        );
}
