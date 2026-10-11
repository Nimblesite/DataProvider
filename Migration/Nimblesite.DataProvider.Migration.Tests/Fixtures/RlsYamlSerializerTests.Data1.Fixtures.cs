// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsYamlSerializerTests
{
    private const string Data_RlsPolicyDefinition_YamlRoundTrip_SubqueryPolicy_1 = """
        exists(
          UserGroupMemberships
          |> filter(fn(m) => m.UserId = current_user_id() and m.GroupId = GroupId)
        )
        """;

    private const string Data_RlsPolicy_FullSpecExampleYaml_DeserializesCorrectly_1 = """
        name: MyApp
        tables:
          - name: Documents
            schema: public
            columns:
              - name: Id
                type: Uuid
                isNullable: false
              - name: OwnerId
                type: Uuid
                isNullable: false
              - name: GroupId
                type: Uuid
            primaryKey:
              columns:
                - Id
            rowLevelSecurity:
              enabled: true
              policies:
                - name: owner_isolation
                  permissive: true
                  operations: [All]
                  roles: []
                  using: "OwnerId = current_user_id()"
                  withCheck: "OwnerId = current_user_id()"
        """;
}
