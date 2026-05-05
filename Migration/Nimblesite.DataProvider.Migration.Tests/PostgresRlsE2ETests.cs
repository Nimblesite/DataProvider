using System.Globalization;

namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [RLS-PG] end-to-end tests from docs/specs/rls-spec.md.

/// <summary>
/// E2E tests for the PostgreSQL RLS pipeline against a real Testcontainers
/// postgres instance. Verifies that policies emitted by the migration tool
/// actually enforce row-level access at runtime.
/// </summary>
[Collection(PostgresTestSuite.Name)]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Usage",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposed via IAsyncLifetime.DisposeAsync"
)]
public sealed class PostgresRlsE2ETests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private NpgsqlConnection _connection = null!;
    private readonly ILogger _logger = NullLogger.Instance;

    public async Task InitializeAsync()
    {
        _connection = await fixture.CreateDatabaseAsync("rls_e2e").ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync().ConfigureAwait(false);
    }

    private static SchemaDefinition BuildOwnerIsolationSchema() =>
        new()
        {
            Name = "rls_test",
            Tables =
            [
                new TableDefinition
                {
                    Schema = "public",
                    Name = "documents",
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
                            Name = "owner_id",
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
                        Policies =
                        [
                            new RlsPolicyDefinition
                            {
                                Name = "owner_isolation",
                                Operations = [RlsOperation.All],
                                UsingLql = "owner_id = current_user_id()::uuid",
                                WithCheckLql = "owner_id = current_user_id()::uuid",
                            },
                        ],
                    },
                },
            ],
        };

    private static SchemaDefinition BuildGroupMembershipSchema() =>
        new()
        {
            Name = "rls_group_test",
            Tables =
            [
                new TableDefinition
                {
                    Schema = "public",
                    Name = "user_group_memberships",
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
                            Type = new VarCharType(450),
                            IsNullable = false,
                        },
                        new ColumnDefinition
                        {
                            Name = "group_id",
                            Type = new UuidType(),
                            IsNullable = false,
                        },
                    ],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                },
                new TableDefinition
                {
                    Schema = "public",
                    Name = "documents",
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
                            Name = "group_id",
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
                        Policies =
                        [
                            new RlsPolicyDefinition
                            {
                                Name = "group_read_access",
                                Operations = [RlsOperation.Select],
                                UsingLql = """
                                    exists(
                                      user_group_memberships
                                      |> filter(fn(m) => m.user_id = current_user_id() and m.group_id = documents.group_id)
                                      |> select(id)
                                    )
                                    """,
                            },
                        ],
                    },
                },
            ],
        };

    private const string AppRole = "rls_app_role";

    private void ApplyAndForceRls(SchemaDefinition desired, params string[] tableNames)
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

        // Testcontainers postgres connects as a superuser with BYPASSRLS.
        // To exercise policies we need a non-bypassrls role and grant CRUD.
        using (var roleCmd = _connection.CreateCommand())
        {
            roleCmd.CommandText = $"""
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{AppRole}') THEN
                        CREATE ROLE {AppRole} NOLOGIN NOBYPASSRLS;
                    END IF;
                END$$;
                GRANT USAGE ON SCHEMA public TO {AppRole};
                """;
            roleCmd.ExecuteNonQuery();
        }

        foreach (var tableName in tableNames)
        {
            using var grantCmd = _connection.CreateCommand();
            grantCmd.CommandText =
                $"GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE \"public\".\"{tableName}\" TO {AppRole}";
            grantCmd.ExecuteNonQuery();
        }
    }

    private static void SetAppRoleAndUser(NpgsqlConnection conn, NpgsqlTransaction tx, Guid id)
    {
        using var roleCmd = conn.CreateCommand();
        roleCmd.Transaction = tx;
        roleCmd.CommandText = $"SET LOCAL ROLE {AppRole}";
        roleCmd.ExecuteNonQuery();

        using var setCmd = conn.CreateCommand();
        setCmd.Transaction = tx;
        setCmd.CommandText = string.Create(
            CultureInfo.InvariantCulture,
            $"SET LOCAL rls.current_user_id = '{id}'"
        );
        setCmd.ExecuteNonQuery();
    }

    [Fact]
    public void EnableRls_TableHasRlsEnabled()
    {
        ApplyAndForceRls(BuildOwnerIsolationSchema(), "documents");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT c.relrowsecurity
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public' AND c.relname = 'documents'
            """;
        Assert.True((bool)cmd.ExecuteScalar()!);
    }

    [Fact]
    public void OwnerIsolationPolicy_BlocksCrossUserSelect()
    {
        ApplyAndForceRls(BuildOwnerIsolationSchema(), "documents");

        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var aliceDoc = Guid.NewGuid();
        var bobDoc = Guid.NewGuid();

        // Alice inserts her doc.
        using (var tx = _connection.BeginTransaction())
        {
            SetAppRoleAndUser(_connection, tx, alice);
            using var ins = _connection.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText =
                "INSERT INTO \"public\".\"documents\"(id, owner_id, title) VALUES (@i, @o, 'alice')";
            ins.Parameters.AddWithValue("@i", aliceDoc);
            ins.Parameters.AddWithValue("@o", alice);
            ins.ExecuteNonQuery();
            tx.Commit();
        }

        // Bob inserts his doc.
        using (var tx = _connection.BeginTransaction())
        {
            SetAppRoleAndUser(_connection, tx, bob);
            using var ins = _connection.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText =
                "INSERT INTO \"public\".\"documents\"(id, owner_id, title) VALUES (@i, @o, 'bob')";
            ins.Parameters.AddWithValue("@i", bobDoc);
            ins.Parameters.AddWithValue("@o", bob);
            ins.ExecuteNonQuery();
            tx.Commit();
        }

        // Bob selects -> sees only Bob's doc.
        using (var tx = _connection.BeginTransaction())
        {
            SetAppRoleAndUser(_connection, tx, bob);
            using var sel = _connection.CreateCommand();
            sel.Transaction = tx;
            sel.CommandText = "SELECT id FROM \"public\".\"documents\"";
            using var reader = sel.ExecuteReader();
            var rows = new List<Guid>();
            while (reader.Read())
            {
                rows.Add(reader.GetGuid(0));
            }
            Assert.Single(rows);
            Assert.Equal(bobDoc, rows[0]);
        }
    }

    [Fact]
    public void OwnerIsolationPolicy_BlocksCrossUserInsert()
    {
        ApplyAndForceRls(BuildOwnerIsolationSchema(), "documents");

        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        // Alice tries to insert a doc owned by Bob -> WITH CHECK fails.
        using var tx = _connection.BeginTransaction();
        SetAppRoleAndUser(_connection, tx, alice);
        using var ins = _connection.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText =
            "INSERT INTO \"public\".\"documents\"(id, owner_id, title) VALUES (@i, @o, 'evil')";
        ins.Parameters.AddWithValue("@i", Guid.NewGuid());
        ins.Parameters.AddWithValue("@o", bob);

        var ex = Assert.Throws<PostgresException>(() => ins.ExecuteNonQuery());
        Assert.Contains("row-level security", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GroupMembershipPolicy_LqlSubquery_AllowsGroupMemberAccess()
    {
        ApplyAndForceRls(BuildGroupMembershipSchema(), "user_group_memberships", "documents");

        var alice = Guid.NewGuid();
        var visibleGroup = Guid.NewGuid();
        var hiddenGroup = Guid.NewGuid();
        var visibleDoc = Guid.NewGuid();
        var hiddenDoc = Guid.NewGuid();

        InsertGroupMembership(alice, visibleGroup);
        InsertDocument(visibleDoc, visibleGroup, "visible");
        InsertDocument(hiddenDoc, hiddenGroup, "hidden");

        using var tx = _connection.BeginTransaction();
        SetAppRoleAndUser(_connection, tx, alice);
        using var sel = _connection.CreateCommand();
        sel.Transaction = tx;
        sel.CommandText = "SELECT id FROM \"public\".\"documents\" ORDER BY title";
        using var reader = sel.ExecuteReader();
        var rows = new List<Guid>();
        while (reader.Read())
        {
            rows.Add(reader.GetGuid(0));
        }

        Assert.Single(rows);
        Assert.Equal(visibleDoc, rows[0]);
    }

    [Fact]
    public void SchemaInspector_RoundTripsPolicy()
    {
        ApplyAndForceRls(BuildOwnerIsolationSchema(), "documents");

        var inspected = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;

        var table = inspected.Tables.Single(t => t.Name == "documents");
        Assert.NotNull(table.RowLevelSecurity);
        Assert.True(table.RowLevelSecurity!.Enabled);
        Assert.Single(table.RowLevelSecurity.Policies);
        Assert.Equal("owner_isolation", table.RowLevelSecurity.Policies[0].Name);
    }

    [Fact]
    public void SchemaDiff_AddsNewPolicy_ToExistingRlsTable()
    {
        ApplyAndForceRls(BuildOwnerIsolationSchema(), "documents");

        var current = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var desiredPlus = BuildOwnerIsolationSchema() with
        {
            Tables =
            [
                BuildOwnerIsolationSchema().Tables[0] with
                {
                    RowLevelSecurity = new RlsPolicySetDefinition
                    {
                        Policies =
                        [
                            new RlsPolicyDefinition
                            {
                                Name = "owner_isolation",
                                Operations = [RlsOperation.All],
                                UsingLql = "owner_id = current_user_id()::uuid",
                                WithCheckLql = "owner_id = current_user_id()::uuid",
                            },
                            new RlsPolicyDefinition
                            {
                                Name = "extra_policy",
                                Operations = [RlsOperation.Select],
                                UsingLql = "true",
                            },
                        ],
                    },
                },
            ],
        };

        var ops = (
            (OperationsResultOk)SchemaDiff.Calculate(current, desiredPlus, logger: _logger)
        ).Value;

        var creates = ops.OfType<CreateRlsPolicyOperation>().ToList();
        Assert.Single(creates);
        Assert.Equal("extra_policy", creates[0].Policy.Name);
    }

    [Fact]
    public void SchemaDiff_AllowDestructive_DropsOrphanPolicy()
    {
        ApplyAndForceRls(BuildOwnerIsolationSchema(), "documents");

        var current = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var desiredEmpty = BuildOwnerIsolationSchema() with
        {
            Tables =
            [
                BuildOwnerIsolationSchema().Tables[0] with
                {
                    RowLevelSecurity = new RlsPolicySetDefinition { Policies = [] },
                },
            ],
        };

        var ops = (
            (OperationsResultOk)
                SchemaDiff.Calculate(current, desiredEmpty, allowDestructive: true, logger: _logger)
        ).Value;

        Assert.Contains(
            ops,
            o => o is DropRlsPolicyOperation drop && drop.PolicyName == "owner_isolation"
        );
    }

    private void InsertGroupMembership(Guid userId, Guid groupId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            "INSERT INTO \"public\".\"user_group_memberships\"(id, user_id, group_id) VALUES (@id, @user, @group)";
        cmd.Parameters.AddWithValue("@id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("@user", userId.ToString());
        cmd.Parameters.AddWithValue("@group", groupId);
        cmd.ExecuteNonQuery();
    }

    private void InsertDocument(Guid id, Guid groupId, string title)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            "INSERT INTO \"public\".\"documents\"(id, group_id, title) VALUES (@id, @group, @title)";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@group", groupId);
        cmd.Parameters.AddWithValue("@title", title);
        cmd.ExecuteNonQuery();
    }
}
