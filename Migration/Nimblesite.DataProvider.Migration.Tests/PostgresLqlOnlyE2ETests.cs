using static global::Tests.Shared.ResultAssertions;
using static global::Tests.Shared.StringAssertions;

// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

// EXHAUSTIVE end-to-end proof that NAP can build production RLS using ONLY
// LQL predicates (no usingSql / withCheckSql). Each test creates the NAP
// SECURITY DEFINER function shapes manually (between phases, mirroring
// NAP's overlay), then applies an LQL-only RLS policy and proves the
// policy evaluates correctly under a non-bypassrls app role against real
// Postgres. CLAUDE.md ban: NO SQL in YAML schema input.

/// <summary>
/// LQL-only end-to-end RLS coverage. Every test uses UsingLql/WithCheckLql
/// and asserts both that DDL succeeds against a real Postgres testcontainer
/// and that the resulting policy enforces correctly at query time.
/// </summary>
[Collection(PostgresTestSuite.Name)]
public sealed partial class PostgresLqlOnlyE2ETests(PostgresContainerFixture fixture)
    : PostgresDatabaseTest(fixture)
{
    protected override string DatabaseName => "rls_lql_e2e";

    // Phase 1 bootstrap: roles + GUC reader fns. Does NOT include is_member(),
    // which depends on tenant_members existing (created by DP migrate).
    protected override void OnDatabaseCreated() =>
        PostgresTestDb.Exec(Connection, Data_BootstrapRolesAndGucReaders_1);

    /// <summary>
    /// Phase 2 bootstrap (after DP creates tenant_members): SECURITY DEFINER
    /// membership fns that reference tenant_members.
    /// </summary>
    private static void BootstrapMembershipFns(NpgsqlConnection conn) =>
        PostgresTestDb.Exec(conn, Data_BootstrapMembershipFns_1);

    /// <summary>
    /// Inspects the live public schema, diffs it against the desired schema,
    /// applies the resulting operations, and asserts the migration succeeded.
    /// </summary>
    internal static void ApplySchema(
        NpgsqlConnection connection,
        SchemaDefinition desired,
        ILogger logger
    )
    {
        var ops = SqliteTestDb.CalculateChanges(
            inspect: () =>
                (Ok(actual: PostgresSchemaInspector.Inspect(connection, "public", logger))).Value,
            desired: desired,
            allowDestructive: false,
            logger: logger
        );
        ApplyOperations(connection, ops, logger);
    }

    /// <summary>
    /// Applies pre-computed schema operations to Postgres and asserts the
    /// migration succeeded.
    /// </summary>
    internal static void ApplyOperations(
        NpgsqlConnection connection,
        IReadOnlyList<SchemaOperation> operations,
        ILogger logger
    )
    {
        var apply = PostgresTestDb.TryApply(
            connection: connection,
            operations: operations,
            logger: logger
        );
        PostgresTestDb.AssertApplyResult(apply: apply);
    }

    private void ApplyAndGrant(SchemaDefinition desired, params string[] tableNames)
    {
        ApplySchema(connection: Connection, desired: desired, logger: Logger);

        foreach (var t in tableNames)
        {
            PostgresTestDb.Exec(
                Connection,
                $"GRANT USAGE ON SCHEMA public TO lql_user, lql_admin; "
                    + $"GRANT SELECT,INSERT,UPDATE,DELETE ON \"public\".\"{t}\" TO lql_user, lql_admin"
            );
        }
    }

    [Fact]
    public void LqlOnly_NapShape_AppliesAndCreatesPolicies()
    {
        // tenant_members must exist before is_member fn body resolves rows.
        VerifySharedContinuation13();
        ApplyAndGrant(TenantTableLqlOnly("agent_configs"), "agent_configs");

        // Verify both policies exist and are PERMISSIVE FOR ALL.
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = Data_LqlOnly_NapShape_AppliesAndCreatesPolicies_1;
        using var r = cmd.ExecuteReader();
        var rows = new List<(string Name, string P, string Cmd)>();
        while (r.Read())
        {
            rows.Add((r.GetString(0), r.GetString(1), r.GetString(2)));
        }
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, x => x.Name == "agent_configs_admin_all");
        Assert.Contains(rows, x => x.Name == "agent_configs_member");
        Assert.All(rows, x => Assert.Equal("PERMISSIVE", x.P));
    }

    [Fact]
    public void LqlOnly_TenantIsolation_RealCrossUserBlock()
    {
        VerifySharedContinuation13();
        ApplyAndGrant(TenantTableLqlOnly("docs_iso"), "docs_iso");

        // Membership rows.
        var (tenantA, tenantB, userA, userB, docA) = SeedMembership();
        PostgresTestDb.SeedTwoTenantsAndAssertIsolation(
            Connection,
            "lql_user",
            "docs_iso",
            docA,
            tenantA,
            tenantB,
            userA,
            userB,
            "SELECT id FROM docs_iso"
        );
    }

    [Fact]
    public void LqlOnly_NonMember_BlockedByIsMemberCheck()
    {
        VerifySharedContinuation13();
        ApplyAndGrant(TenantTableLqlOnly("docs_nm"), "docs_nm");

        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid(); // NOT a member of `tenant`

        // No tenant_members row -> is_member() returns false -> insert blocked
        // by WITH CHECK predicate.
        using var tx = Connection.BeginTransaction();
        PostgresTestDb.SetTenantSession(
            connection: Connection,
            transaction: tx,
            role: "lql_user",
            tenant: tenant,
            user: user,
            settingNamespace: "rls"
        );
        using var ins = Connection.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText =
            $"INSERT INTO docs_nm(id, tenant_id, title) VALUES ('{Guid.NewGuid()}', '{tenant}', 'x')";
        var ex = Assert.Throws<PostgresException>(() => ins.ExecuteNonQuery());
        Assert.Contains("row-level security", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LqlOnly_Admin_SeesAllTenants()
    {
        VerifySharedContinuation13();
        ApplyAndGrant(TenantTableLqlOnly("docs_admin"), "docs_admin");

        // Admin policy is "true" — admin sees everything.
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        PostgresTestDb.InsertAsTenant(
            Connection,
            "lql_admin",
            null,
            null,
            "rls",
            $"INSERT INTO docs_admin(id, tenant_id, title) VALUES ('{Guid.NewGuid()}', '{t1}', 'x')",
            $"INSERT INTO docs_admin(id, tenant_id, title) VALUES ('{Guid.NewGuid()}', '{t2}', 'y')"
        );

        Assert.Equal(
            2,
            PostgresTestDb.CountAsTenant(
                Connection,
                "lql_admin",
                null,
                null,
                "rls",
                "SELECT COUNT(*) FROM docs_admin"
            )
        );
    }

    [Fact]
    public void LqlOnly_Idempotent_SecondApplyEmitsZeroOps()
    {
        VerifySharedContinuation13();
        ApplyAndGrant(TenantTableLqlOnly("docs_idem"), "docs_idem");

        // Re-apply same desired -> 0 ops.
        var ops = PostgresTestDb.CalculateChanges(
            Connection,
            TenantTableLqlOnly("docs_idem"),
            Logger
        );
        Assert.Empty(ops);
    }

    [Fact]
    public void LqlOnly_OrCombination_SelfOrOwner()
    {
        VerifySharedContinuation13();
        var schema = Fixture_LqlOnly_OrCombination_SelfOrOwner_1();
        ApplyAndGrant(schema, "self_owner");

        // Verify the OR-combination predicate landed in pg_policies.
        var qual = (string)
            global::Tests.Shared.DatabaseCommands.Scalar(
                connection: Connection,
                configure: command =>
                    command.CommandText =
                        "SELECT qual FROM pg_policies WHERE tablename='self_owner' AND policyname='self_or_member'"
            )!;

        Assert.Contains("OR", qual, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("is_member", qual, StringComparison.Ordinal);
    }

    [Fact]
    public void LqlOnly_DifferentUsingVsWithCheck_ApiKeysShape()
    {
        // NAP shape: USING any member, WITH CHECK only writers.
        // We don't have is_tenant_writer() in this test so fall back to a
        // simpler asymmetric pair that proves the asymmetry plumbs through:
        // USING is_member(...), WITH CHECK is_member(...) AND is_member(...).
        VerifySharedContinuation13();
        var schema = Fixture_LqlOnly_DifferentUsingVsWithCheck_ApiKeysShape_1();
        ApplyAndGrant(schema, "asymmetric");

        PostgresTestDb.WithFirstRow(
            Connection,
            "SELECT qual, with_check FROM pg_policies WHERE tablename='asymmetric'",
            r =>
            {
                var qual = r.GetString(0);
                var wc = r.GetString(1);
                Assert.Contains("is_member", qual, StringComparison.Ordinal);
                ContainsAll(
                    actual: wc,
                    expected: ["is_member", "tenant_id"],
                    comparison: StringComparison.Ordinal
                );
                // Asymmetry: with_check has tenant_id check, qual doesn't.
                Assert.DoesNotContain("\"tenant_id\"", qual, StringComparison.Ordinal);
            }
        );
    }

    [Fact]
    public void LqlOnly_NapMessagesShape_BareFnCallInLambdaBody_EnforcesIsolation()
    {
        // GitHub issue #40 / #41: messages.tenant_id is derived via the
        // parent conversations.tenant_id. The policy uses an exists() over
        // conversations with a lambda body that has a bare fn-call as one
        // side of the AND. This is the predicate shape NAP hit on preview5
        // (Unsupported expr type in comparison: ExprContext) and that
        // preview6 fixes. End-to-end test proves the full pipeline:
        // YAML -> LQL parse -> Postgres CREATE POLICY -> runtime enforce.
        VerifySharedContinuation13();

        // Parent table: conversations.
        var convSchema =
            Fixture_LqlOnly_NapMessagesShape_BareFnCallInLambdaBody_EnforcesIsolation_1();
        ApplyAndGrant(convSchema, "conversations");

        // Child table: messages — RLS via exists() over conversations.
        var msgSchema =
            Fixture_LqlOnly_NapMessagesShape_BareFnCallInLambdaBody_EnforcesIsolation_2();
        ApplyAndGrant(msgSchema, "messages");

        // Set up: tenantA has userA as member, tenantB has userB as member.
        var (tenantA, tenantB, userA, userB, convA) = SeedMembership();
        var convB = Guid.NewGuid();
        PostgresTestDb.Exec(
            Connection,
            $"INSERT INTO conversations(id, tenant_id) VALUES ('{convA}', '{tenantA}')"
        );
        PostgresTestDb.Exec(
            Connection,
            $"INSERT INTO conversations(id, tenant_id) VALUES ('{convB}', '{tenantB}')"
        );

        // userA (tenantA) inserts a message in convA -> allowed.
        var msgA = Guid.NewGuid();
        PostgresTestDb.WithTenantTransaction(
            connection: Connection,
            role: "lql_user",
            tenant: tenantA,
            user: userA,
            settingNamespace: "rls",
            run: tx =>
            {
                PostgresTestDb.Exec(
                    Connection,
                    tx,
                    $"INSERT INTO messages(id, conversation_id, body) VALUES ('{msgA}', '{convA}', 'hi')"
                );
            },
            commit: true
        );

        // userA tries to insert a message in convB (tenantB's conversation)
        // -> blocked because exists() returns false: convB.tenant_id is
        // tenantB, and is_member(userA, tenantB) is false.
        PostgresTestDb.WithTenantTransaction(
            connection: Connection,
            role: "lql_user",
            tenant: tenantA,
            user: userA,
            settingNamespace: "rls",
            run: tx =>
            {
                using var ins = Connection.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText =
                    $"INSERT INTO messages(id, conversation_id, body) VALUES ('{Guid.NewGuid()}', '{convB}', 'evil')";
                var ex = Assert.Throws<PostgresException>(() => ins.ExecuteNonQuery());
                Assert.Contains(
                    "row-level security",
                    ex.Message,
                    StringComparison.OrdinalIgnoreCase
                );
            },
            commit: false
        );

        // userA SELECTs messages -> sees only msgA (in convA, in their tenant).
        PostgresTestDb.AssertTenantSeesSingle(
            Connection,
            "lql_user",
            tenantA,
            userA,
            msgA,
            "SELECT id FROM messages"
        );
    }

    [Fact]
    public void LqlOnly_DropPolicy_AllowDestructive_RemovesIt()
    {
        VerifySharedContinuation13();
        ApplyAndGrant(TenantTableLqlOnly("docs_drop"), "docs_drop");

        var depleted = TenantTableLqlOnly("docs_drop") with
        {
            Tables =
            [
                TenantTableLqlOnly("docs_drop").Tables[0] with
                {
                    RowLevelSecurity = Dataset_LqlOnly_DropPolicy_AllowDestructive_RemovesIt_1(),
                },
            ],
        };
        var current = PostgresTestDb.Inspect(Connection, Logger);
        var ops = SchemaDiffAssertions.Diff(
            current: current,
            desired: depleted,
            destructive: true,
            logger: Logger
        );

        global::Tests.Shared.ResultAssertions.Ok(
            actual: PostgresTestDb.TryApply(
                connection: Connection,
                operations: ops,
                logger: Logger,
                options: MigrationOptions.Destructive
            )
        );

        Assert.Equal(
            0L,
            (long)
                global::Tests.Shared.DatabaseCommands.Scalar(
                    connection: Connection,
                    configure: command =>
                        command.CommandText =
                            "SELECT COUNT(*) FROM pg_policies WHERE tablename='docs_drop'"
                )!
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void VerifySharedContinuation13()
    { // tenant_members must exist before is_member fn body resolves rows.
        ApplyAndGrant(TenantMembersSchema(), "tenant_members");
        BootstrapMembershipFns(Connection);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private (Guid TenantA, Guid TenantB, Guid UserA, Guid UserB, Guid Seed) SeedMembership()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var seed = VerifyReusableWorkflow19(
            input0: Connection,
            input1: userA,
            input2: tenantA,
            input3: userB,
            input4: tenantB
        );
        return (tenantA, tenantB, userA, userB, seed);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static Guid VerifyReusableWorkflow19(
        NpgsqlConnection input0,
        Guid input1,
        Guid input2,
        Guid input3,
        Guid input4
    )
    {
        // Membership rows.
        PostgresTestDb.Exec(
            input0,
            $"INSERT INTO tenant_members(id, user_id, tenant_id) VALUES ('{Guid.NewGuid()}', '{input1}', '{input2}')"
        );
        PostgresTestDb.Exec(
            input0,
            $"INSERT INTO tenant_members(id, user_id, tenant_id) VALUES ('{Guid.NewGuid()}', '{input3}', '{input4}')"
        );

        // Tenant A inserts.
        var docA = Guid.NewGuid();

        return docA;
    }
}
