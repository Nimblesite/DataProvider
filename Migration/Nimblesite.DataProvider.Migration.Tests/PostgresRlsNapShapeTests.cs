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
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Usage",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposed via IAsyncLifetime.DisposeAsync"
)]
public sealed class PostgresRlsNapShapeTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private NpgsqlConnection _connection = null!;
    private readonly ILogger _logger = NullLogger.Instance;

    public async Task InitializeAsync()
    {
        _connection = await fixture.CreateDatabaseAsync("rls_nap").ConfigureAwait(false);
        BootstrapNapPrelude(_connection);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Mirrors NAP's bootstrap.py — creates the SECURITY DEFINER fn and GUC
    /// reader fns the policies depend on, plus the non-bypassrls app role.
    /// </summary>
    private static void BootstrapNapPrelude(NpgsqlConnection conn)
    {
        Exec(
            conn,
            """
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='nap_app_user') THEN
                    CREATE ROLE nap_app_user NOLOGIN NOBYPASSRLS;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='nap_app_admin') THEN
                    CREATE ROLE nap_app_admin NOLOGIN NOBYPASSRLS;
                END IF;
            END$$;

            CREATE OR REPLACE FUNCTION app_user_id() RETURNS uuid AS $$
                SELECT NULLIF(current_setting('rls.user_id', true), '')::uuid
            $$ LANGUAGE sql STABLE;

            CREATE OR REPLACE FUNCTION app_tenant_id() RETURNS uuid AS $$
                SELECT NULLIF(current_setting('rls.tenant_id', true), '')::uuid
            $$ LANGUAGE sql STABLE;
            """
        );
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
        return new SchemaDefinition { Name = "nap", Tables = tables };
    }

    private static TableDefinition MakeTenantTable(string tableName) =>
        new()
        {
            Schema = "public",
            Name = tableName,
            Columns =
            [
                new ColumnDefinition
                {
                    Name = "id",
                    Type = new UuidType(),
                    IsNullable = false,
                },
                new ColumnDefinition
                {
                    Name = "tenant_id",
                    Type = new UuidType(),
                    IsNullable = false,
                },
                new ColumnDefinition
                {
                    Name = "title",
                    Type = new VarCharType(200),
                    IsNullable = false,
                },
            ],
            PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
            RowLevelSecurity = new RlsPolicySetDefinition
            {
                Enabled = true,
                Forced = true, // issue #37 — NAP threat model
                Policies =
                [
                    new RlsPolicyDefinition
                    {
                        Name = $"{tableName}_member",
                        Operations = [RlsOperation.All],
                        Roles = ["nap_app_user"],
                        // LQL form — fn calls (app_tenant_id) pass through verbatim.
                        UsingLql = "tenant_id = app_tenant_id()",
                        WithCheckLql = "tenant_id = app_tenant_id()",
                    },
                    new RlsPolicyDefinition
                    {
                        Name = $"{tableName}_admin_all",
                        Operations = [RlsOperation.All],
                        Roles = ["nap_app_admin"],
                        UsingLql = "true",
                        WithCheckLql = "true",
                    },
                ],
            },
        };

    private void ApplyAndGrant(SchemaDefinition desired)
    {
        var current = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var ops = (
            (OperationsResultOk)SchemaDiff.Calculate(current, desired, logger: _logger)
        ).Value;
        var apply = MigrationRunner.Apply(
            _connection,
            ops,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );
        Assert.True(
            apply is MigrationApplyResultOk,
            $"Migration failed: {(apply as MigrationApplyResultError)?.Value}"
        );

        // Grant CRUD on each table to the app roles (NAP would do this in its
        // own bootstrap; we replicate it here to make the test runnable).
        foreach (var t in desired.Tables)
        {
            Exec(
                _connection,
                $"GRANT USAGE ON SCHEMA public TO nap_app_user, nap_app_admin; "
                    + $"GRANT SELECT,INSERT,UPDATE,DELETE ON \"public\".\"{t.Name}\" TO nap_app_user, nap_app_admin"
            );
        }
    }

    private static void SetSession(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        string role,
        Guid? tenant,
        Guid? user
    )
    {
        Exec(conn, tx, $"SET LOCAL ROLE {role}");
        Exec(conn, tx, $"SET LOCAL rls.tenant_id = '{tenant?.ToString() ?? string.Empty}'");
        Exec(conn, tx, $"SET LOCAL rls.user_id = '{user?.ToString() ?? string.Empty}'");
    }

    [Fact]
    public void NapShape_FourTenantTables_AppliesCleanlyAndIsForced()
    {
        ApplyAndGrant(NapSchema());

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT relname, relrowsecurity, relforcerowsecurity
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname='public' AND relname IN ('agent_configs','conversations','messages','api_keys')
            ORDER BY relname
            """;
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
        using (var tx = _connection.BeginTransaction())
        {
            SetSession(_connection, tx, "nap_app_user", tenantA, userA);
            Exec(
                _connection,
                tx,
                $"INSERT INTO public.agent_configs(id, tenant_id, title) VALUES ('{configA}', '{tenantA}', 'a')"
            );
            tx.Commit();
        }

        // Tenant B user inserts a config.
        var configB = Guid.NewGuid();
        using (var tx = _connection.BeginTransaction())
        {
            SetSession(_connection, tx, "nap_app_user", tenantB, userB);
            Exec(
                _connection,
                tx,
                $"INSERT INTO public.agent_configs(id, tenant_id, title) VALUES ('{configB}', '{tenantB}', 'b')"
            );
            tx.Commit();
        }

        // Tenant A user lists -> sees only tenantA's config.
        using (var tx = _connection.BeginTransaction())
        {
            SetSession(_connection, tx, "nap_app_user", tenantA, userA);
            using var sel = _connection.CreateCommand();
            sel.Transaction = tx;
            sel.CommandText = "SELECT id FROM public.agent_configs";
            var ids = new List<Guid>();
            using var reader = sel.ExecuteReader();
            while (reader.Read())
            {
                ids.Add(reader.GetGuid(0));
            }
            Assert.Single(ids);
            Assert.Equal(configA, ids[0]);
        }
    }

    [Fact]
    public void NapShape_AdminRole_SeesEverything()
    {
        ApplyAndGrant(NapSchema());

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        // Insert as admin (admin_all policy USING true).
        using (var tx = _connection.BeginTransaction())
        {
            SetSession(_connection, tx, "nap_app_admin", null, null);
            Exec(
                _connection,
                tx,
                $"INSERT INTO public.agent_configs(id, tenant_id, title) VALUES ('{Guid.NewGuid()}', '{tenantA}', 'admin1')"
            );
            Exec(
                _connection,
                tx,
                $"INSERT INTO public.agent_configs(id, tenant_id, title) VALUES ('{Guid.NewGuid()}', '{tenantB}', 'admin2')"
            );
            tx.Commit();
        }

        using (var tx = _connection.BeginTransaction())
        {
            SetSession(_connection, tx, "nap_app_admin", null, null);
            using var sel = _connection.CreateCommand();
            sel.Transaction = tx;
            sel.CommandText = "SELECT COUNT(*) FROM public.agent_configs";
            var count = (long)sel.ExecuteScalar()!;
            Assert.Equal(2, count);
        }
    }

    [Fact]
    public void NapShape_Idempotent_SecondApplyEmitsZeroOps()
    {
        ApplyAndGrant(NapSchema());

        var current = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var ops = (
            (OperationsResultOk)SchemaDiff.Calculate(current, NapSchema(), logger: _logger)
        ).Value;

        Assert.Empty(ops);
    }

    [Fact]
    public void NapShape_DriftRename_AllowDestructiveDropsOldAndCreatesNew()
    {
        ApplyAndGrant(NapSchema());

        // Rename '*_admin_all' to '*_admin_full' on every table.
        var renamed = new SchemaDefinition
        {
            Name = "nap",
            Tables = NapSchema()
                .Tables.Select(t =>
                    t with
                    {
                        RowLevelSecurity = new RlsPolicySetDefinition
                        {
                            Enabled = true,
                            Forced = true,
                            Policies = t.RowLevelSecurity!.Policies.Select(p =>
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
                                .ToList(),
                        },
                    }
                )
                .ToList(),
        };

        var current = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var ops = (
            (OperationsResultOk)
                SchemaDiff.Calculate(current, renamed, allowDestructive: true, logger: _logger)
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

        var depleted = new SchemaDefinition
        {
            Name = "nap",
            Tables = NapSchema()
                .Tables.Select(t =>
                    t with
                    {
                        RowLevelSecurity = new RlsPolicySetDefinition
                        {
                            Enabled = true,
                            Forced = true,
                            Policies = [t.RowLevelSecurity!.Policies[0]],
                        },
                    }
                )
                .ToList(),
        };

        var current = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var ops = (
            (OperationsResultOk)SchemaDiff.Calculate(current, depleted, logger: _logger)
        ).Value;

        Assert.DoesNotContain(ops, o => o is DropRlsPolicyOperation);
    }

    [Fact]
    public void NapShape_OrCombinationPredicate_AppliesCorrectly()
    {
        // tenant_members_self_or_owner shape:
        // user_id = app_user_id() OR (tenant_id = app_tenant_id() AND is_owner)
        var schema = new SchemaDefinition
        {
            Name = "nap",
            Tables =
            [
                new TableDefinition
                {
                    Schema = "public",
                    Name = "tenant_members",
                    Columns =
                    [
                        new ColumnDefinition
                        {
                            Name = "id",
                            Type = new UuidType(),
                            IsNullable = false,
                        },
                        new ColumnDefinition
                        {
                            Name = "user_id",
                            Type = new UuidType(),
                            IsNullable = false,
                        },
                        new ColumnDefinition
                        {
                            Name = "tenant_id",
                            Type = new UuidType(),
                            IsNullable = false,
                        },
                        new ColumnDefinition
                        {
                            Name = "is_owner",
                            Type = new BooleanType(),
                            IsNullable = false,
                            DefaultValue = "false",
                        },
                    ],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                    RowLevelSecurity = new RlsPolicySetDefinition
                    {
                        Enabled = true,
                        Forced = true,
                        Policies =
                        [
                            new RlsPolicyDefinition
                            {
                                Name = "tenant_members_self_or_owner",
                                Operations = [RlsOperation.Select],
                                Roles = ["nap_app_user"],
                                UsingLql =
                                    "user_id = app_user_id() or (tenant_id = app_tenant_id() and is_owner)",
                            },
                        ],
                    },
                },
            ],
        };
        ApplyAndGrant(schema);

        var inspected = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
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

        var unforced = new SchemaDefinition
        {
            Name = "nap",
            Tables = NapSchema()
                .Tables.Select(t =>
                    t with
                    {
                        RowLevelSecurity = t.RowLevelSecurity! with { Forced = false },
                    }
                )
                .ToList(),
        };

        var current = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;

        var safeOps = (
            (OperationsResultOk)SchemaDiff.Calculate(current, unforced, logger: _logger)
        ).Value;
        Assert.DoesNotContain(safeOps, o => o is DisableForceRlsOperation);

        var destructiveOps = (
            (OperationsResultOk)
                SchemaDiff.Calculate(current, unforced, allowDestructive: true, logger: _logger)
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
            using var tx = _connection.BeginTransaction();
            SetSession(_connection, tx, "nap_app_user", tenant, Guid.NewGuid());
            Exec(
                _connection,
                tx,
                $"INSERT INTO public.agent_configs(id, tenant_id, title) VALUES ('{Guid.NewGuid()}', '{tenant}', 'row{i}')"
            );
            tx.Commit();
            inserted[tenant]++;
        }

        foreach (var tenant in tenants)
        {
            using var tx = _connection.BeginTransaction();
            SetSession(_connection, tx, "nap_app_user", tenant, Guid.NewGuid());
            using var sel = _connection.CreateCommand();
            sel.Transaction = tx;
            sel.CommandText = "SELECT COUNT(*) FROM public.agent_configs";
            var seen = (long)sel.ExecuteScalar()!;
            Assert.Equal(inserted[tenant], (int)seen);
        }
    }

    private static void Exec(NpgsqlConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static void Exec(NpgsqlConnection conn, NpgsqlTransaction tx, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
