// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresRlsDdlTests
{
    private global::Nimblesite.DataProvider.Migration.Core.RlsPolicyDefinition Fixture_Generate_CreateRlsPolicy_OwnerIsolation_FullShape_1(
        global::System.String name,
        global::System.Boolean isPermissive,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.RlsOperation> operations,
        global::System.String? usingLql,
        global::System.String? withCheckLql
    ) =>
        SchemaTestData.RlsPolicy(
            name: "owner_isolation",
            isPermissive: true,
            operations: [RlsOperation.All],
            usingLql: "OwnerId = current_user_id()",
            withCheckLql: "OwnerId = current_user_id()"
        );

    private global::Nimblesite.DataProvider.Migration.Core.RlsPolicyDefinition Fixture_Generate_CreateRlsPolicy_RestrictiveSelectOnly_OnSpecificRoles_1(
        global::System.String name,
        global::System.Boolean isPermissive,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.RlsOperation> operations,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? roles,
        global::System.String? usingLql
    ) =>
        SchemaTestData.RlsPolicy(
            name: "audit_admin_only",
            isPermissive: false,
            operations: [RlsOperation.Select],
            roles: ["admin", "auditor"],
            usingLql: "true"
        );

    private global::Nimblesite.DataProvider.Migration.Core.RlsPolicyDefinition Fixture_Generate_CreateRlsPolicy_LqlExistsSubquery_TranspilesAndWraps_1(
        global::System.String name,
        global::System.Boolean isPermissive,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.RlsOperation> operations,
        global::System.String? usingLql,
        global::System.String lql
    ) =>
        SchemaTestData.RlsPolicy(
            name: "group_read",
            isPermissive: true,
            operations: [RlsOperation.Select],
            usingLql: $"exists({lql})"
        );

    private global::Nimblesite.DataProvider.Migration.Core.RlsPolicyDefinition Fixture_Generate_CreateRlsPolicy_RawSqlPredicates_EmitVerbatim_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.RlsOperation> operations,
        global::System.String? usingLql,
        global::System.String? withCheckLql,
        global::System.String? usingSql,
        global::System.String? withCheckSql
    ) =>
        SchemaTestData.RlsPolicy(
            name: "raw_sql",
            operations: [RlsOperation.All],
            usingLql: "OwnerId = current_user_id()",
            withCheckLql: "OwnerId = current_user_id()",
            usingSql: "is_member(\"GroupId\")",
            withCheckSql: "can_write(\"GroupId\")"
        );
}
