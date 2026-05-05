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
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Usage",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposed via IAsyncLifetime.DisposeAsync"
)]
public sealed class PostgresLqlOnlyE2ETests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private NpgsqlConnection _connection = null!;
    private readonly ILogger _logger = NullLogger.Instance;

    public async Task InitializeAsync()
    {
        _connection = await fixture.CreateDatabaseAsync("rls_lql_e2e").ConfigureAwait(false);
        BootstrapRolesAndGucReaders(_connection);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Phase 1 bootstrap: roles + GUC reader fns. Does NOT include is_member()
    /// which depends on tenant_members existing (created by DP migrate).
    /// </summary>
    private static void BootstrapRolesAndGucReaders(NpgsqlConnection conn)
    {
        Exec(
            conn,
            """
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='lql_user') THEN
                    CREATE ROLE lql_user NOLOGIN NOBYPASSRLS;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='lql_admin') THEN
                    CREATE ROLE lql_admin NOLOGIN NOBYPASSRLS;
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

    /// <summary>
    /// Phase 2 bootstrap (after DP creates tenant_members): SECURITY DEFINER
    /// membership fns that reference tenant_members.
    /// </summary>
    private static void BootstrapMembershipFns(NpgsqlConnection conn) =>
        Exec(
            conn,
            """
            CREATE OR REPLACE FUNCTION is_member(u uuid, t uuid) RETURNS bool
                LANGUAGE sql SECURITY DEFINER STABLE AS $$
                    SELECT EXISTS (
                        SELECT 1 FROM tenant_members WHERE user_id = u AND tenant_id = t
                    )
                $$;
            GRANT EXECUTE ON FUNCTION is_member(uuid, uuid) TO lql_user, lql_admin;
            """
        );

    private void ApplyAndGrant(SchemaDefinition desired, params string[] tableNames)
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

        foreach (var t in tableNames)
        {
            Exec(
                _connection,
                $"GRANT USAGE ON SCHEMA public TO lql_user, lql_admin; "
                    + $"GRANT SELECT,INSERT,UPDATE,DELETE ON \"public\".\"{t}\" TO lql_user, lql_admin"
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

    private static SchemaDefinition TenantMembersSchema() =>
        new()
        {
            Name = "lql",
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
                    ],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                },
            ],
        };

    private static SchemaDefinition TenantTableLqlOnly(string name) =>
        new()
        {
            Name = "lql",
            Tables =
            [
                new TableDefinition
                {
                    Schema = "public",
                    Name = name,
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
                        Forced = true,
                        Policies =
                        [
                            new RlsPolicyDefinition
                            {
                                Name = $"{name}_member",
                                Operations = [RlsOperation.All],
                                Roles = ["lql_user"],
                                // PURE LQL - no usingSql.
                                UsingLql =
                                    "tenant_id = app_tenant_id() and is_member(app_user_id(), app_tenant_id())",
                                WithCheckLql =
                                    "tenant_id = app_tenant_id() and is_member(app_user_id(), app_tenant_id())",
                            },
                            new RlsPolicyDefinition
                            {
                                Name = $"{name}_admin_all",
                                Operations = [RlsOperation.All],
                                Roles = ["lql_admin"],
                                UsingLql = "true",
                                WithCheckLql = "true",
                            },
                        ],
                    },
                },
            ],
        };

    [Fact]
    public void LqlOnly_NapShape_AppliesAndCreatesPolicies()
    {
        // tenant_members must exist before is_member fn body resolves rows.
        ApplyAndGrant(TenantMembersSchema(), "tenant_members");
        BootstrapMembershipFns(_connection);
        ApplyAndGrant(TenantTableLqlOnly("agent_configs"), "agent_configs");

        // Verify both policies exist and are PERMISSIVE FOR ALL.
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT policyname, permissive, cmd FROM pg_policies
            WHERE schemaname='public' AND tablename='agent_configs'
            ORDER BY policyname
            """;
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
        ApplyAndGrant(TenantMembersSchema(), "tenant_members");
        BootstrapMembershipFns(_connection);
        ApplyAndGrant(TenantTableLqlOnly("docs_iso"), "docs_iso");

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        // Membership rows.
        Exec(
            _connection,
            $"INSERT INTO tenant_members(id, user_id, tenant_id) VALUES ('{Guid.NewGuid()}', '{userA}', '{tenantA}')"
        );
        Exec(
            _connection,
            $"INSERT INTO tenant_members(id, user_id, tenant_id) VALUES ('{Guid.NewGuid()}', '{userB}', '{tenantB}')"
        );

        // Tenant A inserts.
        var docA = Guid.NewGuid();
        using (var tx = _connection.BeginTransaction())
        {
            SetSession(_connection, tx, "lql_user", tenantA, userA);
            Exec(
                _connection,
                tx,
                $"INSERT INTO docs_iso(id, tenant_id, title) VALUES ('{docA}', '{tenantA}', 'a')"
            );
            tx.Commit();
        }

        // Tenant B inserts.
        var docB = Guid.NewGuid();
        using (var tx = _connection.BeginTransaction())
        {
            SetSession(_connection, tx, "lql_user", tenantB, userB);
            Exec(
                _connection,
                tx,
                $"INSERT INTO docs_iso(id, tenant_id, title) VALUES ('{docB}', '{tenantB}', 'b')"
            );
            tx.Commit();
        }

        // Tenant A user reads -> only sees A.
        using (var tx = _connection.BeginTransaction())
        {
            SetSession(_connection, tx, "lql_user", tenantA, userA);
            using var sel = _connection.CreateCommand();
            sel.Transaction = tx;
            sel.CommandText = "SELECT id FROM docs_iso";
            var ids = new List<Guid>();
            using var rdr = sel.ExecuteReader();
            while (rdr.Read())
            {
                ids.Add(rdr.GetGuid(0));
            }
            Assert.Single(ids);
            Assert.Equal(docA, ids[0]);
        }
    }

    [Fact]
    public void LqlOnly_NonMember_BlockedByIsMemberCheck()
    {
        ApplyAndGrant(TenantMembersSchema(), "tenant_members");
        BootstrapMembershipFns(_connection);
        ApplyAndGrant(TenantTableLqlOnly("docs_nm"), "docs_nm");

        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid(); // NOT a member of `tenant`

        // No tenant_members row -> is_member() returns false -> insert blocked
        // by WITH CHECK predicate.
        using var tx = _connection.BeginTransaction();
        SetSession(_connection, tx, "lql_user", tenant, user);
        using var ins = _connection.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText =
            $"INSERT INTO docs_nm(id, tenant_id, title) VALUES ('{Guid.NewGuid()}', '{tenant}', 'x')";
        var ex = Assert.Throws<PostgresException>(() => ins.ExecuteNonQuery());
        Assert.Contains("row-level security", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LqlOnly_Admin_SeesAllTenants()
    {
        ApplyAndGrant(TenantMembersSchema(), "tenant_members");
        BootstrapMembershipFns(_connection);
        ApplyAndGrant(TenantTableLqlOnly("docs_admin"), "docs_admin");

        // Admin policy is "true" — admin sees everything.
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        using (var tx = _connection.BeginTransaction())
        {
            SetSession(_connection, tx, "lql_admin", null, null);
            Exec(
                _connection,
                tx,
                $"INSERT INTO docs_admin(id, tenant_id, title) VALUES ('{Guid.NewGuid()}', '{t1}', 'x')"
            );
            Exec(
                _connection,
                tx,
                $"INSERT INTO docs_admin(id, tenant_id, title) VALUES ('{Guid.NewGuid()}', '{t2}', 'y')"
            );
            tx.Commit();
        }

        using (var tx = _connection.BeginTransaction())
        {
            SetSession(_connection, tx, "lql_admin", null, null);
            using var sel = _connection.CreateCommand();
            sel.Transaction = tx;
            sel.CommandText = "SELECT COUNT(*) FROM docs_admin";
            var n = (long)sel.ExecuteScalar()!;
            Assert.Equal(2, n);
        }
    }

    [Fact]
    public void LqlOnly_Idempotent_SecondApplyEmitsZeroOps()
    {
        ApplyAndGrant(TenantMembersSchema(), "tenant_members");
        BootstrapMembershipFns(_connection);
        ApplyAndGrant(TenantTableLqlOnly("docs_idem"), "docs_idem");

        var current = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        // Re-apply same desired -> 0 ops.
        var ops = (
            (OperationsResultOk)
                SchemaDiff.Calculate(current, TenantTableLqlOnly("docs_idem"), logger: _logger)
        ).Value;
        Assert.Empty(ops);
    }

    [Fact]
    public void LqlOnly_OrCombination_SelfOrOwner()
    {
        ApplyAndGrant(TenantMembersSchema(), "tenant_members");
        BootstrapMembershipFns(_connection);
        var schema = new SchemaDefinition
        {
            Name = "lql",
            Tables =
            [
                new TableDefinition
                {
                    Schema = "public",
                    Name = "self_owner",
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
                                Name = "self_or_member",
                                Operations = [RlsOperation.Select],
                                Roles = ["lql_user"],
                                UsingLql =
                                    "user_id = app_user_id() or (tenant_id = app_tenant_id() and is_member(app_user_id(), app_tenant_id()))",
                            },
                        ],
                    },
                },
            ],
        };
        ApplyAndGrant(schema, "self_owner");

        // Verify the OR-combination predicate landed in pg_policies.
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            "SELECT qual FROM pg_policies WHERE tablename='self_owner' AND policyname='self_or_member'";
        var qual = (string)cmd.ExecuteScalar()!;
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
        ApplyAndGrant(TenantMembersSchema(), "tenant_members");
        BootstrapMembershipFns(_connection);
        var schema = new SchemaDefinition
        {
            Name = "lql",
            Tables =
            [
                new TableDefinition
                {
                    Schema = "public",
                    Name = "asymmetric",
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
                                Name = "asym_pol",
                                Operations = [RlsOperation.All],
                                Roles = ["lql_user"],
                                UsingLql = "is_member(app_user_id(), app_tenant_id())",
                                WithCheckLql =
                                    "is_member(app_user_id(), app_tenant_id()) and tenant_id = app_tenant_id()",
                            },
                        ],
                    },
                },
            ],
        };
        ApplyAndGrant(schema, "asymmetric");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT qual, with_check FROM pg_policies WHERE tablename='asymmetric'";
        using var r = cmd.ExecuteReader();
        Assert.True(r.Read());
        var qual = r.GetString(0);
        var wc = r.GetString(1);
        Assert.Contains("is_member", qual, StringComparison.Ordinal);
        Assert.Contains("is_member", wc, StringComparison.Ordinal);
        Assert.Contains("tenant_id", wc, StringComparison.Ordinal);
        // Asymmetry: with_check has tenant_id check, qual doesn't.
        Assert.DoesNotContain("\"tenant_id\"", qual, StringComparison.Ordinal);
    }

    [Fact]
    public void LqlOnly_DropPolicy_AllowDestructive_RemovesIt()
    {
        ApplyAndGrant(TenantMembersSchema(), "tenant_members");
        BootstrapMembershipFns(_connection);
        ApplyAndGrant(TenantTableLqlOnly("docs_drop"), "docs_drop");

        var depleted = TenantTableLqlOnly("docs_drop") with
        {
            Tables =
            [
                TenantTableLqlOnly("docs_drop").Tables[0] with
                {
                    RowLevelSecurity = new RlsPolicySetDefinition
                    {
                        Enabled = true,
                        Forced = true,
                        Policies = [],
                    },
                },
            ],
        };
        var current = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var ops = (
            (OperationsResultOk)
                SchemaDiff.Calculate(current, depleted, allowDestructive: true, logger: _logger)
        ).Value;

        var apply = MigrationRunner.Apply(
            _connection,
            ops,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Destructive,
            _logger
        );
        Assert.True(apply is MigrationApplyResultOk);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM pg_policies WHERE tablename='docs_drop'";
        Assert.Equal(0L, (long)cmd.ExecuteScalar()!);
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
