namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [MIG-TRIGGER-SQLITE] and [MIG-TRIGGER-DIFF] from
// docs/specs/declarative-triggers-spec.md (GitHub issue 82).

/// <summary>
/// E2E tests for declarative BEFORE-trigger guards on SQLite. The schema
/// declares a last-owner guard on TenantMembers: demoting or deleting the
/// only 'owner' row of a tenant must fail with the declared error message.
/// </summary>
public sealed class SqliteDeclarativeTriggerTests
{
    private static readonly ILogger Logger = NullLogger.Instance;
    private static readonly string OwnerA = Guid.NewGuid().ToString();
    private static readonly string OwnerB = Guid.NewGuid().ToString();
    private static readonly string MemberC = Guid.NewGuid().ToString();
    private static readonly string Tenant1 = Guid.NewGuid().ToString();
    private static readonly string Tenant2 = Guid.NewGuid().ToString();

    private const string GuardYaml = """
        name: tenant_app
        tables:
          - name: TenantMembers
            schema: main
            columns:
              - name: Id
                type: Text
                isNullable: false
              - name: TenantId
                type: Text
                isNullable: false
              - name: Role
                type: Text
                isNullable: false
            primaryKey:
              columns:
                - Id
            triggers:
              - name: assert_not_last_owner
                timing: Before
                events: [Update, Delete]
                forEachRow: true
                raiseWhen: |
                  old.Role = 'owner' and not exists(
                    TenantMembers
                    |> filter(fn(m) => m.TenantId = old.TenantId and m.Role = 'owner' and m.Id <> old.Id)
                  )
                errorMessage: cannot remove the last owner of a tenant
        """;

    private const string PlainYaml = """
        name: tenant_app
        tables:
          - name: TenantMembers
            schema: main
            columns:
              - name: Id
                type: Text
                isNullable: false
              - name: TenantId
                type: Text
                isNullable: false
              - name: Role
                type: Text
                isNullable: false
            primaryKey:
              columns:
                - Id
        """;

    [Fact]
    public void Sqlite_TriggerGuard_BlocksDeleteOfLastOwner()
    {
        SqliteTestDb.WithDb(connection =>
        {
            ApplyGuardSchema(connection);
            InsertMember(connection, OwnerA, Tenant1, "owner");
            InsertMember(connection, MemberC, Tenant1, "member");

            var ex = Assert.Throws<SqliteException>(() => DeleteMember(connection, OwnerA));

            Assert.Contains("last owner", ex.Message, StringComparison.Ordinal);
            Assert.Equal(2, SqliteTestDb.CountRows(connection, "TenantMembers"));
        });
    }

    [Fact]
    public void Sqlite_TriggerGuard_AllowsDeleteWhenAnotherOwnerExists()
    {
        SqliteTestDb.WithDb(connection =>
        {
            ApplyGuardSchema(connection);
            InsertMember(connection, OwnerA, Tenant1, "owner");
            InsertMember(connection, OwnerB, Tenant1, "owner");

            DeleteMember(connection, OwnerA);

            Assert.Equal(1, SqliteTestDb.CountRows(connection, "TenantMembers"));
        });
    }

