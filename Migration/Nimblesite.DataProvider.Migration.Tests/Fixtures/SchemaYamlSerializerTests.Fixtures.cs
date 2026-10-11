// Implements [CI-DESLOP-INDEPENDENT-DATA].
using System.Globalization;
using Tests.Shared;

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaYamlSerializerTests
{
    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_ToYaml_SimpleSchema_ProducesValidYaml_1() =>
        Schema
            .Define("test")
            .Table(
                "Users",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Email", PortableTypes.VarChar(255), c => c.NotNull())
                        .Column("Name", PortableTypes.Text)
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_RoundTrip_ComplexSchema_PreservesAllData_1(
        global::System.Boolean unique
    ) =>
        Schema
            .Define("complex_schema")
            .Table(
                "Users",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Email", PortableTypes.NVarChar(255), c => c.NotNull())
                        .Column(
                            "CreatedAt",
                            PortableTypes.DateTime(),
                            c => c.NotNull().DefaultLql("now()")
                        )
                        .Column(
                            "IsActive",
                            PortableTypes.Boolean,
                            c => c.NotNull().DefaultLql("true")
                        )
                        .Index("idx_users_email", "Email", unique: true)
            )
            .Table(
                "Orders",
                t =>
                    t.Column("Id", PortableTypes.BigInt, c => c.PrimaryKey().Identity())
                        .Column("UserId", PortableTypes.Uuid, c => c.NotNull())
                        .Column("Total", PortableTypes.Decimal(12, 2), c => c.NotNull())
                        .ForeignKey("UserId", "Users", "Id", ForeignKeyAction.Cascade)
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_PortableType_AllTypes_SerializeCorrectly_1() =>
        Schema
            .Define("types_test")
            .Table(
                "AllTypes",
                t =>
                    t.Column("TinyInt", PortableTypes.TinyInt)
                        .Column("SmallInt", PortableTypes.SmallInt)
                        .Column("Int", PortableTypes.Int)
                        .Column("BigInt", PortableTypes.BigInt)
                        .Column("Decimal", PortableTypes.Decimal(18, 4))
                        .Column("Float", PortableTypes.Float)
                        .Column("Double", PortableTypes.Double)
                        .Column("Money", PortableTypes.Money)
                        .Column("Bool", PortableTypes.Boolean)
                        .Column("Char", PortableTypes.Char(10))
                        .Column("VarChar", PortableTypes.VarChar(100))
                        .Column("NChar", PortableTypes.NChar(5))
                        .Column("NVarChar", PortableTypes.NVarChar(500))
                        .Column("NVarCharMax", PortableTypes.NVarCharMax)
                        .Column("Text", PortableTypes.Text)
                        .Column("Binary", PortableTypes.Binary(16))
                        .Column("VarBinary", PortableTypes.VarBinary(256))
                        .Column("VarBinaryMax", PortableTypes.VarBinaryMax)
                        .Column("Blob", PortableTypes.Blob)
                        .Column("Date", PortableTypes.Date)
                        .Column("Time", PortableTypes.Time(3))
                        .Column("DateTime", PortableTypes.DateTime(6))
                        .Column("DateTimeOffset", PortableTypes.DateTimeOffset)
                        .Column("Uuid", PortableTypes.Uuid)
                        .Column("Json", PortableTypes.Json)
                        .Column("Xml", PortableTypes.Xml)
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_ForeignKey_WithActions_SerializesCorrectly_1(
        global::Nimblesite.DataProvider.Migration.Core.ForeignKeyAction onDelete,
        global::Nimblesite.DataProvider.Migration.Core.ForeignKeyAction onUpdate
    ) =>
        Schema
            .Define("fk_test")
            .Table("Parent", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Table(
                "Child",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("ParentId", PortableTypes.Uuid, c => c.NotNull())
                        .ForeignKey(
                            "ParentId",
                            "Parent",
                            "Id",
                            onDelete: ForeignKeyAction.Cascade,
                            onUpdate: ForeignKeyAction.SetNull
                        )
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Index_WithFilter_SerializesCorrectly_1(
        global::System.Boolean unique,
        global::System.String? filter
    ) =>
        Schema
            .Define("index_test")
            .Table(
                "Items",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("IsActive", PortableTypes.Boolean, c => c.NotNull())
                        .Column("Name", PortableTypes.VarChar(100))
                        .Index("idx_active_items", "Name", unique: true, filter: "IsActive = 1")
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_ExpressionIndex_SerializesCorrectly_1(
        global::Nimblesite.DataProvider.Migration.Core.TableBuilder table,
        global::Nimblesite.DataProvider.Migration.Core.PortableType idType,
        global::Nimblesite.DataProvider.Migration.Core.PortableType nameType,
        global::System.Boolean unique
    ) =>
        Schema
            .Define("expr_index_test")
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

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_CheckConstraint_SerializesCorrectly_1() =>
        Schema
            .Define("check_test")
            .Table(
                "Products",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column(
                            "Price",
                            PortableTypes.Decimal(10, 2),
                            c => c.NotNull().Check("CK_Products_Price", "Price >= 0")
                        )
                        .Column("Quantity", PortableTypes.Int, c => c.NotNull())
                        .Check("CK_Products_Quantity", "Quantity >= 0")
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_UniqueConstraint_SerializesCorrectly_1() =>
        Schema
            .Define("unique_test")
            .Table(
                "Users",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Email", PortableTypes.VarChar(255), c => c.NotNull())
                        .Column("TenantId", PortableTypes.Uuid, c => c.NotNull())
                        .Unique("UQ_Users_Email_Tenant", "Email", "TenantId")
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_PortableType_EnumType_SerializesCorrectly_1() =>
        Schema
            .Define("enum_test")
            .Table(
                "Items",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column(
                            "Status",
                            PortableTypes.Enum("item_status", "pending", "active", "archived")
                        )
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_DefaultLqlExpression_SerializesCorrectly_1() =>
        Schema
            .Define("lql_test")
            .Table(
                "Events",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey().DefaultLql("gen_uuid()"))
                        .Column(
                            "CreatedAt",
                            PortableTypes.DateTime(),
                            c => c.NotNull().DefaultLql("now()")
                        )
                        .Column(
                            "IsActive",
                            PortableTypes.Boolean,
                            c => c.NotNull().DefaultLql("true")
                        )
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_ToYaml_OmitsSemanticDefaultValues_1() =>
        Schema
            .Define("test")
            .Table(
                "Users",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Name", PortableTypes.Text) // nullable by default
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_VectorType_YamlRoundTrip_PreservesDimensions_1() =>
        Schema
            .Define("ai")
            .Table(
                "Documents",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Embedding", PortableTypes.Vector(1536), c => c.NotNull())
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_VectorType_PostgresDdl_EmitsPgVectorType_1() =>
        Schema
            .Define("ai")
            .Table(
                "Documents",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Embedding", PortableTypes.Vector(768), c => c.NotNull())
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_VectorType_SqliteDdl_FallsBackToBlob_1() =>
        Schema
            .Define("ai")
            .Table(
                "Documents",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Embedding", PortableTypes.Vector(768), c => c.NotNull())
            )
            .Build();
}
