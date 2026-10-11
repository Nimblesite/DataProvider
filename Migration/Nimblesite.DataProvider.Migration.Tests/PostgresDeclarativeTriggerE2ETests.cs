// Implements [CI-TEST-SHARED-COMMAND-PARAMETERS].

namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [MIG-TRIGGER-PG] and [MIG-TRIGGER-DIFF] from
// docs/specs/declarative-triggers-spec.md (GitHub issue 82).

/// <summary>
/// E2E tests for declarative BEFORE-trigger guards against a real
/// Testcontainers postgres instance. The schema declares a last-owner guard
/// on tenant_members: demoting or deleting the only 'owner' row of a tenant
/// must fail with the declared error message.
/// </summary>
[Collection(PostgresTestSuite.Name)]
public sealed partial class PostgresDeclarativeTriggerE2ETests(PostgresContainerFixture fixture)
    : PostgresDatabaseTest(fixture)
{
    protected override string DatabaseName => "trigger_e2e";

    private const string GuardYaml = Data_Shared_1;

    [Fact]
    public void PgTriggerGuard_BlocksDeleteOfLastOwner()
    {
        ArrangeOwnerAndAssertGuard(action: DeleteMember);
        Assert.Equal(2, CountMembers());
    }

    [Fact]
    public void PgTriggerGuard_AllowsDeleteWhenAnotherOwnerExists()
    {
        var ownerA = VerifyReusableWorkflow21(
            input0: ApplyGuardSchema,
            input1: InsertMember,
            value1: "owner"
        );

        DeleteMember(ownerA);

        Assert.Equal(1, CountMembers());
    }

    [Fact]
    public void PgTriggerGuard_BlocksDemoteOfLastOwner()
    {
        ArrangeOwnerAndAssertGuard(action: DemoteMember);
    }

    [Fact]
    public void PgTrigger_RediffAfterApply_ProducesNoOperations()
    {
        ApplyGuardSchema();

        var ops = PostgresTestDb.CalculateChanges(
            Connection,
            SchemaYamlSerializer.FromYaml(GuardYaml),
            Logger
        );

        Assert.Empty(ops);
    }

    private const string PascalCaseGuardYaml = Data_Shared_2;

    [Fact]
    public void PgTriggerGuard_PascalCaseIdentifiers_EnforcedAtRuntime()
    {
        // Implements [MIG-TRIGGER-GUARD-LQL]: exists() pipeline columns must
        // be quoted on Postgres or mixed-case columns fold to lowercase and
        // every UPDATE/DELETE fails with 42703 at trigger-fire time.
        PostgresTestDb.ApplySchema(
            Connection,
            SchemaYamlSerializer.FromYaml(PascalCaseGuardYaml),
            Logger
        );
        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        InsertPascalMember(ownerA, tenant, "owner");
        InsertPascalMember(ownerB, tenant, "owner");

        // Two owners: deleting one must succeed (trigger fires cleanly).
        Execute("DELETE FROM \"public\".\"TenantMembers\" WHERE \"Id\" = @i", ownerA);

        // Last owner: must be blocked by the declared guard message.
        var ex = Assert.Throws<PostgresException>(() =>
            Execute("DELETE FROM \"public\".\"TenantMembers\" WHERE \"Id\" = @i", ownerB)
        );
        Assert.Contains("last owner", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PgTrigger_DestructiveRerun_PreservesUnmanagedTriggerAndGuardFunction()
    {
        // Implements [MIG-TRIGGER-PG]: only usr_-prefixed triggers are
        // migration-managed. Unmanaged triggers (e.g. Sync change tracking)
        // must survive destructive runs, and the guard function must not be
        // dropped out from under its trigger.
        ApplyGuardSchema();
        Execute(
            """
            CREATE TRIGGER zz_unmanaged_touch BEFORE UPDATE ON "public"."tenant_members"
            FOR EACH ROW EXECUTE FUNCTION suppress_redundant_updates_trigger()
            """
        );

        PostgresTestDb.ApplySchema(
            Connection,
            SchemaYamlSerializer.FromYaml(GuardYaml),
            Logger,
            allowDestructive: true
        );

        Assert.Equal(
            1L,
            Assert.IsType<long>(
                global::Tests.Shared.DatabaseCommands.Scalar(
                    connection: Connection,
                    configure: command =>
                        command.CommandText =
                            "SELECT COUNT(*) FROM pg_trigger WHERE tgname = 'zz_unmanaged_touch'"
                )
            )
        );

        // Guard still enforced after the destructive rerun.
        var owner = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        InsertMember(owner, tenant, "owner");
        var ex = Assert.Throws<PostgresException>(() => DeleteMember(owner));
        Assert.Contains("last owner", ex.Message, StringComparison.Ordinal);
    }

    private void Execute(string sql, Guid? id = null)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = sql;
        if (id is { } value)
        {
            cmd.Parameters.AddWithValue("@i", value);
        }
        cmd.ExecuteNonQuery();
    }

    private void InsertPascalMember(Guid id, Guid tenantId, string role)
    {
        DatabaseCommands.Execute(
            connection: Connection,
            configure: cmd =>
                cmd.CommandText =
                    "INSERT INTO \"public\".\"TenantMembers\"(\"Id\", \"TenantId\", \"Role\") VALUES (@i, @t, @r)",
            parameters: () => [("@i", id), ("@t", tenantId), ("@r", role)]
        );
    }

    private void ApplyGuardSchema() =>
        PostgresTestDb.ApplySchema(Connection, SchemaYamlSerializer.FromYaml(GuardYaml), Logger);

    private void InsertMember(Guid id, Guid tenantId, string role)
    {
        DatabaseCommands.Execute(
            connection: Connection,
            configure: cmd =>
                cmd.CommandText =
                    "INSERT INTO \"public\".\"tenant_members\"(id, tenant_id, role) VALUES (@i, @t, @r)",
            parameters: () => [("@i", id), ("@t", tenantId), ("@r", role)]
        );
    }

    private void DeleteMember(Guid id) =>
        ExecuteById(sql: "DELETE FROM \"public\".\"tenant_members\" WHERE id = @i", id: id);

    private void DemoteMember(Guid id) =>
        ExecuteById(
            sql: "UPDATE \"public\".\"tenant_members\" SET role = 'member' WHERE id = @i",
            id: id
        );

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void ExecuteById(string sql, Guid id)
    {
        DatabaseCommands.Execute(
            connection: Connection,
            configure: cmd => cmd.CommandText = sql,
            parameters: () => [("@i", id)]
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void ArrangeOwnerAndAssertGuard(Action<Guid> action)
    {
        var owner = VerifyReusableWorkflow21(
            input0: ApplyGuardSchema,
            input1: InsertMember,
            value1: "member"
        );
        var ex = Assert.Throws<PostgresException>(() => action(owner));
        Assert.Contains("last owner", ex.Message, StringComparison.Ordinal);
    }

    private long CountMembers()
    {
        return Assert.IsType<long>(
            global::Tests.Shared.DatabaseCommands.Scalar(
                connection: Connection,
                configure: command =>
                    command.CommandText = "SELECT COUNT(*) FROM \"public\".\"tenant_members\""
            )
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static Guid VerifyReusableWorkflow21(
        Action input0,
        Action<Guid, Guid, string> input1,
        string value1
    )
    {
        input0();
        var owner = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        input1(arg1: owner, arg2: tenant, arg3: "owner");
        input1(arg1: Guid.NewGuid(), arg2: tenant, arg3: value1);
        return owner;
    }
}
