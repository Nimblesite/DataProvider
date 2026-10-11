// Implements [CI-DESLOP-INDEPENDENT-DATA].


namespace Nimblesite.DataProvider.Migration.Tests;

public partial class MigrationCornerCaseTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_TableName_WithUnderscores_Success_1() =>
        Schema
                .Define("Test")
                .Table(
                    "user_roles_history",
                    t =>
                        t.Column("id", PortableTypes.BigInt, c => c.PrimaryKey())
                            .Column("user_id", PortableTypes.Uuid)
                            .Column("role_name", PortableTypes.VarChar(100))
                )
                .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_ColumnName_IsReservedWord_HandledCorrectly_1() =>
        Schema
                .Define("Test")
                .Table(
                    "DataTable",
                    t =>
                        t.Column("index", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("order", PortableTypes.Int)
                            .Column("group", PortableTypes.VarChar(50))
                            .Column("select", PortableTypes.Text)
                            .Column("where", PortableTypes.Boolean)
                            .Column("from", PortableTypes.DateTime())
                            .Column("table", PortableTypes.VarChar(100))
                )
                .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_TableName_CamelCase_PreservedCorrectly_1() =>
        Schema
                .Define("Test")
                .Table(
                    "UserAccountSettings",
                    t =>
                        t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                            .Column("UserId", PortableTypes.Uuid)
                            .Column("EnableNotifications", PortableTypes.Boolean)
                )
                .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_ColumnName_WithNumbers_Success_1() =>
        Schema
                .Define("Test")
                .Table(
                    "Metrics",
                    t =>
                        t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("value1", PortableTypes.Decimal(10, 2))
                            .Column("value2", PortableTypes.Decimal(10, 2))
                            .Column("metric99", PortableTypes.Float)
                )
                .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Table_ManyColumns_Success_1() =>
        Schema
                .Define("Test")
                .Table(
                    "WideTable",
                    t =>
                    {
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey());
                        // Add 20 columns (enough to test wide tables)
                        for (var i = 1; i <= 20; i++)
                        {
                            t.Column($"Col{i}", PortableTypes.VarChar(100));
                        }
                    }
                )
                .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Column_MaximumVarCharLength_Success_1() =>
        Schema
                .Define("Test")
                .Table(
                    "LargeText",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("ShortText", PortableTypes.VarChar(10))
                            .Column("MediumText", PortableTypes.VarChar(4000))
                            .Column("LargeText", PortableTypes.VarChar(8000))
                            .Column("MaxText", PortableTypes.Text)
                )
                .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Decimal_ExtremeScaleAndPrecision_Success_1() =>
        Schema
                .Define("Test")
                .Table(
                    "Financials",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("TinyMoney", PortableTypes.Decimal(5, 2))
                            .Column("StandardMoney", PortableTypes.Decimal(10, 2))
                            .Column("BigMoney", PortableTypes.Decimal(18, 4))
                            .Column("HugeMoney", PortableTypes.Decimal(28, 8))
                            .Column("CryptoValue", PortableTypes.Decimal(38, 18))
                )
                .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Table_MultiColumnUniqueConstraint_Success_1() =>
        Schema
                .Define("Test")
                .Table(
                    "CompositeUnique",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("TenantId", PortableTypes.Uuid, c => c.NotNull())
                            .Column("EntityId", PortableTypes.Uuid, c => c.NotNull())
                            .Column("Data", PortableTypes.Text)
                            .Unique("UQ_tenant_entity", "TenantId", "EntityId")
                )
                .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Table_MultiColumnIndex_Success_1() =>
        Schema
                .Define("Test")
                .Table(
                    "Events",
                    t =>
                        t.Column("Id", PortableTypes.BigInt, c => c.PrimaryKey())
                            .Column("TenantId", PortableTypes.Uuid)
                            .Column("EntityType", PortableTypes.VarChar(100))
                            .Column("EntityId", PortableTypes.Uuid)
                            .Column("EventDate", PortableTypes.DateTime())
                            .Index(
                                "idx_events_tenant_entity",
                                ["TenantId", "EntityType", "EntityId"]
                            )
                )
                .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Table_SelfReferencingForeignKey_Success_1() =>
        Schema
                .Define("Test")
                .Table(
                    "Categories",
                    t =>
                        SchemaTestColumns
                            .IdAndName(
                                table: t,
                                idType: PortableTypes.Int,
                                nameType: PortableTypes.VarChar(100)
                            )
                            .Column("ParentId", PortableTypes.Int)
                            .ForeignKey("ParentId", "Categories", "Id", ForeignKeyAction.SetNull)
                )
                .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Table_MultipleIndexesOnSameColumn_Success_1() =>
        Schema
                .Define("Test")
                .Table(
                    "Documents",
                    t =>
                        t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                            .Column("Title", PortableTypes.VarChar(500))
                            .Column("Status", PortableTypes.VarChar(20))
                            .Column("CreatedAt", PortableTypes.DateTime())
                            .Index("idx_docs_title", "Title")
                            .Index("idx_docs_status", "Status")
                            .Index("idx_docs_status_created", ["Status", "CreatedAt"])
                )
                .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_AllColumnsNullable_ExceptPrimaryKey_Success_1() =>
        Schema
                .Define("Test")
                .Table(
                    "OptionalData",
                    t =>
                        t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                            .Column("Name", PortableTypes.VarChar(100))
                            .Column("Email", PortableTypes.VarChar(255))
                            .Column("Age", PortableTypes.Int)
                            .Column("Balance", PortableTypes.Decimal(10, 2))
                            .Column("Active", PortableTypes.Boolean)
                )
                .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_AllColumnsNotNull_Success_1() =>
        Schema
                .Define("Test")
                .Table(
                    "RequiredData",
                    t =>
                        SchemaTestColumns
                            .IdAndName(
                                table: t,
                                idType: PortableTypes.Uuid,
                                nameType: PortableTypes.VarChar(100)
                            )
                            .Column("Email", PortableTypes.VarChar(255), c => c.NotNull())
                            .Column(
                                "Status",
                                PortableTypes.VarChar(20),
                                c => c.NotNull().Default("'active'")
                            )
                )
                .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_DefaultValue_StringWithQuotes_Success_1() =>
        Schema
                .Define("Test")
                .Table(
                    "Defaults",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column(
                                "Status",
                                PortableTypes.VarChar(50),
                                c => c.Default("'pending'")
                            )
                            .Column("Type", PortableTypes.VarChar(50), c => c.Default("'default'"))
                )
                .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_DefaultValue_NumericZero_Success_1() =>
        Schema
                .Define("Test")
                .Table(
                    "Counters",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("Count", PortableTypes.Int, c => c.Default("0"))
                            .Column("Balance", PortableTypes.Decimal(10, 2), c => c.Default("0.00"))
                )
                .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_DefaultValue_BooleanFalse_Success_1() =>
        Schema
                .Define("Test")
                .Table(
                    "Flags",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("IsActive", PortableTypes.Boolean, c => c.Default("0"))
                            .Column("IsVerified", PortableTypes.Boolean, c => c.Default("1"))
                )
                .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_DefaultValue_CurrentTimestamp_Success_1() =>
        Schema
                .Define("Test")
                .Table(
                    "Auditable",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column(
                                "CreatedAt",
                                PortableTypes.DateTime(),
                                c => c.Default("CURRENT_TIMESTAMP")
                            )
                            .Column("UpdatedAt", PortableTypes.DateTime())
                )
                .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_MultipleTables_CircularForeignKeys_DeferredConstraints_1() =>
        Schema
                .Define("Test")
                .Table(
                    "Authors",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("Name", PortableTypes.VarChar(100))
                )
                .Table(
                    "Books",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("Title", PortableTypes.VarChar(200))
                            .Column("AuthorId", PortableTypes.Int)
                            .ForeignKey("AuthorId", "Authors", "Id")
                )
                .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_UpgradeFrom_EmptyTable_ToFullSchema_Success_1() =>
        Schema
                .Define("Test")
                .Table(
                    "Products",
                    t =>
                        SchemaTestColumns
                            .IdAndName(
                                table: t,
                                idType: PortableTypes.Int,
                                nameType: PortableTypes.VarChar(200)
                            )
                            .Column("Price", PortableTypes.Decimal(10, 2))
                            .Column("CategoryId", PortableTypes.Int)
                            .Column("CreatedAt", PortableTypes.DateTime())
                            .Index("idx_products_name", "Name")
                            .Index("idx_products_category", "CategoryId")
                )
                .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_AddIndex_ThenAddAnother_Success_1() =>
        Schema
                .Define("Test")
                .Table(
                    "Items",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("Code", PortableTypes.VarChar(50))
                            .Column("Category", PortableTypes.VarChar(50))
                            .Index("idx_items_code", "Code")
                )
                .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_AddIndex_ThenAddAnother_Success_2() =>
        Schema
                .Define("Test")
                .Table(
                    "Items",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("Code", PortableTypes.VarChar(50))
                            .Column("Category", PortableTypes.VarChar(50))
                            .Index("idx_items_code", "Code")
                            .Index("idx_items_category", "Category")
                )
                .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Table_MultipleIdentityColumns_OnePerTable_1() =>
        Schema
                .Define("Test")
                .Table(
                    "Sequenced",
                    t =>
                        t.Column("Id", PortableTypes.BigInt, c => c.PrimaryKey().Identity())
                            .Column("Name", PortableTypes.VarChar(100))
                            .Column("OrderNum", PortableTypes.Int) // Not identity
                )
                .Build();
}
