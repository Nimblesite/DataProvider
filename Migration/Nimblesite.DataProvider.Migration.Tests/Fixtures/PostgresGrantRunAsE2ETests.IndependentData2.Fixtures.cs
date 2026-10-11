// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresGrantRunAsE2ETests
{
    private static global::Nimblesite.DataProvider.Migration.Core.GrantPrivilegesOperation Dataset_GrantRunAs_MissingRoleMembership_ReturnsClearGrantToMessage_1(
        global::System.String appUser,
        global::System.String owner
    ) =>
        new GrantPrivilegesOperation(
            SchemaTestData.PostgresGrant(
                schema: "public",
                target: PostgresGrantTarget.Schema,
                privileges: ["USAGE"],
                roles: [appUser],
                runAs: owner
            )
        );
}
