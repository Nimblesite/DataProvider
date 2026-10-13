// Implements [MIG-TEST-SCHEMA-FIXTURES].
// Implements [CI-TEST-SHARED-COMMAND-PARAMETERS].

namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [RLS-PG] end-to-end tests from docs/specs/rls-spec.md.

/// <summary>
/// E2E tests for the PostgreSQL RLS pipeline against a real Testcontainers
/// postgres instance. Verifies that policies emitted by the migration tool
/// actually enforce row-level access at runtime.
/// </summary>
[Collection(PostgresTestSuite.Name)]
public sealed partial class PostgresRlsE2ETests(PostgresContainerFixture fixture)
    : PostgresDatabaseTest(fixture)
{
    protected override string DatabaseName => "rls_e2e";

    private const string AppRole = "rls_app_role";

    private void ApplyAndForceRls(SchemaDefinition desired, params string[] tableNames)
    {
        PostgresTestDb.ApplySchema(Connection, desired, Logger);

        // Testcontainers postgres connects as a superuser with BYPASSRLS.
        // To exercise policies we need a non-bypassrls role and grant CRUD.
        using (var roleCmd = Connection.CreateCommand())
        {
            roleCmd.CommandText = SqlData_ApplyAndForceRls_1();
            roleCmd.ExecuteNonQuery();
        }

        foreach (var tableName in tableNames)
        {
            using var grantCmd = Connection.CreateCommand();
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

        Assert.True(
            (bool)
                global::Tests.Shared.DatabaseCommands.Scalar(
                    connection: Connection,
                    configure: command => command.CommandText = Data_EnableRls_TableHasRlsEnabled_1
                )!
        );
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
        InsertDocumentAsAppRole(user: alice, docId: aliceDoc, title: "alice");

        // Bob inserts his doc.
        InsertDocumentAsAppRole(user: bob, docId: bobDoc, title: "bob");

        // Bob selects -> sees only Bob's doc.
        using (var tx = Connection.BeginTransaction())
        {
            SetAppRoleAndUser(Connection, tx, bob);
            using var sel = CommandWith(tx, "SELECT id FROM \"public\".\"documents\"");
            using var reader = sel.ExecuteReader();
            var rows = VerifyReusableWorkflow22(input0: reader);
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
        using var tx = Connection.BeginTransaction();
        SetAppRoleAndUser(Connection, tx, alice);
        using var ins = CommandWith(
            tx,
            "INSERT INTO \"public\".\"documents\"(id, owner_id, title) VALUES (@i, @o, 'evil')"
        );
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

        using var tx = Connection.BeginTransaction();
        SetAppRoleAndUser(Connection, tx, alice);
        using var sel = CommandWith(tx, "SELECT id FROM \"public\".\"documents\" ORDER BY title");
        using var reader = sel.ExecuteReader();
        var rows = VerifyReusableWorkflow22(input0: reader);

        Assert.Single(rows);
        Assert.Equal(visibleDoc, rows[0]);
    }

    [Fact]
    public void SchemaInspector_RoundTripsPolicy()
    {
        ApplyAndForceRls(BuildOwnerIsolationSchema(), "documents");

        var inspected = PostgresTestDb.Inspect(Connection, Logger);

        var table = inspected.Tables.Single(t => t.Name == "documents");
        var rls = RlsAssertions.EnabledWithSinglePolicy(table);
        Assert.Equal("owner_isolation", rls.Policies[0].Name);
    }

    [Fact]
    public void SchemaDiff_AddsNewPolicy_ToExistingRlsTable()
    {
        ApplyAndForceRls(BuildOwnerIsolationSchema(), "documents");

        var current = PostgresTestDb.Inspect(Connection, Logger);
        var desiredPlus = BuildOwnerIsolationSchema() with
        {
            Tables =
            [
                BuildOwnerIsolationSchema().Tables[0] with
                {
                    RowLevelSecurity = Fixture_SchemaDiff_AddsNewPolicy_ToExistingRlsTable_1(),
                },
            ],
        };

        var ops = SchemaDiffAssertions.Diff(current: current, desired: desiredPlus, logger: Logger);

        var creates = ops.OfType<CreateRlsPolicyOperation>().ToList();
        Assert.Single(creates);
        Assert.Equal("extra_policy", creates[0].Policy.Name);
    }

    [Fact]
    public void SchemaDiff_AllowDestructive_DropsOrphanPolicy()
    {
        ApplyAndForceRls(BuildOwnerIsolationSchema(), "documents");

        var current = PostgresTestDb.Inspect(Connection, Logger);
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

        var ops = SchemaDiffAssertions.Diff(
            current: current,
            desired: desiredEmpty,
            destructive: true,
            logger: Logger
        );

        SchemaDiffAssertions.AssertDropsRlsPolicy(ops, "owner_isolation");
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void InsertDocumentAsAppRole(Guid user, Guid docId, string title)
    {
        using var tx = Connection.BeginTransaction();
        VerifyReusableWorkflow16(
            configure0: ins =>
            {
                ins.Transaction = tx;
                ins.CommandText =
                    $"INSERT INTO \"public\".\"documents\"(id, owner_id, title) VALUES (@i, @o, '{title}')";
            },
            input0: SetAppRoleAndUser,
            input1: Connection,
            input2: tx,
            input3: user,
            input4: docId
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private NpgsqlCommand CommandWith(NpgsqlTransaction tx, string sql)
    {
        var command = Connection.CreateCommand();
        PostgresTestDb.VerifyReusableWorkflow8(input0: command, input1: tx, value0: sql);
        return command;
    }

    private void InsertGroupMembership(Guid userId, Guid groupId)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText =
            "INSERT INTO \"public\".\"user_group_memberships\"(id, user_id, group_id) VALUES (@id, @user, @group)";
        cmd.Parameters.AddWithValue("@id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("@user", userId.ToString());
        cmd.Parameters.AddWithValue("@group", groupId);
        cmd.ExecuteNonQuery();
    }

    private void InsertDocument(Guid id, Guid groupId, string title) =>
        PostgresTestDb.Insert(
            Connection,
            "INSERT INTO \"public\".\"documents\"(id, group_id, title) VALUES (@id, @group, @title)",
            ("@id", id),
            ("@group", groupId),
            ("@title", title)
        );

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow16(
        Action<NpgsqlConnection, NpgsqlTransaction, Guid> input0,
        NpgsqlConnection input1,
        NpgsqlTransaction input2,
        Guid input3,
        Guid input4,
        global::System.Action<global::System.Data.Common.DbCommand> configure0
    )
    {
        input0(arg1: input1, arg2: input2, arg3: input3);
        DatabaseCommands.Execute(
            connection: input1,
            configure: configure0,
            parameters: () => [("@i", input4), ("@o", input3)]
        );
        input2.Commit();
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static List<Guid> VerifyReusableWorkflow22(NpgsqlDataReader input0)
    {
        var rows = new List<Guid>();
        while (input0.Read())
        {
            rows.Add(input0.GetGuid(0));
        }

        return rows;
    }
}
