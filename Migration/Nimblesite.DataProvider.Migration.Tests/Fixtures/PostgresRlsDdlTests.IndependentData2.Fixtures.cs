// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresRlsDdlTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.CreateRlsPolicyOperation Dataset_Generate_CreateRlsPolicy_OwnerIsolation_FullShape_1() =>
        new CreateRlsPolicyOperation(
            "public",
            "Documents",
            Fixture_Generate_CreateRlsPolicy_OwnerIsolation_FullShape_1()
        );

    private static global::Nimblesite.DataProvider.Migration.Core.CreateRlsPolicyOperation Dataset_Generate_CreateRlsPolicy_RestrictiveSelectOnly_OnSpecificRoles_1() =>
        new CreateRlsPolicyOperation(
            "public",
            "Audit",
            Fixture_Generate_CreateRlsPolicy_RestrictiveSelectOnly_OnSpecificRoles_1()
        );

    private static global::Nimblesite.DataProvider.Migration.Core.CreateRlsPolicyOperation Dataset_Generate_CreateRlsPolicy_LqlExistsSubquery_TranspilesAndWraps_1(
        global::System.String lql
    ) =>
        new CreateRlsPolicyOperation(
            "public",
            "Documents",
            Fixture_Generate_CreateRlsPolicy_LqlExistsSubquery_TranspilesAndWraps_1(lql: lql)
        );

    private static global::Nimblesite.DataProvider.Migration.Core.CreateRlsPolicyOperation Dataset_Generate_CreateRlsPolicy_RawSqlPredicates_EmitVerbatim_1() =>
        new CreateRlsPolicyOperation(
            "public",
            "Documents",
            Fixture_Generate_CreateRlsPolicy_RawSqlPredicates_EmitVerbatim_1()
        );

    private static global::Nimblesite.DataProvider.Migration.Core.CreateRlsPolicyOperation Dataset_Generate_CreateRlsPolicy_EmptyPredicate_ThrowsWithRlsErrorCode_1() =>
        new CreateRlsPolicyOperation(
            "public",
            "Documents",
            SchemaTestData.RlsPolicy(name: "broken", usingLql: "")
        );

    private static global::Nimblesite.DataProvider.Migration.Core.CreateRlsPolicyOperation Dataset_Generate_CreateRlsPolicy_EmptyWithCheck_ThrowsWithRlsErrorCode_1() =>
        new CreateRlsPolicyOperation(
            "public",
            "Documents",
            SchemaTestData.RlsPolicy(name: "broken_check", operations: [RlsOperation.Insert])
        );
}
