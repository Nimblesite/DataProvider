namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial record MigrationPlatformRegressionTests
{
    private static SchemaDefinition NullableSchema(
        bool stripeIdNullable,
        bool externalIdNullable
    ) =>
        new()
        {
            Name = "issue109",
            Tables =
            [
                ParentTable("tenants"),
                new TableDefinition
                {
                    Name = "topup",
                    Columns =
                    [
                        new ColumnDefinition
                        {
                            Name = "id",
                            Type = PortableTypes.Uuid,
                            IsNullable = false,
                        },
                        new ColumnDefinition
                        {
                            Name = "stripe_payment_intent_id",
                            Type = PortableTypes.Text,
                            IsNullable = stripeIdNullable,
                        },
                        new ColumnDefinition
                        {
                            Name = "required_receipt_reference",
                            Type = PortableTypes.Text,
                            IsNullable = false,
                        },
                        new ColumnDefinition
                        {
                            Name = "external_id",
                            Type = PortableTypes.Uuid,
                            IsNullable = externalIdNullable,
                        },
                        new ColumnDefinition
                        {
                            Name = "optional_note",
                            Type = PortableTypes.Text,
                            IsNullable = true,
                        },
                        UuidColumn("tenant_id"),
                    ],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                    ForeignKeys = [ForeignKey("tenant_id", "tenants")],
                },
            ],
        };

    private static SchemaDefinition ForeignKeySchema(bool parent, bool column, bool fk)
    {
        var endUser = new TableDefinition
        {
            Name = "tenant_end_users",
            Columns =
            [
                new ColumnDefinition
                {
                    Name = "id",
                    Type = PortableTypes.Uuid,
                    IsNullable = false,
                },
            ],
            PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
        };
        var events = new TableDefinition
        {
            Name = "usage_events",
            Columns = column
                ?
                [
                    new ColumnDefinition
                    {
                        Name = "id",
                        Type = PortableTypes.Uuid,
                        IsNullable = false,
                    },
                    new ColumnDefinition { Name = "end_user_id", Type = PortableTypes.Uuid },
                ]
                :
                [
                    new ColumnDefinition
                    {
                        Name = "id",
                        Type = PortableTypes.Uuid,
                        IsNullable = false,
                    },
                ],
            PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
            ForeignKeys = fk ? [EndUserForeignKey()] : [],
        };
        return new SchemaDefinition
        {
            Name = "issues79_80",
            Tables = parent ? [endUser, events] : [events],
        };
    }

    private static ForeignKeyDefinition EndUserForeignKey() =>
        new()
        {
            Columns = ["end_user_id"],
            ReferencedTable = "tenant_end_users",
            ReferencedColumns = ["id"],
            OnDelete = ForeignKeyAction.SetNull,
        };

    private static SchemaDefinition ForeignKeyCleanupSchema(
        bool includeTenantForeignKey,
        bool includeProjectForeignKey,
        bool includeObsoleteTable
    )
    {
        var tenants = ParentTable("tenants");
        var projects = ParentTable("projects");
        var events = new TableDefinition
        {
            Name = "usage_events",
            Columns = [UuidColumn("id"), UuidColumn("tenant_id"), UuidColumn("project_id")],
            PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
            ForeignKeys =
                includeTenantForeignKey && includeProjectForeignKey
                    ? [ForeignKey("tenant_id", "tenants"), ForeignKey("project_id", "projects")]
                : includeTenantForeignKey ? [ForeignKey("tenant_id", "tenants")]
                : [ForeignKey("project_id", "projects")],
        };
        var audit = new TableDefinition
        {
            Name = "audit_events",
            Columns = [UuidColumn("id"), UuidColumn("tenant_id")],
            PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
            ForeignKeys = [ForeignKey("tenant_id", "tenants")],
        };
        return new SchemaDefinition
        {
            Name = "issue105",
            Tables = includeObsoleteTable
                ? [tenants, projects, events, audit, ParentTable("obsolete_records")]
                : [tenants, projects, events, audit],
        };
    }

    private static TableDefinition ParentTable(string name) =>
        new()
        {
            Name = name,
            Columns = [UuidColumn("id")],
            PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
        };

    private static ColumnDefinition UuidColumn(string name) =>
        new()
        {
            Name = name,
            Type = PortableTypes.Uuid,
            IsNullable = false,
        };

    private static ForeignKeyDefinition ForeignKey(string column, string referencedTable) =>
        new()
        {
            Columns = [column],
            ReferencedTable = referencedTable,
            ReferencedColumns = ["id"],
            OnDelete = ForeignKeyAction.Cascade,
        };

    private static SchemaDefinition PolicySchema(bool usingAllowed, bool checkAllowed) =>
        new()
        {
            Name = "issue98",
            Tables =
            [
                new TableDefinition
                {
                    Name = "documents",
                    Columns = [UuidColumn("id")],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                    RowLevelSecurity = new RlsPolicySetDefinition
                    {
                        Policies =
                        [
                            new RlsPolicyDefinition
                            {
                                Name = "documents_access",
                                Operations = [RlsOperation.All],
                                UsingLql = usingAllowed ? "id is not null" : "id is null",
                                WithCheckLql = checkAllowed ? "id is not null" : "id is null",
                            },
                        ],
                    },
                },
            ],
        };
}
