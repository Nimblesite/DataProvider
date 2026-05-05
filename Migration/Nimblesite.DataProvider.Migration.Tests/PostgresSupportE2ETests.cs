using System.Globalization;

namespace Nimblesite.DataProvider.Migration.Tests;

[Collection(PostgresTestSuite.Name)]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Usage",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposed via IAsyncLifetime.DisposeAsync"
)]
public sealed class PostgresSupportE2ETests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private NpgsqlConnection _connection = null!;
    private readonly ILogger _logger = NullLogger.Instance;

    public async Task InitializeAsync()
    {
        _connection = await fixture.CreateDatabaseAsync("support_objects").ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public void DeclarativeRolesFunctionsAndGrants_UnblockNapStyleRls()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var names = SupportNames.Create(suffix);
        var schema = SupportSchema(names);

        Apply(schema);

        var reappliedOps = (
            (OperationsResultOk)
                SchemaDiff.Calculate(
                    ((SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public")).Value,
                    schema,
                    logger: _logger
                )
        ).Value;
        Assert.Empty(reappliedOps);

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userA = Guid.NewGuid();
        SeedTenantData(tenantA, tenantB, userA);

        using var tx = _connection.BeginTransaction();
        SetAppRole(tx, names.AppUserRole, tenantA, userA);
        Assert.Equal(1, CountVisibleDocuments(tx));
        Assert.Throws<PostgresException>(() => InsertDocument(tx, tenantB, "blocked"));
    }

    private void Apply(SchemaDefinition schema)
    {
        var current = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public")
        ).Value;
        var ops = (
            (OperationsResultOk)SchemaDiff.Calculate(current, schema, logger: _logger)
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
    }

    private void SeedTenantData(Guid tenantA, Guid tenantB, Guid userA)
    {
        Exec(
            $"INSERT INTO public.tenant_members(id, tenant_id, user_id, role) VALUES ('{Guid.NewGuid()}', '{tenantA}', '{userA}', 'writer')"
        );
        Exec(
            $"INSERT INTO public.documents(id, tenant_id, title) VALUES ('{Guid.NewGuid()}', '{tenantA}', 'visible')"
        );
        Exec(
            $"INSERT INTO public.documents(id, tenant_id, title) VALUES ('{Guid.NewGuid()}', '{tenantB}', 'hidden')"
        );
    }

    private void SetAppRole(NpgsqlTransaction tx, string role, Guid tenant, Guid user)
    {
        Exec(tx, $"SET LOCAL ROLE {role}");
        Exec(tx, $"SET LOCAL rls.tenant_id = '{tenant}'");
        Exec(tx, $"SET LOCAL rls.user_id = '{user}'");
    }

    private int CountVisibleDocuments(NpgsqlTransaction tx)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "SELECT count(*) FROM public.documents";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private void InsertDocument(NpgsqlTransaction tx, Guid tenant, string title) =>
        Exec(
            tx,
            $"INSERT INTO public.documents(id, tenant_id, title) VALUES ('{Guid.NewGuid()}', '{tenant}', '{title}')"
        );

    private void Exec(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private void Exec(NpgsqlTransaction tx, string sql)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static SchemaDefinition SupportSchema(SupportNames names) =>
        new()
        {
            Name = "support_objects",
            Roles =
            [
                new PostgresRoleDefinition { Name = names.AppUserRole, GrantTo = ["test"] },
                new PostgresRoleDefinition { Name = names.AppAdminRole, GrantTo = ["test"] },
            ],
            Tables = [TenantMembersTable(), DocumentsTable(names)],
            Functions = SupportFunctions(names),
            Grants =
            [
                new PostgresGrantDefinition
                {
                    Schema = "public",
                    Target = PostgresGrantTarget.Schema,
                    Privileges = ["USAGE"],
                    Roles = [names.AppUserRole, names.AppAdminRole],
                },
                new PostgresGrantDefinition
                {
                    Schema = "public",
                    Target = PostgresGrantTarget.AllTablesInSchema,
                    Privileges = ["SELECT", "INSERT", "UPDATE", "DELETE"],
                    Roles = [names.AppUserRole, names.AppAdminRole],
                },
            ],
        };

    private static TableDefinition TenantMembersTable() =>
        new()
        {
            Schema = "public",
            Name = "tenant_members",
            Columns =
            [
                RequiredUuid("id"),
                RequiredUuid("tenant_id"),
                RequiredUuid("user_id"),
                new ColumnDefinition
                {
                    Name = "role",
                    Type = PortableTypes.Text,
                    IsNullable = false,
                },
            ],
            PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
        };

    private static TableDefinition DocumentsTable(SupportNames names) =>
        new()
        {
            Schema = "public",
            Name = "documents",
            Columns =
            [
                RequiredUuid("id"),
                RequiredUuid("tenant_id"),
                new ColumnDefinition
                {
                    Name = "title",
                    Type = PortableTypes.Text,
                    IsNullable = false,
                },
            ],
            PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
            RowLevelSecurity = new RlsPolicySetDefinition
            {
                Forced = true,
                Policies =
                [
                    new RlsPolicyDefinition
                    {
                        Name = "documents_member",
                        Roles = [names.AppUserRole],
                        UsingSql =
                            $"tenant_id = {names.TenantFunction}() AND {names.MemberFunction}({names.TenantFunction}(), {names.UserFunction}())",
                        WithCheckSql =
                            $"tenant_id = {names.TenantFunction}() AND {names.MemberFunction}({names.TenantFunction}(), {names.UserFunction}())",
                    },
                    new RlsPolicyDefinition
                    {
                        Name = "documents_admin",
                        Roles = [names.AppAdminRole],
                        UsingSql = "true",
                        WithCheckSql = "true",
                    },
                ],
            },
        };

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

    private static PostgresFunctionDefinition Function(
        string name,
        string returns,
        string body,
        bool securityDefiner,
        SupportNames names
    ) =>
        new()
        {
            Name = name,
            Returns = returns,
            Body = body,
            SecurityDefiner = securityDefiner,
            ExecuteRoles = [names.AppUserRole, names.AppAdminRole],
        };

    private static PostgresFunctionDefinition MembershipFunction(
        string name,
        string roleClause,
        SupportNames names
    ) =>
        Function(
            name,
            "boolean",
            $"""
            SELECT EXISTS (
                SELECT 1
                FROM public.tenant_members tm
                WHERE tm.tenant_id = p_tenant_id
                  AND tm.user_id = p_user_id
                  {roleClause}
            )
            """,
            true,
            names
        ) with
        {
            Arguments =
            [
                new PostgresFunctionArgumentDefinition { Name = "p_tenant_id", Type = "uuid" },
                new PostgresFunctionArgumentDefinition { Name = "p_user_id", Type = "uuid" },
            ],
        };

    private static ColumnDefinition RequiredUuid(string name) =>
        new()
        {
            Name = name,
            Type = PortableTypes.Uuid,
            IsNullable = false,
        };

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
}
