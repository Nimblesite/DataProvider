// Implements [MIG-TEST-SCHEMA-FIXTURES].
// Implements [CI-TEST-SHARED-COMMAND].

namespace Nimblesite.DataProvider.Migration.Tests;

[Collection(PostgresTestSuite.Name)]
public sealed partial class PostgresFunctionBodyLqlE2ETests(PostgresContainerFixture fixture)
    : PostgresDatabaseTest(fixture)
{
    protected override string DatabaseName => "function_body_lql";

    [Fact]
    public void BodyLqlSupportFunctionsAndLqlPolicies_EnforceNapTenantIsolation()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var names = Names.Create(suffix);

        Apply(Schema(names));

        var (tenantA, tenantB, userA) =
            global::Nimblesite.DataProvider.Migration.Tests.PostgresSupportE2ETests.VerifyCompletionWorkflow4(
                input0: Connection,
                value3: false
            );

        using var tx = Connection.BeginTransaction();
        PostgresTestDb.SetTenantSession(
            connection: Connection,
            transaction: tx,
            role: names.AppUserRole,
            tenant: tenantA,
            user: userA,
            settingNamespace: "app"
        );

        global::Nimblesite.DataProvider.Migration.Tests.PostgresSupportE2ETests.VerifyReusableWorkflow24(
            input0: Connection,
            input1: tx,
            input2: tenantB
        );
    }

    private void Apply(SchemaDefinition schema)
    {
        var current = PostgresTestDb.Inspect(Connection, Logger);
        var ops = SchemaDiffAssertions.Diff(current: current, desired: schema, logger: Logger);
        var apply = PostgresTestDb.TryApply(
            connection: Connection,
            operations: ops,
            logger: Logger
        );

        PostgresTestDb.AssertApplyResult(apply: apply);
    }

    private sealed record Names(
        string AppUserRole,
        string AppTenantFunction,
        string AppUserFunction,
        string IsMemberFunction
    )
    {
        public static Names Create(string suffix) =>
            new(
                $"body_lql_user_{suffix}",
                $"app_tenant_id_{suffix}",
                $"app_user_id_{suffix}",
                $"is_member_{suffix}"
            );
    }
}
