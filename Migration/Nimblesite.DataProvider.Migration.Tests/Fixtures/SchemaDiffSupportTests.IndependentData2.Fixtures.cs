// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaDiffSupportTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.PostgresGrantDefinition Dataset_Calculate_StaleManagedGrant_AllowDestructive_EmitsRevoke_1() =>
        SchemaTestData.PostgresGrant(
            schema: "public",
            target: PostgresGrantTarget.AllTablesInSchema,
            privileges: ["SELECT"],
            roles: ["app_user"]
        );

    private static global::Nimblesite.DataProvider.Migration.Core.PostgresGrantDefinition Dataset_Calculate_GrantRunAsAlreadyApplied_HasNoOperations_1() =>
        SchemaTestData.PostgresGrant(
            schema: "public",
            target: PostgresGrantTarget.AllTablesInSchema,
            privileges: ["SELECT", "INSERT"],
            roles: ["app_user"],
            runAs: "schema_owner"
        );

    private static global::Nimblesite.DataProvider.Migration.Core.PostgresGrantDefinition Dataset_Calculate_MissingGrantRunAs_PreservesRunAsOnOperation_1() =>
        SchemaTestData.PostgresGrant(
            schema: "auth",
            target: PostgresGrantTarget.Table,
            objectName: "users",
            privileges: ["SELECT"],
            roles: ["app_user"],
            runAs: "supabase_admin"
        );
}
