// Implements [CI-DESLOP-INDEPENDENT-DATA].
using Tests.Shared;

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class LqlDefaultsTests
{
    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlBoolean_True_WorksOnBothPlatforms_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "settings",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("enabled", PortableTypes.Boolean, c => c.DefaultLql("true"))
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlBoolean_False_WorksOnBothPlatforms_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "flags",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("disabled", PortableTypes.Boolean, c => c.DefaultLql("false"))
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlNow_DefaultsToCurrentTime_BothPlatforms_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "events",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column(
                            "created_at",
                            PortableTypes.DateTimeOffset,
                            c => c.DefaultLql("now()")
                        )
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlCurrentTimestamp_SameAsNow_BothPlatforms_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "logs",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column(
                            "timestamp",
                            PortableTypes.DateTimeOffset,
                            c => c.DefaultLql("current_timestamp()")
                        )
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlCurrentDate_ReturnsDateOnly_BothPlatforms_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "daily_records",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column(
                            "record_date",
                            PortableTypes.Date,
                            c => c.DefaultLql("current_date()")
                        )
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlNumericInteger_DefaultsCorrectly_BothPlatforms_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "counters",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("count", PortableTypes.Int, c => c.DefaultLql("42"))
                        .Column("negative", PortableTypes.Int, c => c.DefaultLql("-100"))
                        .Column("zero", PortableTypes.Int, c => c.DefaultLql("0"))
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlNumericDecimal_DefaultsCorrectly_BothPlatforms_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "prices",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("amount", PortableTypes.Decimal(10, 2), c => c.DefaultLql("99.99"))
                        .Column(
                            "tax_rate",
                            PortableTypes.Decimal(5, 4),
                            c => c.DefaultLql("0.0825")
                        )
                        .Column(
                            "discount",
                            PortableTypes.Decimal(5, 2),
                            c => c.DefaultLql("-10.50")
                        )
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlStringLiteral_DefaultsCorrectly_BothPlatforms_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "statuses",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("status", PortableTypes.VarChar(50), c => c.DefaultLql("'active'"))
                        .Column(
                            "category",
                            PortableTypes.VarChar(100),
                            c => c.DefaultLql("'uncategorized'")
                        )
                        .Column("empty", PortableTypes.VarChar(10), c => c.DefaultLql("''"))
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlGenUuid_GeneratesValidUuid_BothPlatforms_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "entities",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey().DefaultLql("gen_uuid()"))
                        .Column("name", PortableTypes.VarChar(100))
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlUuidAlias_SameAsGenUuid_BothPlatforms_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "items",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey().DefaultLql("uuid()"))
                        .Column("label", PortableTypes.VarChar(50))
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlMultipleDefaults_AllWorkTogether_BothPlatforms_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "audit_records",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey().DefaultLql("gen_uuid()"))
                        .Column("action", PortableTypes.VarChar(100), c => c.NotNull())
                        .Column("is_success", PortableTypes.Boolean, c => c.DefaultLql("true"))
                        .Column("retry_count", PortableTypes.Int, c => c.DefaultLql("0"))
                        .Column("priority", PortableTypes.Decimal(3, 1), c => c.DefaultLql("5.0"))
                        .Column("status", PortableTypes.VarChar(20), c => c.DefaultLql("'pending'"))
                        .Column(
                            "created_at",
                            PortableTypes.DateTimeOffset,
                            c => c.DefaultLql("now()")
                        )
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlDefaults_Idempotent_BothPlatforms_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "rerunnable",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("active", PortableTypes.Boolean, c => c.DefaultLql("true"))
                        .Column("count", PortableTypes.Int, c => c.DefaultLql("1"))
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlNumeric_LargeInteger_SameValueBothPlatforms_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "large_nums",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("big_pos", PortableTypes.BigInt, c => c.DefaultLql("2147483647")) // int32 max
                        .Column("big_neg", PortableTypes.BigInt, c => c.DefaultLql("-2147483648")) // int32 min
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlNumeric_VerySmallDecimal_SameValueBothPlatforms_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "precision_test",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column(
                            "tiny",
                            PortableTypes.Decimal(10, 8),
                            c => c.DefaultLql("0.00000001")
                        )
                        .Column(
                            "scientific",
                            PortableTypes.Decimal(15, 10),
                            c => c.DefaultLql("3.1415926535")
                        )
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlString_SpecialCharacters_SameValueBothPlatforms_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "special_strings",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column(
                            "with_spaces",
                            PortableTypes.VarChar(50),
                            c => c.DefaultLql("'hello world'")
                        )
                        .Column(
                            "with_numbers",
                            PortableTypes.VarChar(50),
                            c => c.DefaultLql("'test123'")
                        )
                        .Column(
                            "with_hyphen",
                            PortableTypes.VarChar(50),
                            c => c.DefaultLql("'foo-bar-baz'")
                        )
                        .Column(
                            "with_underscore",
                            PortableTypes.VarChar(50),
                            c => c.DefaultLql("'snake_case_value'")
                        )
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlBoolean_MultipleColumns_AllDefaultCorrectly_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "feature_flags",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("flag_a", PortableTypes.Boolean, c => c.DefaultLql("true"))
                        .Column("flag_b", PortableTypes.Boolean, c => c.DefaultLql("false"))
                        .Column("flag_c", PortableTypes.Boolean, c => c.DefaultLql("true"))
                        .Column("flag_d", PortableTypes.Boolean, c => c.DefaultLql("false"))
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlUuid_MultipleInserts_AllUnique_BothPlatforms_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "uuid_test",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey().DefaultLql("gen_uuid()"))
                        .Column("seq", PortableTypes.Int, c => c.NotNull())
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlTimestamp_AllTimeTypes_WorkBothPlatforms_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "time_test",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("ts1", PortableTypes.DateTimeOffset, c => c.DefaultLql("now()"))
                        .Column(
                            "ts2",
                            PortableTypes.DateTimeOffset,
                            c => c.DefaultLql("current_timestamp()")
                        )
                        .Column("d", PortableTypes.Date, c => c.DefaultLql("current_date()"))
                        .Column("t", PortableTypes.Time(), c => c.DefaultLql("current_time()"))
            )
            .Build();
}
