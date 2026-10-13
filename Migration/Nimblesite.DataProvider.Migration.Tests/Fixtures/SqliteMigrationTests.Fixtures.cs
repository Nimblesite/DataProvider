// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SqliteMigrationTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_CreateDatabaseFromScratch_SingleTable_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "Users",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Email", PortableTypes.NVarChar(255), c => c.NotNull())
                        .Column("Name", PortableTypes.NVarChar(100))
                        .Index("idx_users_email", "Email", unique: true)
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_CreateDatabaseFromScratch_MultipleTablesWithForeignKeys_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "Users",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Email", PortableTypes.NVarChar(255), c => c.NotNull())
            )
            .Table(
                "Orders",
                t =>
                    t.Column("Id", PortableTypes.BigInt, c => c.PrimaryKey().Identity())
                        .Column("UserId", PortableTypes.Uuid, c => c.NotNull())
                        .Column("Total", PortableTypes.Decimal(10, 2), c => c.NotNull())
                        .Column(
                            "CreatedAt",
                            PortableTypes.DateTime(),
                            c => c.NotNull().Default("CURRENT_TIMESTAMP")
                        )
                        .ForeignKey("UserId", "Users", "Id", ForeignKeyAction.Cascade)
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_UpgradeExistingDatabase_AddColumn_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "Users",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Email", PortableTypes.NVarChar(255))
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_UpgradeExistingDatabase_AddColumn_Success_2() =>
        Schema
            .Define("Test")
            .Table(
                "Users",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Email", PortableTypes.NVarChar(255))
                        .Column("Name", PortableTypes.NVarChar(100))
                        .Column("CreatedAt", PortableTypes.DateTime())
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_UpgradeExistingDatabase_AddTable_Success_1() =>
        Schema
            .Define("Test")
            .Table("Users", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Table(
                "Products",
                t =>
                    t.Column("Id", PortableTypes.BigInt, c => c.PrimaryKey().Identity())
                        .Column("Name", PortableTypes.NVarChar(200), c => c.NotNull())
                        .Column("Price", PortableTypes.Decimal(10, 2))
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_UpgradeExistingDatabase_AddIndex_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "Users",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Email", PortableTypes.NVarChar(255))
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_UpgradeExistingDatabase_AddIndex_Success_2() =>
        Schema
            .Define("Test")
            .Table(
                "Users",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Email", PortableTypes.NVarChar(255))
                        .Index("idx_users_email", "Email", unique: true)
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Migration_IsIdempotent_NoErrorOnRerun_1() =>
        Schema
            .Define("Test")
            .Table(
                "Items",
                t =>
                    t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("Name", PortableTypes.NVarChar(50))
                        .Index("idx_items_name", "Name")
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_CreateTable_AllPortableTypes_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "TypeTest",
                t =>
                    t.Column("Id", PortableTypes.BigInt, c => c.PrimaryKey())
                        .Column("TinyInt", PortableTypes.TinyInt)
                        .Column("SmallInt", PortableTypes.SmallInt)
                        .Column("Int", PortableTypes.Int)
                        .Column("BigInt", PortableTypes.BigInt)
                        .Column("Decimal", PortableTypes.Decimal(18, 2))
                        .Column("Float", PortableTypes.Float)
                        .Column("Double", PortableTypes.Double)
                        .Column("Money", PortableTypes.Money)
                        .Column("Bool", PortableTypes.Boolean)
                        .Column("Char", PortableTypes.Char(10))
                        .Column("VarChar", PortableTypes.VarChar(50))
                        .Column("NChar", PortableTypes.NChar(10))
                        .Column("NVarChar", PortableTypes.NVarChar(100))
                        .Column("Text", PortableTypes.Text)
                        .Column("Binary", PortableTypes.Binary(16))
                        .Column("VarBinary", PortableTypes.VarBinary(256))
                        .Column("Blob", PortableTypes.Blob)
                        .Column("Date", PortableTypes.Date)
                        .Column("Time", PortableTypes.Time())
                        .Column("DateTime", PortableTypes.DateTime())
                        .Column("DateTimeOffset", PortableTypes.DateTimeOffset)
                        .Column("Uuid", PortableTypes.Uuid)
                        .Column("Json", PortableTypes.Json)
                        .Column("Xml", PortableTypes.Xml)
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_SchemaInspector_RoundTrip_Matches_1() =>
        Schema
            .Define("Test")
            .Table(
                "Users",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Email", PortableTypes.NVarChar(255), c => c.NotNull())
                        .Column("Active", PortableTypes.Boolean, c => c.Default("1"))
                        .Index("idx_users_email", "Email", unique: true)
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_ExpressionIndex_CreateWithLowerFunction_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "Artists",
                t =>
                    SchemaTestColumns
                        .IdAndName(
                            table: t,
                            idType: PortableTypes.Uuid,
                            nameType: PortableTypes.VarChar(200)
                        )
                        .ExpressionIndex("uq_artists_name", "lower(Name)", unique: true)
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_ExpressionIndex_EnforcesCaseInsensitiveUniqueness_1() =>
        Schema
            .Define("Test")
            .Table(
                "Venues",
                t =>
                    SchemaTestColumns
                        .IdAndName(
                            table: t,
                            idType: PortableTypes.Uuid,
                            nameType: PortableTypes.VarChar(200)
                        )
                        .ExpressionIndex("uq_venues_name", "lower(Name)", unique: true)
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_ExpressionIndex_MultiExpression_CompositeIndexSuccess_1() =>
        Schema
            .Define("Test")
            .Table(
                "Suburbs",
                t =>
                    SchemaTestColumns.IdAndName(
                        table: t,
                        idType: PortableTypes.Uuid,
                        nameType: PortableTypes.VarChar(100)
                    )
            )
            .Table(
                "Places",
                t =>
                    SchemaTestColumns
                        .IdAndName(
                            table: t,
                            idType: PortableTypes.Uuid,
                            nameType: PortableTypes.VarChar(200)
                        )
                        .Column("SuburbId", PortableTypes.Uuid, c => c.NotNull())
                        .ExpressionIndex(
                            "uq_places_name_suburb",
                            ["lower(Name)", "SuburbId"],
                            unique: true
                        )
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_ExpressionIndex_Idempotent_NoErrorOnRerun_1() =>
        Schema
            .Define("Test")
            .Table(
                "Bands",
                t =>
                    SchemaTestColumns
                        .IdAndName(
                            table: t,
                            idType: PortableTypes.Uuid,
                            nameType: PortableTypes.VarChar(200)
                        )
                        .ExpressionIndex("uq_bands_name", "lower(Name)", unique: true)
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_UpgradeIndex_ColumnToExpression_RequiresDropAndCreate_1() =>
        Schema
            .Define("Test")
            .Table(
                "Artists",
                t =>
                    SchemaTestColumns
                        .IdAndName(
                            table: t,
                            idType: PortableTypes.Uuid,
                            nameType: PortableTypes.VarChar(200)
                        )
                        .Index("idx_artists_name", "Name", unique: true)
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_UpgradeIndex_ColumnToExpression_RequiresDropAndCreate_2() =>
        Schema
            .Define("Test")
            .Table(
                "Artists",
                t =>
                    SchemaTestColumns
                        .IdAndName(
                            table: t,
                            idType: PortableTypes.Uuid,
                            nameType: PortableTypes.VarChar(200)
                        )
                        .ExpressionIndex("uq_artists_name_ci", "lower(Name)", unique: true)
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_UpgradeIndex_ExpressionToColumn_RequiresDropAndCreate_1() =>
        Schema
            .Define("Test")
            .Table(
                "Venues",
                t =>
                    SchemaTestColumns
                        .IdAndName(
                            table: t,
                            idType: PortableTypes.Uuid,
                            nameType: PortableTypes.VarChar(200)
                        )
                        .ExpressionIndex("uq_venues_name", "lower(Name)", unique: true)
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_UpgradeIndex_ExpressionToColumn_RequiresDropAndCreate_2() =>
        Schema
            .Define("Test")
            .Table(
                "Venues",
                t =>
                    SchemaTestColumns
                        .IdAndName(
                            table: t,
                            idType: PortableTypes.Uuid,
                            nameType: PortableTypes.VarChar(200)
                        )
                        .Index("idx_venues_name", "Name", unique: true)
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlDefault_NowFunction_TranslatesToCurrentTimestamp_1() =>
        Schema
            .Define("Test")
            .Table(
                "events",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column(
                            "created_at",
                            PortableTypes.DateTime(),
                            c => c.NotNull().DefaultLql("now()")
                        )
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlDefault_BooleanTrue_TranslatesTo1_1() =>
        Schema
            .Define("Test")
            .Table(
                "flags",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column(
                            "is_active",
                            PortableTypes.Boolean,
                            c => c.NotNull().DefaultLql("true")
                        )
                        .Column(
                            "is_deleted",
                            PortableTypes.Boolean,
                            c => c.NotNull().DefaultLql("false")
                        )
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlDefault_NumericValues_PassThrough_1() =>
        Schema
            .Define("Test")
            .Table(
                "counters",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("count", PortableTypes.Int, c => c.NotNull().DefaultLql("0"))
                        .Column("priority", PortableTypes.Int, c => c.NotNull().DefaultLql("100"))
                        .Column(
                            "rate",
                            PortableTypes.Decimal(5, 2),
                            c => c.NotNull().DefaultLql("1.5")
                        )
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlDefault_StringLiteral_PassThrough_1() =>
        Schema
            .Define("Test")
            .Table(
                "items",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column(
                            "status",
                            PortableTypes.VarChar(20),
                            c => c.NotNull().DefaultLql("'pending'")
                        )
                        .Column(
                            "category",
                            PortableTypes.VarChar(50),
                            c => c.DefaultLql("'uncategorized'")
                        )
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlDefault_GenUuid_GeneratesValidUuidFormat_1() =>
        Schema
            .Define("Test")
            .Table(
                "records",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey().DefaultLql("gen_uuid()"))
                        .Column("name", PortableTypes.VarChar(100))
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlDefault_CurrentDate_ReturnsDateOnly_1() =>
        Schema
            .Define("Test")
            .Table(
                "logs",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column(
                            "log_date",
                            PortableTypes.Date,
                            c => c.NotNull().DefaultLql("current_date()")
                        )
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlDefault_MixedDefaults_AllWorkTogether_1() =>
        Schema
            .Define("Test")
            .Table(
                "orders",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey().DefaultLql("gen_uuid()"))
                        .Column(
                            "status",
                            PortableTypes.VarChar(20),
                            c => c.NotNull().DefaultLql("'pending'")
                        )
                        .Column("quantity", PortableTypes.Int, c => c.NotNull().DefaultLql("1"))
                        .Column(
                            "is_urgent",
                            PortableTypes.Boolean,
                            c => c.NotNull().DefaultLql("false")
                        )
                        .Column(
                            "created_at",
                            PortableTypes.DateTime(),
                            c => c.NotNull().DefaultLql("now()")
                        )
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_CreateTableWithVectorColumn_FallsBackToBlob_AndMigratesSuccessfully_1() =>
        Schema
            .Define("Embeddings")
            .Table(
                "documents",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Body", PortableTypes.Text, c => c.NotNull())
                        .Column("Embedding", PortableTypes.Vector(384), c => c.NotNull())
            )
            .Build();
}
