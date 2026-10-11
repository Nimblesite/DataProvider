// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class DataProviderMigrateIntegrityTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Migrate_WhenDiffIsCleanButIntegrityFails_DoesNotClaimSchemaUpToDate_1() =>
        Schema
            .Define(name: "nap")
            .Table(
                schema: "public",
                name: "settings",
                configure: t =>
                    t.Column(name: "id", type: PortableTypes.Uuid, configure: c => c.PrimaryKey())
                        .Column(
                            name: "tier",
                            type: PortableTypes.Text,
                            configure: c => c.NotNull().Default(defaultValue: "'free'")
                        )
            )
            .Build();
}
