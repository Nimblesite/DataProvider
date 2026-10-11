// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

// EXTREME E2E for NimblesiteAgenticPlatform (NAP) — proves issues #32, #36, #37
// against a real Postgres container. Mirrors NAP's bootstrap.py shapes:
// 13 tenant-scoped tables × 2 policies (member + admin_all), SECURITY DEFINER
// is_member() function, FORCE ROW LEVEL SECURITY, two GUC session contexts
// (rls.user_id + rls.tenant_id), and idempotent + drift-aware re-apply.

/// <summary>
/// EXTREME end-to-end RLS tests proving the migration tool unblocks NAP's
/// production threat model. Every test runs against a real Postgres container.
/// </summary>
[Collection(PostgresTestSuite.Name)]
public sealed partial class PostgresRlsNapShapeTests(PostgresContainerFixture fixture)
    : PostgresDatabaseTest(fixture)
{
    protected override string DatabaseName => "rls_nap";

    protected override void OnDatabaseCreated() => BootstrapNapPrelude(Connection);

    /// <summary>
    /// Mirrors NAP's bootstrap.py — creates the SECURITY DEFINER fn and GUC
    /// reader fns the policies depend on, plus the non-bypassrls app role.
    /// </summary>
    private static void BootstrapNapPrelude(NpgsqlConnection conn)
    {
        PostgresTestDb.Exec(conn, Data_BootstrapNapPrelude_1);
    }

    private static readonly string[] TenantTableNames =
    [
        "agent_configs",
        "conversations",
        "messages",
        "api_keys",
    ];

    private static SchemaDefinition NapSchema()
    {
        // 4 tenant-scoped tables (subset of NAP's 13 — same shape).
        var tables = TenantTableNames.Select(MakeTenantTable).ToList();
        return SchemaTestData.Schema(name: "nap", tables: tables);
    }

    private void ApplyAndGrant(SchemaDefinition desired)
    {
        PostgresLqlOnlyE2ETests.ApplySchema(
            connection: Connection,
            desired: desired,
            logger: Logger
        );

        // Grant CRUD on each table to the app roles (NAP would do this in its
        // own bootstrap; we replicate it here to make the test runnable).
        foreach (var t in desired.Tables)
        {
            PostgresTestDb.Exec(
                Connection,
                $"GRANT USAGE ON SCHEMA public TO nap_app_user, nap_app_admin; "
                    + $"GRANT SELECT,INSERT,UPDATE,DELETE ON \"public\".\"{t.Name}\" TO nap_app_user, nap_app_admin"
            );
        }
    }

    [Fact]
    public void NapShape_FourTenantTables_AppliesCleanlyAndIsForced()
    {
        ApplyAndGrant(NapSchema());

        using var cmd = Connection.CreateCommand();
        cmd.CommandText = Data_NapShape_FourTenantTables_AppliesCleanlyAndIsForced_1;
        using var r = cmd.ExecuteReader();
        var rows = new List<(string Name, bool Enabled, bool Forced)>();
        while (r.Read())
        {
            rows.Add((r.GetString(0), r.GetBoolean(1), r.GetBoolean(2)));
        }
        Assert.Equal(4, rows.Count);
        Assert.All(rows, row => Assert.True(row.Enabled, $"{row.Name} not RLS-enabled"));
        Assert.All(rows, row => Assert.True(row.Forced, $"{row.Name} not FORCE'd"));
    }

    [Fact]
    public void NapShape_TenantIsolation_AppUserBlockedAcrossTenants()
    {
        ApplyAndGrant(NapSchema());

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        // Tenant A user inserts a config.
        var configA = Guid.NewGuid();
        PostgresTestDb.SeedTwoTenantsAndAssertIsolation(
            Connection,
            "nap_app_user",
            "public.agent_configs",
            configA,
            tenantA,
            tenantB,
            userA,
            userB,
            "SELECT id FROM public.agent_configs"
        );
    }

    [Fact]
    public void NapShape_AdminRole_SeesEverything()
    {
        ApplyAndGrant(NapSchema());

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        // Insert as admin (admin_all policy USING true).
        PostgresTestDb.InsertAsTenant(
            Connection,
            "nap_app_admin",
            null,
            null,
            "rls",
            $"INSERT INTO public.agent_configs(id, tenant_id, title) VALUES ('{Guid.NewGuid()}', '{tenantA}', 'admin1')",
            $"INSERT INTO public.agent_configs(id, tenant_id, title) VALUES ('{Guid.NewGuid()}', '{tenantB}', 'admin2')"
        );

        Assert.Equal(
            2,
            PostgresTestDb.CountAsTenant(
                Connection,
                "nap_app_admin",
                null,
                null,
                "rls",
                "SELECT COUNT(*) FROM public.agent_configs"
            )
        );
    }

    [Fact]
    public void NapShape_Idempotent_SecondApplyEmitsZeroOps()
    {
        ApplyAndGrant(NapSchema());

        var ops = PostgresTestDb.CalculateChanges(Connection, NapSchema(), Logger);

        Assert.Empty(ops);
    }

    [Fact]
    public void NapShape_DriftRename_AllowDestructiveDropsOldAndCreatesNew()
    {
        ApplyAndGrant(NapSchema());

        // Rename '*_admin_all' to '*_admin_full' on every table.
        var renamed = SchemaTestData.Schema(
            name: "nap",
            tables: NapSchema()
                .Tables.Select(t =>
                    t with
                    {
                        RowLevelSecurity = SchemaTestData.RlsPolicySet(
                            enabled: true,
                            forced: true,
                            policies: t.RowLevelSecurity!.Policies.Select(p =>
                                    p.Name.EndsWith("_admin_all", StringComparison.Ordinal)
                                        ? p with
                                        {
                                            Name = p.Name.Replace(
                                                "_admin_all",
                                                "_admin_full",
                                                StringComparison.Ordinal
                                            ),
                                        }
                                        : p
                                )
                                .ToList()
                        ),
                    }
                )
                .ToList()
        );

        var current = PostgresTestDb.Inspect(Connection, Logger);
        var ops = (
            (OperationsResultOk)
                SchemaDiff.Calculate(current, renamed, allowDestructive: true, logger: Logger)
        ).Value;

        var drops = ops.OfType<DropRlsPolicyOperation>().ToList();
        var creates = ops.OfType<CreateRlsPolicyOperation>().ToList();

        Assert.Equal(4, drops.Count);
        Assert.Equal(4, creates.Count);
        Assert.All(
            drops,
            d => Assert.EndsWith("_admin_all", d.PolicyName, StringComparison.Ordinal)
        );
        Assert.All(
            creates,
            c => Assert.EndsWith("_admin_full", c.Policy.Name, StringComparison.Ordinal)
        );
    }

    [Fact]
    public void NapShape_Drift_ForwardOnly_DoesNotDropOrphan()
    {
        ApplyAndGrant(NapSchema());

        var depleted = SchemaTestData.Schema(
            name: "nap",
            tables: NapSchema()
                .Tables.Select(t =>
                    t with
                    {
                        RowLevelSecurity = Dataset_NapShape_Drift_ForwardOnly_DoesNotDropOrphan_1(
                            t: t
                        ),
                    }
                )
                .ToList()
        );

        var ops = PostgresTestDb.CalculateChanges(Connection, depleted, Logger);

        Assert.DoesNotContain(ops, o => o is DropRlsPolicyOperation);
    }

    [Fact]
    public void NapShape_OrCombinationPredicate_AppliesCorrectly()
    {
        // tenant_members_self_or_owner shape:
        // user_id = app_user_id() OR (tenant_id = app_tenant_id() AND is_owner)
        var schema = Fixture_NapShape_OrCombinationPredicate_AppliesCorrectly_1();
        ApplyAndGrant(schema);

        var inspected = PostgresTestDb.Inspect(Connection, Logger);
        var policy = inspected
            .Tables.Single(t => t.Name == "tenant_members")
            .RowLevelSecurity!.Policies.Single();
        Assert.Contains("OR", policy.UsingSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("app_user_id", policy.UsingSql, StringComparison.Ordinal);
    }

    [Fact]
    public void NapShape_DropForceRls_RequiresAllowDestructive()
    {
        ApplyAndGrant(NapSchema());

        var unforced = SchemaTestData.Schema(
            name: "nap",
            tables: NapSchema()
                .Tables.Select(t =>
                    t with
                    {
                        RowLevelSecurity = t.RowLevelSecurity! with { Forced = false },
                    }
                )
                .ToList()
        );

        var current = PostgresTestDb.Inspect(Connection, Logger);
        var safeOps = PostgresTestDb.CalculateChanges(Connection, unforced, Logger);
        Assert.DoesNotContain(safeOps, o => o is DisableForceRlsOperation);

        var destructiveOps = (
            (OperationsResultOk)
                SchemaDiff.Calculate(current, unforced, allowDestructive: true, logger: Logger)
        ).Value;
        Assert.Equal(4, destructiveOps.OfType<DisableForceRlsOperation>().Count());
    }

    [Fact]
    public void NapShape_Stress_HundredRowsAcrossTenants_PerUserCountsCorrect()
    {
        ApplyAndGrant(NapSchema());

        var tenants = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        var inserted = new Dictionary<Guid, int>();
        foreach (var t in tenants)
            inserted[t] = 0;

        for (var i = 0; i < 100; i++)
        {
            // Round-robin across tenants -- deterministic distribution, no RNG needed.
            var tenant = tenants[i % tenants.Count];
            PostgresTestDb.InsertAsTenant(
                Connection,
                "nap_app_user",
                tenant,
                Guid.NewGuid(),
                "rls",
                $"INSERT INTO public.agent_configs(id, tenant_id, title) VALUES ('{Guid.NewGuid()}', '{tenant}', 'row{i}')"
            );
            inserted[tenant]++;
        }

        foreach (var tenant in tenants)
        {
            Assert.Equal(
                inserted[tenant],
                (int)
                    PostgresTestDb.CountAsTenant(
                        Connection,
                        "nap_app_user",
                        tenant,
                        Guid.NewGuid(),
                        "rls",
                        "SELECT COUNT(*) FROM public.agent_configs"
                    )
            );
        }
    }
}
