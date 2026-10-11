// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaDiffTests
{
    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Calculate_RemovedTable_NotDroppedByDefault_1() =>
        Schema
            .Define("Current")
            .Table("public", "users", t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Table(
                "public",
                "obsolete",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Calculate_RemovedTable_DroppedWhenDestructiveAllowed_1() =>
        Schema
            .Define("Current")
            .Table("public", "users", t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Table(
                "public",
                "obsolete",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Calculate_RemovedForeignKey_DroppedWhenDestructiveAllowed_1() =>
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
                        .ForeignKey("dept_id", "departments", "id", ForeignKeyAction.Cascade)
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Calculate_RemovedForeignKey_DroppedWhenDestructiveAllowed_2() =>
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
            )
            .Build();
}
