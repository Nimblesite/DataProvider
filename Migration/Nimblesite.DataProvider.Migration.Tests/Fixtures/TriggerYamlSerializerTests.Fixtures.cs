// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class TriggerYamlSerializerTests
{
    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Triggers_Absent_DoNotAppearInYaml_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.TableDefinition>? tables,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.ColumnDefinition>? columns,
        global::System.String name,
        global::Nimblesite.DataProvider.Migration.Core.PortableType type
    ) =>
        SchemaTestData.Schema(
            name: "t",
            tables:
            [
                SchemaTestData.Table(
                    name: "plain",
                    columns: [SchemaTestData.Column(name: "id", type: new UuidType())]
                ),
            ]
        );
}
