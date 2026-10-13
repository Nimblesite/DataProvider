// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial record MigrationPlatformRegressionTests
{
    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static ColumnDefinition Col(string name, PortableType type, bool isNullable) =>
        SchemaTestData.Column(name: name, type: type, isNullable: isNullable);

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
                        Col("id", PortableTypes.Uuid, isNullable: false),
                        Col(
                            "stripe_payment_intent_id",
                            PortableTypes.Text,
                            isNullable: stripeIdNullable
                        ),
                        Col("required_receipt_reference", PortableTypes.Text, isNullable: false),
                        Col("external_id", PortableTypes.Uuid, isNullable: externalIdNullable),
                        Col("optional_note", PortableTypes.Text, isNullable: true),
                        SchemaTestData.RequiredUuid(name: "tenant_id"),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
                    foreignKeys: [ForeignKey("tenant_id", "tenants")]
                ),
            ]
        );

    private static SchemaDefinition ForeignKeySchema(bool parent, bool column, bool fk)
    {
        var endUser = Fixture_ForeignKeySchema_1();
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
        return Dataset_ForeignKeySchema_1(parent: parent, endUser: endUser, events: events);
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
            columns:
            [
                SchemaTestData.RequiredUuid(name: "id"),
                SchemaTestData.RequiredUuid(name: "tenant_id"),
                SchemaTestData.RequiredUuid(name: "project_id"),
            ],
            primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
            foreignKeys: includeTenantForeignKey && includeProjectForeignKey
                    ? [ForeignKey("tenant_id", "tenants"), ForeignKey("project_id", "projects")]
                : includeTenantForeignKey ? [ForeignKey("tenant_id", "tenants")]
                : [ForeignKey("project_id", "projects")]
        );
        var audit = SchemaTestData.Table(
            name: "audit_events",
            columns:
            [
                SchemaTestData.RequiredUuid(name: "id"),
                SchemaTestData.RequiredUuid(name: "tenant_id"),
            ],
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
            columns: [SchemaTestData.RequiredUuid(name: "id")],
            primaryKey: new PrimaryKeyDefinition { Columns = ["id"] }
        );

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
                    columns: [SchemaTestData.RequiredUuid(name: "id")],
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
