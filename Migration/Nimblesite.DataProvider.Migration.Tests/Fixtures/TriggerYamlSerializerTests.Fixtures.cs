// Implements [CI-DESLOP-INDEPENDENT-DATA].


namespace Nimblesite.DataProvider.Migration.Tests;

public partial class TriggerYamlSerializerTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Triggers_Absent_DoNotAppearInYaml_1() =>
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
