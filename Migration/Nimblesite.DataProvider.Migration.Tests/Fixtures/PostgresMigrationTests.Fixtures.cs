// Implements [CI-DESLOP-INDEPENDENT-DATA].
using Tests.Shared;

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresMigrationTests
{
    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_CreateDatabaseFromScratch_SingleTable_Success_1(
        global::System.Boolean unique
    ) =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("email", PortableTypes.VarChar(255), c => c.NotNull())
                        .Column("name", PortableTypes.VarChar(100))
                        .Index("idx_users_email", "email", unique: true)
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_CreateDatabaseFromScratch_MultipleTablesWithForeignKeys_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "customers",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("email", PortableTypes.VarChar(255), c => c.NotNull())
            )
            .Table(
                "public",
                "invoices",
                t =>
                    t.Column("id", PortableTypes.BigInt, c => c.PrimaryKey().Identity())
                        .Column("customer_id", PortableTypes.Uuid, c => c.NotNull())
                        .Column("total", PortableTypes.Decimal(12, 2), c => c.NotNull())
                        .Column(
                            "created_at",
                            PortableTypes.DateTimeOffset,
                            c => c.NotNull().Default("CURRENT_TIMESTAMP")
                        )
                        .ForeignKey("customer_id", "customers", "id", ForeignKeyAction.Cascade)
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_UpgradeExistingDatabase_AddColumn_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "products",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(200))
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_UpgradeExistingDatabase_AddColumn_Success_2() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "products",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(200))
                        .Column("price", PortableTypes.Decimal(10, 2))
                        .Column("sku", PortableTypes.VarChar(50))
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_UpgradeExistingDatabase_AddTable_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "categories",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey().Identity())
                        .Column("name", PortableTypes.VarChar(100))
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_UpgradeExistingDatabase_AddTable_Success_2() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "categories",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey().Identity())
                        .Column("name", PortableTypes.VarChar(100))
            )
            .Table(
                "public",
                "items",
                t =>
                    t.Column("id", PortableTypes.BigInt, c => c.PrimaryKey().Identity())
                        .Column("category_id", PortableTypes.Int, c => c.NotNull())
                        .Column("title", PortableTypes.VarChar(300), c => c.NotNull())
                        .ForeignKey("category_id", "categories", "id")
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_UpgradeExistingDatabase_AddIndex_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "logs",
                t =>
                    t.Column("id", PortableTypes.BigInt, c => c.PrimaryKey().Identity())
                        .Column("message", PortableTypes.Text)
                        .Column("level", PortableTypes.VarChar(20))
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_UpgradeExistingDatabase_AddIndex_Success_2() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "logs",
                t =>
                    t.Column("id", PortableTypes.BigInt, c => c.PrimaryKey().Identity())
                        .Column("message", PortableTypes.Text)
                        .Column("level", PortableTypes.VarChar(20))
                        .Index("idx_logs_level", "level")
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Migration_IsIdempotent_NoErrorOnRerun_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "settings",
                t =>
                    t.Column("key", PortableTypes.VarChar(100), c => c.PrimaryKey())
                        .Column("value", PortableTypes.Text)
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_CreateTable_PostgresNativeTypes_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "type_test",
                t =>
                    t.Column("id", PortableTypes.BigInt, c => c.PrimaryKey())
                        .Column("uuid_col", PortableTypes.Uuid)
                        .Column("json_col", PortableTypes.Json)
                        .Column("bool_col", PortableTypes.Boolean)
                        .Column("timestamp_col", PortableTypes.DateTimeOffset)
                        .Column("decimal_col", PortableTypes.Decimal(18, 4))
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_CreateTable_MixedCaseColumnCheckConstraint_PreservesIdentifierCase_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "fhir_patient",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column(
                            "Gender",
                            PortableTypes.Text,
                            c => c.NotNull().Check("\"Gender\" IN ('male', 'female', 'other')")
                        )
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_ExpressionIndex_CreateWithLowerFunction_Success_1(
        global::System.Boolean unique
    ) =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "artists",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(200), c => c.NotNull())
                        .ExpressionIndex("uq_artists_name", "lower(name)", unique: true)
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_ExpressionIndex_EnforcesCaseInsensitiveUniqueness_1(
        global::System.Boolean unique
    ) =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "venues",
                t =>
                    t.Column(
                            "id",
                            PortableTypes.Uuid,
                            c => c.PrimaryKey().Default("gen_random_uuid()")
                        )
                        .Column("name", PortableTypes.VarChar(200), c => c.NotNull())
                        .ExpressionIndex("uq_venues_name", "lower(name)", unique: true)
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_ExpressionIndex_MultiExpression_CompositeIndexSuccess_1(
        global::System.Boolean unique
    ) =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "suburbs",
                t =>
                    t.Column(
                            "id",
                            PortableTypes.Uuid,
                            c => c.PrimaryKey().Default("gen_random_uuid()")
                        )
                        .Column("name", PortableTypes.VarChar(100), c => c.NotNull())
            )
            .Table(
                "public",
                "places",
                t =>
                    t.Column(
                            "id",
                            PortableTypes.Uuid,
                            c => c.PrimaryKey().Default("gen_random_uuid()")
                        )
                        .Column("name", PortableTypes.VarChar(200), c => c.NotNull())
                        .Column("suburb_id", PortableTypes.Uuid, c => c.NotNull())
                        .ForeignKey("suburb_id", "suburbs", "id")
                        .ExpressionIndex(
                            "uq_places_name_suburb",
                            ["lower(name)", "suburb_id"],
                            unique: true
                        )
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_ExpressionIndex_MultiExpression_AllowsSameNameDifferentSuburb_1(
        global::System.Boolean unique
    ) =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "regions",
                t =>
                    t.Column(
                            "id",
                            PortableTypes.Uuid,
                            c => c.PrimaryKey().Default("gen_random_uuid()")
                        )
                        .Column("name", PortableTypes.VarChar(100), c => c.NotNull())
            )
            .Table(
                "public",
                "locations",
                t =>
                    t.Column(
                            "id",
                            PortableTypes.Uuid,
                            c => c.PrimaryKey().Default("gen_random_uuid()")
                        )
                        .Column("name", PortableTypes.VarChar(200), c => c.NotNull())
                        .Column("region_id", PortableTypes.Uuid, c => c.NotNull())
                        .ForeignKey("region_id", "regions", "id")
                        .ExpressionIndex(
                            "uq_locations_name_region",
                            ["lower(name)", "region_id"],
                            unique: true
                        )
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_ExpressionIndex_Idempotent_NoErrorOnRerun_1(
        global::System.Boolean unique
    ) =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "bands",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(200), c => c.NotNull())
                        .ExpressionIndex("uq_bands_name", "lower(name)", unique: true)
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_UpgradeIndex_ColumnToExpression_RequiresDropAndCreate_1(
        global::System.Boolean unique
    ) =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "artists",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(200), c => c.NotNull())
                        .Index("idx_artists_name", "name", unique: true)
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_UpgradeIndex_ColumnToExpression_RequiresDropAndCreate_2(
        global::System.Boolean unique
    ) =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "artists",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(200), c => c.NotNull())
                        .ExpressionIndex("uq_artists_name_ci", "lower(name)", unique: true)
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_UpgradeIndex_ExpressionToColumn_RequiresDropAndCreate_1(
        global::System.Boolean unique
    ) =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "venues",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(200), c => c.NotNull())
                        .ExpressionIndex("uq_venues_name", "lower(name)", unique: true)
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_UpgradeIndex_ExpressionToColumn_RequiresDropAndCreate_2(
        global::System.Boolean unique
    ) =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "venues",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(200), c => c.NotNull())
                        .Index("idx_venues_name", "name", unique: true)
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_UpgradeIndex_SameNameDifferentType_NotDetectedWithoutDestructive_1(
        global::System.Boolean unique
    ) =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "products",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(200), c => c.NotNull())
                        .Index("idx_products_name", "name", unique: true)
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_UpgradeIndex_SameNameDifferentType_NotDetectedWithoutDestructive_2(
        global::System.Boolean unique
    ) =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "products",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(200), c => c.NotNull())
                        .ExpressionIndex("idx_products_name", "lower(name)", unique: true)
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Destructive_DropTable_AllowedWithOption_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "keepers",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Table("public", "dropme", t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Destructive_DropTable_AllowedWithOption_2() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "keepers",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlDefault_NowFunction_GeneratesCurrentTimestamp_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "events",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column(
                            "created_at",
                            PortableTypes.DateTimeOffset,
                            c => c.NotNull().DefaultLql("now()")
                        )
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlDefault_GenUuidFunction_GeneratesValidUuid_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "items",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey().DefaultLql("gen_uuid()"))
                        .Column("name", PortableTypes.VarChar(100), c => c.NotNull())
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlDefault_BooleanTrue_GeneratesCorrectValue_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "flags",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
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

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlDefault_NumericLiterals_GeneratesCorrectValues_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "counters",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("count", PortableTypes.Int, c => c.NotNull().DefaultLql("0"))
                        .Column(
                            "score",
                            PortableTypes.Decimal(10, 2),
                            c => c.NotNull().DefaultLql("100")
                        )
                        .Column("rate", PortableTypes.Double, c => c.NotNull().DefaultLql("0.5"))
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlDefault_StringLiteral_GeneratesCorrectValue_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "statuses",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column(
                            "status",
                            PortableTypes.VarChar(50),
                            c => c.NotNull().DefaultLql("'pending'")
                        )
                        .Column(
                            "category",
                            PortableTypes.VarChar(50),
                            c => c.NotNull().DefaultLql("'default'")
                        )
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlDefault_AllTypesInOneTable_WorksTogether_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "comprehensive_defaults",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey().DefaultLql("gen_uuid()"))
                        .Column("name", PortableTypes.VarChar(100), c => c.NotNull())
                        .Column(
                            "created_at",
                            PortableTypes.DateTimeOffset,
                            c => c.NotNull().DefaultLql("now()")
                        )
                        .Column(
                            "is_active",
                            PortableTypes.Boolean,
                            c => c.NotNull().DefaultLql("true")
                        )
                        .Column(
                            "is_archived",
                            PortableTypes.Boolean,
                            c => c.NotNull().DefaultLql("false")
                        )
                        .Column("count", PortableTypes.Int, c => c.NotNull().DefaultLql("0"))
                        .Column("priority", PortableTypes.Int, c => c.NotNull().DefaultLql("5"))
                        .Column(
                            "status",
                            PortableTypes.VarChar(20),
                            c => c.NotNull().DefaultLql("'active'")
                        )
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_CreateTableWithVectorColumn_MigratesAgainstPgvectorContainer_Success_1() =>
        Schema
            .Define("Embeddings")
            .Table(
                "public",
                "documents",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("body", PortableTypes.Text, c => c.NotNull())
                        .Column("embedding", PortableTypes.Vector(384), c => c.NotNull())
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_CreateTableWithVectorColumn_OpenAiLargeDim_Success_1() =>
        Schema
            .Define("EmbeddingsLarge")
            .Table(
                "public",
                "large_embeddings",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("embedding", PortableTypes.Vector(3072), c => c.NotNull())
            )
            .Build();
}
