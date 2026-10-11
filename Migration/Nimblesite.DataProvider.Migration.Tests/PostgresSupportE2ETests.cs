// Implements [MIG-TEST-SCHEMA-FIXTURES].
// Implements [CI-TEST-SHARED-COMMAND].

namespace Nimblesite.DataProvider.Migration.Tests;

[Collection(PostgresTestSuite.Name)]
public sealed partial class PostgresSupportE2ETests(PostgresContainerFixture fixture)
    : PostgresDatabaseTest(fixture)
{
    protected override string DatabaseName => "support_objects";

    [Fact]
    public void DeclarativeRolesFunctionsAndGrants_UnblockNapStyleRls()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var names = SupportNames.Create(suffix);
        var schema = SupportSchema(names);

        Apply(schema);

        var reappliedOps = PostgresTestDb.CalculateChanges(Connection, schema, Logger);
        Assert.Empty(reappliedOps);

        var (tenantA, tenantB, userA) = VerifyCompletionWorkflow4(input0: Connection, value3: true);

        using var tx = Connection.BeginTransaction();
        PostgresTestDb.SetTenantSession(
            connection: Connection,
            transaction: tx,
            role: names.AppUserRole,
            tenant: tenantA,
            user: userA,
            settingNamespace: "rls"
        );
        VerifyReusableWorkflow24(input0: Connection, input1: tx, input2: tenantB);
    }

    private void Apply(SchemaDefinition schema)
    {
        var ops = PostgresTestDb.CalculateChanges(Connection, schema, Logger);
        PostgresLqlOnlyE2ETests.ApplyOperations(
            connection: Connection,
            operations: ops,
            logger: Logger
        );
    }

    private static IReadOnlyList<PostgresFunctionDefinition> SupportFunctions(SupportNames names) =>
        [
            Function(
                names.TenantFunction,
                "uuid",
                "SELECT NULLIF(current_setting('rls.tenant_id', true), '')::uuid",
                false,
                names
            ),
            Function(
                names.UserFunction,
                "uuid",
                "SELECT NULLIF(current_setting('rls.user_id', true), '')::uuid",
                false,
                names
            ),
            MembershipFunction(names.MemberFunction, "", names),
            MembershipFunction(names.WriterFunction, "AND tm.role IN ('writer', 'owner')", names),
            MembershipFunction(names.OwnerFunction, "AND tm.role = 'owner'", names),
        ];

    private sealed record SupportNames(
        string AppUserRole,
        string AppAdminRole,
        string TenantFunction,
        string UserFunction,
        string MemberFunction,
        string WriterFunction,
        string OwnerFunction
    )
    {
        public static SupportNames Create(string suffix) =>
            new(
                $"dp_app_user_{suffix}",
                $"dp_app_admin_{suffix}",
                $"app_tenant_id_{suffix}",
                $"app_user_id_{suffix}",
                $"is_member_{suffix}",
                $"is_tenant_writer_{suffix}",
                $"is_tenant_owner_{suffix}"
            );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow24(
        NpgsqlConnection input0,
        NpgsqlTransaction input1,
        Guid input2
    )
    {
        Assert.Equal(1, PostgresTestDb.CountDocuments(connection: input0, transaction: input1));
        Assert.Throws<PostgresException>(() =>
            PostgresTestDb.InsertTenantDocument(
                connection: input0,
                transaction: input1,
                tenant: input2,
                title: "blocked"
            )
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static (Guid, Guid, Guid) VerifyCompletionWorkflow4(
        NpgsqlConnection input0,
        bool value3
    )
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userA = Guid.NewGuid();
        PostgresTestDb.SeedTenantDocuments(
            connection: input0,
            tenantA: tenantA,
            tenantB: tenantB,
            userA: userA,
            includeWriterRole: value3
        );

        return (tenantA, tenantB, userA);
    }
}
