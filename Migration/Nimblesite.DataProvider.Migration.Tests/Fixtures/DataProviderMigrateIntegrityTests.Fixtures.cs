// Implements [CI-DESLOP-INDEPENDENT-DATA].
using System.Globalization;

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class DataProviderMigrateIntegrityTests
{
    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Migrate_WhenDiffIsCleanButIntegrityFails_DoesNotClaimSchemaUpToDate_1(
        global::System.String name,
        global::System.String schema,
        global::System.String name,
        global::System.Action<global::Nimblesite.DataProvider.Migration.Core.TableBuilder> configure,
        global::System.String name,
        global::Nimblesite.DataProvider.Migration.Core.PortableType type,
        global::System.Action<global::Nimblesite.DataProvider.Migration.Core.ColumnBuilder>? configure,
        global::System.String defaultValue
    ) =>
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
