// Implements [CI-DESLOP-INDEPENDENT-DATA].
using Tests.Shared;

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class MigrateSchemaTests
{
    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_MigrateSchema_TableCreationFails_ContinuesWithOtherTables_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "good_table",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Table(
                "public",
                "bad_table",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("ref_id", PortableTypes.Uuid)
                        .ForeignKey("ref_id", "nonexistent_table", "id")
            )
            .Table(
                "public",
                "another_good_table",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_MigrateSchema_CallbacksInvoked_ForSuccessAndFailure_1() =>
        Schema
            .Define("Test")
            .Table(
                "public",
                "success_table",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Table(
                "public",
                "fail_table",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .ForeignKey("id", "missing_parent", "id")
            )
            .Build();
}
