// Implements [CI-DESLOP-INDEPENDENT-DATA], [MIG-TEST-DIFF-SHARED].
namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class SchemaDiffTests
{
    public static IEnumerable<object[]> NonDestructiveRemovalCases() =>
        [
            new object[]
            {
                "Calculate_RemovedTable_NotDroppedByDefault",
                Fixture_Calculate_RemovedTable_NotDroppedByDefault_1(),
                UsersSchema(name: "Desired"),
            },
            new object[]
            {
                "Calculate_RemovedColumn_NotDroppedByDefault",
                UsersSchema(name: "Current", oldField: true),
                UsersSchema(name: "Desired"),
            },
            new object[]
            {
                "Calculate_RemovedIndex_NotDroppedByDefault",
                UsersSchema(name: "Current", indexEmail: true),
                UsersSchema(name: "Desired", email: true),
            },
        ];
}
