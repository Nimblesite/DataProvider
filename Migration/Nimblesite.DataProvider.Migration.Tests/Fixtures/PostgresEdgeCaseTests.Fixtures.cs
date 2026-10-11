// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresEdgeCaseTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_NullableColumn_InsertNull_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "nullable_test",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(100))
                        .Column("email", PortableTypes.VarChar(255))
                        .Column("age", PortableTypes.Int)
                        .Column("balance", PortableTypes.Decimal(18, 2))
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_AddNullableColumn_ExistingDataUnaffected_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "evolving_table",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(100))
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_AddNullableColumn_ExistingDataUnaffected_Success_2() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "evolving_table",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(100))
                        .Column("email", PortableTypes.VarChar(255))
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_DefaultValue_NegativeNumber_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "negative_defaults",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("balance", PortableTypes.Decimal(10, 2), c => c.Default("-100.50"))
                        .Column("adjustment", PortableTypes.Int, c => c.Default("-1")) // Not "offset" - reserved word
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_DefaultValue_EmptyString_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "empty_defaults",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("notes", PortableTypes.VarChar(500), c => c.Default("''"))
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_DefaultValue_BooleanTrue_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "bool_defaults",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("is_active", PortableTypes.Boolean, c => c.Default("true"))
                        .Column("is_deleted", PortableTypes.Boolean, c => c.Default("false"))
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_DefaultValue_UuidGeneration_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "uuid_defaults",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column(
                            "external_id",
                            PortableTypes.Uuid,
                            c => c.Default("gen_random_uuid()")
                        )
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_UnicodeData_InsertAndRetrieve_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "unicode_test",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("name", PortableTypes.NVarChar(200))
                        .Column("description", PortableTypes.Text)
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_SpecialChars_InData_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "special_chars",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("value", PortableTypes.Text)
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_JsonColumn_StoreAndQuery_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "json_test",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("data", PortableTypes.Json)
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_JsonColumn_NullVsJsonNull_Distinction_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "json_null_test",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("data", PortableTypes.Json)
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Decimal_ExtremeValues_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "decimal_extremes",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("tiny", PortableTypes.Decimal(5, 4))
                        .Column("large", PortableTypes.Decimal(18, 0)) // Fits in .NET decimal
                        .Column("precise", PortableTypes.Decimal(18, 8))
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Decimal_ZeroAndNegative_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "decimal_zero_neg",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("zero", PortableTypes.Decimal(10, 2))
                        .Column("negative", PortableTypes.Decimal(10, 2))
                        .Column("negative_small", PortableTypes.Decimal(10, 8))
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Timestamp_ExtremeValues_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "timestamp_extremes",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("past", PortableTypes.DateTimeOffset)
                        .Column("future", PortableTypes.DateTimeOffset)
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Index_VeryLongName_Success_1(
        global::System.String longName
    ) =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "long_index_name",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("value", PortableTypes.VarChar(100))
                        .Index(longName, "value")
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Index_AllColumnsInTable_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "all_indexed",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("a", PortableTypes.VarChar(50))
                        .Column("b", PortableTypes.VarChar(50))
                        .Column("c", PortableTypes.VarChar(50))
                        .Index("idx_all", ["a", "b", "c"])
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_ForeignKey_AllCascadeTypes_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "parent_table",
                t => t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
            )
            .Table(
                "public",
                "child_cascade",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("parent_id", PortableTypes.Int)
                        .ForeignKey("parent_id", "parent_table", "id", ForeignKeyAction.Cascade)
            )
            .Table(
                "public",
                "child_setnull",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("parent_id", PortableTypes.Int)
                        .ForeignKey("parent_id", "parent_table", "id", ForeignKeyAction.SetNull)
            )
            .Table(
                "public",
                "child_restrict",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("parent_id", PortableTypes.Int)
                        .ForeignKey("parent_id", "parent_table", "id", ForeignKeyAction.Restrict)
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_MultipleOperations_CorrectOrder_Success_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "ordered_ops",
                t => t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_MultipleOperations_CorrectOrder_Success_2() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "ordered_ops",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(100))
                        .Column("email", PortableTypes.VarChar(255))
                        .Index("idx_name", "name")
                        .Index("idx_email", "email", unique: true)
            )
            .Table(
                "public",
                "related",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("ordered_id", PortableTypes.Int)
                        .ForeignKey("ordered_id", "ordered_ops", "id")
            )
            .Build();
}
