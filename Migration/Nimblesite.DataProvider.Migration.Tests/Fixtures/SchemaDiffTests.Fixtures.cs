// Implements [CI-DESLOP-INDEPENDENT-DATA].


namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaDiffTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Calculate_EmptyCurrentToNewDesired_CreatesTable_1() =>
        Schema
            .Define("Desired")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(100), c => c.NotNull())
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Calculate_SameSchema_NoOperations_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(100))
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Calculate_NewColumn_AddsColumn_1() =>
        Schema
            .Define("Desired")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("email", PortableTypes.VarChar(255), c => c.NotNull())
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Calculate_NewForeignKey_AddsForeignKey_1() =>
        Schema
            .Define("Current")
            .Table(
                "public",
                "departments",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Table(
                "public",
                "employees",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("dept_id", PortableTypes.Uuid)
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Calculate_NewForeignKey_AddsForeignKey_2() =>
        Schema
            .Define("Desired")
            .Table(
                "public",
                "departments",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Table(
                "public",
                "employees",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("dept_id", PortableTypes.Uuid)
                        .ForeignKey("dept_id", "departments", "id", ForeignKeyAction.Cascade)
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Calculate_CaseInsensitiveColumnMatching_1() =>
        Schema
            .Define("Current")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("EMAIL", PortableTypes.VarChar(255))
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Calculate_MultipleNewTables_CreatesAll_1() =>
        Schema
            .Define("Desired")
            .Table(
                "public",
                "countries",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Table(
                "public",
                "regions",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Table("public", "cities", t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Calculate_ComplexMigration_CombinesOperations_1() =>
        Schema
            .Define("Current")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("old_field", PortableTypes.VarChar(100))
            )
            .Table(
                "public",
                "obsolete_table",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Calculate_ComplexMigration_CombinesOperations_2() =>
        Schema
            .Define("Desired")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("new_field", PortableTypes.VarChar(200))
                        .Index("idx_users_new", "new_field")
            )
            .Table(
                "public",
                "new_table",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("user_id", PortableTypes.Uuid)
                        .ForeignKey("user_id", "users", "id")
            )
            .Build();
}