    [Fact]
    public void Sqlite_TriggerGuard_BlocksDemoteOfLastOwner()
    {
        SqliteTestDb.WithDb(connection =>
        {
            ApplyGuardSchema(connection);
            InsertMember(connection, OwnerA, Tenant1, "owner");
            InsertMember(connection, MemberC, Tenant1, "member");

            var ex = Assert.Throws<SqliteException>(() => DemoteMember(connection, OwnerA));

            Assert.Contains("last owner", ex.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Sqlite_TriggerGuard_AllowsDemoteWhenAnotherOwnerExists()
    {
        SqliteTestDb.WithDb(connection =>
        {
            ApplyGuardSchema(connection);
            InsertMember(connection, OwnerA, Tenant1, "owner");
            InsertMember(connection, OwnerB, Tenant1, "owner");

            DemoteMember(connection, OwnerA);

            Assert.Equal(
                1,
                SqliteTestDb.Count(
                    connection,
                    "SELECT COUNT(*) FROM [TenantMembers] WHERE [Role]='owner'"
                )
            );
        });
    }

    [Fact]
    public void Sqlite_TriggerGuard_IsScopedToTenant()
    {
        SqliteTestDb.WithDb(connection =>
        {
            ApplyGuardSchema(connection);
            InsertMember(connection, OwnerA, Tenant1, "owner");
            InsertMember(connection, OwnerB, Tenant2, "owner");

            // OwnerB owns a different tenant, so OwnerA is still the last
            // owner of Tenant1 and must not be deletable.
            var ex = Assert.Throws<SqliteException>(() => DeleteMember(connection, OwnerA));

            Assert.Contains("last owner", ex.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Sqlite_Trigger_RediffAfterApply_ProducesNoOperations()
    {
        SqliteTestDb.WithDb(connection =>
        {
            ApplyGuardSchema(connection);

            var current = SqliteTestDb.Inspect(connection);
            var ops = (
                (OperationsResultOk)
                    SchemaDiff.Calculate(
                        current,
                        SchemaYamlSerializer.FromYaml(GuardYaml),
                        logger: Logger
                    )
            ).Value;

            Assert.Empty(ops);
        });
    }

    [Fact]
    public void Sqlite_TriggerRemoval_WithoutDestructive_KeepsGuard()
    {
        SqliteTestDb.WithDb(connection =>
        {
            ApplyGuardSchema(connection);
            SqliteTestDb.ApplySchema(connection, SchemaYamlSerializer.FromYaml(PlainYaml));
            InsertMember(connection, OwnerA, Tenant1, "owner");

            var ex = Assert.Throws<SqliteException>(() => DeleteMember(connection, OwnerA));

            Assert.Contains("last owner", ex.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Sqlite_TriggerRemoval_WithDestructive_DropsGuard()
    {
        SqliteTestDb.WithDb(connection =>
        {
            ApplyGuardSchema(connection);
            SqliteTestDb.ApplySchema(
                connection,
                SchemaYamlSerializer.FromYaml(PlainYaml),
                allowDestructive: true
            );
            InsertMember(connection, OwnerA, Tenant1, "owner");

            DeleteMember(connection, OwnerA);

            Assert.Equal(0, SqliteTestDb.CountRows(connection, "TenantMembers"));
        });
    }

    [Fact]
    public void Sqlite_TriggerGuard_OldReferenceOnInsertEvent_FailsLoudly()
    {
        // Implements [MIG-TRIGGER-GUARD-LQL]: old. does not exist on INSERT.
        // Without validation SQLite fails at DML time while Postgres silently
        // never fires the guard.
        SqliteTestDb.WithDb(connection =>
        {
            var result = TryApplyGuardVariant(
                connection,
                events: "[Insert]",
                raiseWhen: "old.Role = 'owner'"
            );

            var error = Assert.IsType<MigrationApplyResultError>(result);
            Assert.Contains("fires on Insert", error.Value.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Sqlite_TriggerGuard_ForEachRowFalse_FailsLoudly()
    {
        // Implements [MIG-TRIGGER-GUARD-LQL]: guards are row-level on every
        // platform; statement-level triggers see no OLD/NEW row on Postgres.
        SqliteTestDb.WithDb(connection =>
        {
            var result = TryApplyGuardVariant(
                connection,
                events: "[Delete]",
                raiseWhen: "old.Role = 'owner'",
                extraTriggerYaml: "forEachRow: false"
            );

            var error = Assert.IsType<MigrationApplyResultError>(result);
            Assert.Contains("statement-level", error.Value.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Sqlite_TriggerGuard_ReservedTokenInPredicate_FailsLoudly()
    {
        // Implements [MIG-TRIGGER-GUARD-LQL]: __TRG_ is reserved for internal
        // sentinels; user literals containing it would be silently rewritten.
        SqliteTestDb.WithDb(connection =>
        {
            var result = TryApplyGuardVariant(
                connection,
                events: "[Delete]",
                raiseWhen: "old.Role = '__TRG_OLD_Role__'"
            );

            var error = Assert.IsType<MigrationApplyResultError>(result);
            Assert.Contains("reserved token", error.Value.Message, StringComparison.Ordinal);
        });
    }

    private static MigrationApplyResult TryApplyGuardVariant(
        SqliteConnection connection,
        string events,
        string raiseWhen,
        string? extraTriggerYaml = null
    )
    {
        var extra = extraTriggerYaml is null ? string.Empty : $"\n        {extraTriggerYaml}";
        var yaml = $"""
            name: tenant_app
            tables:
              - name: TenantMembers
                schema: main
                columns:
                  - name: Id
                    type: Text
                    isNullable: false
                  - name: TenantId
                    type: Text
                    isNullable: false
                  - name: Role
                    type: Text
                    isNullable: false
                primaryKey:
                  columns:
                    - Id
                triggers:
                  - name: guard_variant
                    events: {events}
                    raiseWhen: "{raiseWhen}"{extra}
            """;
        return SqliteTestDb.TryApplySchema(connection, SchemaYamlSerializer.FromYaml(yaml));
    }

    private static void ApplyGuardSchema(SqliteConnection connection) =>
        SqliteTestDb.ApplySchema(connection, SchemaYamlSerializer.FromYaml(GuardYaml));

    private static void InsertMember(
        SqliteConnection connection,
        string id,
        string tenantId,
        string role
    ) =>
        SqliteTestDb.Execute(
            connection,
            $"INSERT INTO [TenantMembers]([Id], [TenantId], [Role]) VALUES ('{id}', '{tenantId}', '{role}')"
        );

    private static void DeleteMember(SqliteConnection connection, string id) =>
        SqliteTestDb.Execute(connection, $"DELETE FROM [TenantMembers] WHERE [Id]='{id}'");

    private static void DemoteMember(SqliteConnection connection, string id) =>
        SqliteTestDb.Execute(
            connection,
            $"UPDATE [TenantMembers] SET [Role]='member' WHERE [Id]='{id}'"
        );
}
