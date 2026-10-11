// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Pure immutable fixture definitions; assertions and database workflows remain analysed.

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class SqliteRlsMigrationTests
{
    private static TableDefinition DocumentsTable(RlsPolicyDefinition policy) =>
        SchemaTestData.Table(
            schema: "main",
            name: "Documents",
            columns:
            [
                RequiredText("Id"),
                RequiredText("OwnerId"),
                SchemaTestData.Column(name: "Title", type: PortableTypes.Text),
            ],
            primaryKey: SchemaTestData.PrimaryKey(name: "PK_Documents", columns: ["Id"]),
            rowLevelSecurity: new RlsPolicySetDefinition { Policies = [policy] }
        );

    private static TableDefinition MembershipTable() =>
        SchemaTestData.Table(
            schema: "main",
            name: "UserGroupMemberships",
            columns: [RequiredText("Id"), RequiredText("UserId")],
            primaryKey: SchemaTestData.PrimaryKey(name: "PK_UserGroupMemberships", columns: ["Id"])
        );

    private static RlsPolicyDefinition OwnerPolicy(IReadOnlyList<RlsOperation> ops) =>
        SchemaTestData.RlsPolicy(
            name: "owner_isolation",
            operations: ops,
            usingLql: "OwnerId = current_user_id()",
            withCheckLql: "OwnerId = current_user_id()"
        );

    private static RlsPolicyDefinition GroupMembershipPolicy() =>
        SchemaTestData.RlsPolicy(
            name: "group_member_insert",
            operations: [RlsOperation.Insert],
            withCheckLql: Data_GroupMembershipPolicy_1
        );
}
