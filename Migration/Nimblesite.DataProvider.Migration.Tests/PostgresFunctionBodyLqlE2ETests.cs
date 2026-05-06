using System.Globalization;

namespace Nimblesite.DataProvider.Migration.Tests;

[Collection(PostgresTestSuite.Name)]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Usage",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposed via IAsyncLifetime.DisposeAsync"
)]
public sealed class PostgresFunctionBodyLqlE2ETests(PostgresContainerFixture fixture)
    : IAsyncLifetime
{
    private NpgsqlConnection _connection = null!;
    private readonly ILogger _logger = NullLogger.Instance;

    public async Task InitializeAsync()
    {
        _connection = await fixture.CreateDatabaseAsync("function_body_lql").ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public void BodyLqlSupportFunctionsAndLqlPolicies_EnforceNapTenantIsolation()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var names = Names.Create(suffix);

        Apply(Schema(names));

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userA = Guid.NewGuid();
        Seed(tenantA, tenantB, userA);

        using var tx = _connection.BeginTransaction();
        SetAppSession(tx, names.AppUserRole, tenantA, userA);

        Assert.Equal(1, CountVisibleDocuments(tx));
        Assert.Throws<PostgresException>(() => InsertDocument(tx, tenantB, "blocked"));
    }

    private void Apply(SchemaDefinition schema)
    {
        var current = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
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
        var failure = apply is MigrationApplyResultError error ? error.Value.Message : "unknown";

        Assert.True(apply is MigrationApplyResultOk, $"Migration failed: {failure}");
    }

    private void Seed(Guid tenantA, Guid tenantB, Guid userA)
    {
        Exec(
            $"INSERT INTO public.tenant_members(id, tenant_id, user_id) VALUES ('{Guid.NewGuid()}', '{tenantA}', '{userA}')"
        );
        Exec(
            $"INSERT INTO public.documents(id, tenant_id, title) VALUES ('{Guid.NewGuid()}', '{tenantA}', 'visible')"
        );
        Exec(
            $"INSERT INTO public.documents(id, tenant_id, title) VALUES ('{Guid.NewGuid()}', '{tenantB}', 'hidden')"
        );
    }

    private void SetAppSession(NpgsqlTransaction tx, string role, Guid tenant, Guid user)
    {
        Exec(tx, $"SET LOCAL ROLE {role}");
        Exec(tx, $"SET LOCAL app.tenant_id = '{tenant}'");
        Exec(tx, $"SET LOCAL app.user_id = '{user}'");
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

    private static SchemaDefinition Schema(Names names) =>
        new()
        {
            Name = "body_lql",
            Roles = [new PostgresRoleDefinition { Name = names.AppUserRole, GrantTo = ["test"] }],
            Tables = [TenantMembersTable(), DocumentsTable(names)],
            Functions =
            [
                new PostgresFunctionDefinition
                {
                    Name = names.AppTenantFunction,
                    Returns = "uuid",
                    BodyLql = "current_setting('app.tenant_id')::uuid",
                    ExecuteRoles = [names.AppUserRole],
                },
                new PostgresFunctionDefinition
                {
                    Name = names.AppUserFunction,
                    Returns = "uuid",
                    BodyLql = "current_setting('app.user_id')::uuid",
                    ExecuteRoles = [names.AppUserRole],
                },
                new PostgresFunctionDefinition
                {
                    Name = names.IsMemberFunction,
                    Returns = "boolean",
                    Arguments =
                    [
                        new PostgresFunctionArgumentDefinition { Name = "u", Type = "uuid" },
                        new PostgresFunctionArgumentDefinition { Name = "t", Type = "uuid" },
                    ],
                    SecurityDefiner = true,
                    BodyLql =
                        "exists(tenant_members |> filter(fn(m) => m.user_id = u and m.tenant_id = t))",
                    ExecuteRoles = [names.AppUserRole],
                },
            ],
            Grants =
            [
                new PostgresGrantDefinition
                {
                    Schema = "public",
                    Target = PostgresGrantTarget.Schema,
                    Privileges = ["USAGE"],
                    Roles = [names.AppUserRole],
                },
                new PostgresGrantDefinition
                {
                    Schema = "public",
                    Target = PostgresGrantTarget.AllTablesInSchema,
                    Privileges = ["SELECT", "INSERT", "UPDATE", "DELETE"],
                    Roles = [names.AppUserRole],
                },
            ],
        };

    private static TableDefinition TenantMembersTable() =>
        new()
        {
            Schema = "public",
            Name = "tenant_members",
            Columns = [RequiredUuid("id"), RequiredUuid("tenant_id"), RequiredUuid("user_id")],
            PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
        };

    private static TableDefinition DocumentsTable(Names names) =>
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
                        UsingLql =
                            $"tenant_id = {names.AppTenantFunction}() and {names.IsMemberFunction}({names.AppUserFunction}(), {names.AppTenantFunction}())",
                        WithCheckLql =
                            $"tenant_id = {names.AppTenantFunction}() and {names.IsMemberFunction}({names.AppUserFunction}(), {names.AppTenantFunction}())",
                    },
                ],
            },
        };

    private static ColumnDefinition RequiredUuid(string name) =>
        new()
        {
            Name = name,
            Type = PortableTypes.Uuid,
            IsNullable = false,
        };

    private sealed record Names(
        string AppUserRole,
        string AppTenantFunction,
        string AppUserFunction,
        string IsMemberFunction
    )
    {
        public static Names Create(string suffix) =>
            new(
                $"body_lql_user_{suffix}",
                $"app_tenant_id_{suffix}",
                $"app_user_id_{suffix}",
                $"is_member_{suffix}"
            );
    }
}
