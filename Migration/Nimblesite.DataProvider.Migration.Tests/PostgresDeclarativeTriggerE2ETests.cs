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
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Usage",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposed via IAsyncLifetime.DisposeAsync"
)]
public sealed class PostgresDeclarativeTriggerE2ETests(PostgresContainerFixture fixture)
    : IAsyncLifetime
{
    private NpgsqlConnection _connection = null!;
    private readonly ILogger _logger = NullLogger.Instance;

    public async Task InitializeAsync()
    {
        _connection = await fixture.CreateDatabaseAsync("trigger_e2e").ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync().ConfigureAwait(false);
    }

    private const string GuardYaml = """
        name: tenant_app
        tables:
          - name: tenant_members
            columns:
              - name: id
                type: Uuid
                isNullable: false
              - name: tenant_id
                type: Uuid
                isNullable: false
              - name: role
                type: VarChar(50)
                isNullable: false
            primaryKey:
              columns:
                - id
            triggers:
              - name: assert_not_last_owner
                events: [Update, Delete]
                raiseWhen: |
                  old.role = 'owner' and not exists(
                    tenant_members
                    |> filter(fn(m) => m.tenant_id = old.tenant_id and m.role = 'owner' and m.id <> old.id)
                  )
                errorMessage: cannot remove the last owner of a tenant
        """;

    [Fact]
    public void PgTriggerGuard_BlocksDeleteOfLastOwner()
    {
        ApplyGuardSchema();
        var owner = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        InsertMember(owner, tenant, "owner");
        InsertMember(Guid.NewGuid(), tenant, "member");

        var ex = Assert.Throws<PostgresException>(() => DeleteMember(owner));

        Assert.Contains("last owner", ex.Message, StringComparison.Ordinal);
        Assert.Equal(2, CountMembers());
    }

    [Fact]
    public void PgTriggerGuard_AllowsDeleteWhenAnotherOwnerExists()
    {
        ApplyGuardSchema();
        var ownerA = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        InsertMember(ownerA, tenant, "owner");
        InsertMember(Guid.NewGuid(), tenant, "owner");

        DeleteMember(ownerA);

        Assert.Equal(1, CountMembers());
    }

    [Fact]
    public void PgTriggerGuard_BlocksDemoteOfLastOwner()
    {
        ApplyGuardSchema();
        var owner = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        InsertMember(owner, tenant, "owner");
        InsertMember(Guid.NewGuid(), tenant, "member");

        var ex = Assert.Throws<PostgresException>(() => DemoteMember(owner));

        Assert.Contains("last owner", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PgTrigger_RediffAfterApply_ProducesNoOperations()
    {
        ApplyGuardSchema();

        var current = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var ops = (
            (OperationsResultOk)
                SchemaDiff.Calculate(
                    current,
                    SchemaYamlSerializer.FromYaml(GuardYaml),
                    logger: _logger
                )
        ).Value;

        Assert.Empty(ops);
    }

    private const string PascalCaseGuardYaml = """
        name: tenant_app
        tables:
          - name: TenantMembers
            columns:
              - name: Id
                type: Uuid
                isNullable: false
              - name: TenantId
                type: Uuid
                isNullable: false
              - name: Role
                type: VarChar(50)
                isNullable: false
            primaryKey:
              columns:
                - Id
            triggers:
              - name: assert_not_last_owner
                events: [Update, Delete]
                raiseWhen: |
                  old.Role = 'owner' and not exists(
                    TenantMembers
                    |> filter(fn(m) => m.TenantId = old.TenantId and m.Role = 'owner' and m.Id <> old.Id)
                  )
                errorMessage: cannot remove the last owner of a tenant
        """;

    [Fact]
    public void PgTriggerGuard_PascalCaseIdentifiers_EnforcedAtRuntime()
    {
        // Implements [MIG-TRIGGER-GUARD-LQL]: exists() pipeline columns must
        // be quoted on Postgres or mixed-case columns fold to lowercase and
        // every UPDATE/DELETE fails with 42703 at trigger-fire time.
        PostgresTestDb.ApplySchema(
            _connection,
            SchemaYamlSerializer.FromYaml(PascalCaseGuardYaml),
            _logger
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
            _connection,
            SchemaYamlSerializer.FromYaml(GuardYaml),
            _logger,
            allowDestructive: true
        );

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM pg_trigger WHERE tgname = 'zz_unmanaged_touch'";
        Assert.Equal(1L, Assert.IsType<long>(cmd.ExecuteScalar()));

        // Guard still enforced after the destructive rerun.
        var owner = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        InsertMember(owner, tenant, "owner");
        var ex = Assert.Throws<PostgresException>(() => DeleteMember(owner));
        Assert.Contains("last owner", ex.Message, StringComparison.Ordinal);
    }

    private void Execute(string sql, Guid? id = null)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        if (id is { } value)
        {
            cmd.Parameters.AddWithValue("@i", value);
        }
        cmd.ExecuteNonQuery();
    }

    private void InsertPascalMember(Guid id, Guid tenantId, string role)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            "INSERT INTO \"public\".\"TenantMembers\"(\"Id\", \"TenantId\", \"Role\") VALUES (@i, @t, @r)";
        cmd.Parameters.AddWithValue("@i", id);
        cmd.Parameters.AddWithValue("@t", tenantId);
        cmd.Parameters.AddWithValue("@r", role);
        cmd.ExecuteNonQuery();
    }

    private void ApplyGuardSchema() =>
        PostgresTestDb.ApplySchema(_connection, SchemaYamlSerializer.FromYaml(GuardYaml), _logger);

    private void InsertMember(Guid id, Guid tenantId, string role)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            "INSERT INTO \"public\".\"tenant_members\"(id, tenant_id, role) VALUES (@i, @t, @r)";
        cmd.Parameters.AddWithValue("@i", id);
        cmd.Parameters.AddWithValue("@t", tenantId);
        cmd.Parameters.AddWithValue("@r", role);
        cmd.ExecuteNonQuery();
    }

    private void DeleteMember(Guid id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM \"public\".\"tenant_members\" WHERE id = @i";
        cmd.Parameters.AddWithValue("@i", id);
        cmd.ExecuteNonQuery();
    }

    private void DemoteMember(Guid id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE \"public\".\"tenant_members\" SET role = 'member' WHERE id = @i";
        cmd.Parameters.AddWithValue("@i", id);
        cmd.ExecuteNonQuery();
    }

    private long CountMembers()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM \"public\".\"tenant_members\"";
        return Assert.IsType<long>(cmd.ExecuteScalar());
    }
}
