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

    public static IEnumerable<object[]> DestructiveRemovalCases() =>
        [
            new object[]
            {
                "Calculate_RemovedTable_DroppedWhenDestructiveAllowed",
                Fixture_Calculate_RemovedTable_DroppedWhenDestructiveAllowed_1(),
                UsersSchema(name: "Desired"),
                "obsolete",
            },
            new object[]
            {
                "Calculate_RemovedColumn_DroppedWhenDestructiveAllowed",
                UsersSchema(name: "Current", oldField: true),
                UsersSchema(name: "Desired"),
                "old_field",
            },
            new object[]
            {
                "Calculate_RemovedIndex_DroppedWhenDestructiveAllowed",
                UsersSchema(name: "Current", indexEmail: true),
                UsersSchema(name: "Desired", email: true),
                "idx_users_email",
            },
        ];
}
