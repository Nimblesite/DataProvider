// Implements [MIG-TEST-SCHEMA-FIXTURES].
// Implements [CI-TEST-SHARED-COMMAND].
using System.Globalization;
using Tests.Shared;

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
        var apply = PostgresTestDb.TryApply(
            connection: _connection,
            operations: ops,
            logger: _logger
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
        DatabaseCommands.Execute(
            connection: _connection,
            configure: command => command.CommandText = sql
        );
    }

    private void Exec(NpgsqlTransaction tx, string sql)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static SchemaDefinition Schema(Names names) =>
        SchemaTestData.Schema(
            name: "body_lql",
            roles: [SchemaTestData.PostgresRole(name: names.AppUserRole, grantTo: ["test"])],
            tables: [TenantMembersTable(), DocumentsTable(names)],
            functions:
            [
                SchemaTestData.PostgresFunction(
                    name: names.AppTenantFunction,
                    returns: "uuid",
                    bodyLql: "current_setting('app.tenant_id')::uuid",
                    executeRoles: [names.AppUserRole]
                ),
                SchemaTestData.PostgresFunction(
                    name: names.AppUserFunction,
                    returns: "uuid",
                    bodyLql: "current_setting('app.user_id')::uuid",
                    executeRoles: [names.AppUserRole]
                ),
                SchemaTestData.PostgresFunction(
                    name: names.IsMemberFunction,
                    returns: "boolean",
                    arguments:
                    [
                        SchemaTestData.PostgresFunctionArgument(name: "u", type: "uuid"),
                        SchemaTestData.PostgresFunctionArgument(name: "t", type: "uuid"),
                    ],
                    securityDefiner: true,
                    bodyLql: "exists(tenant_members |> filter(fn(m) => m.user_id = u and m.tenant_id = t))",
                    executeRoles: [names.AppUserRole]
                ),
            ],
            grants:
            [
                SchemaTestData.PostgresGrant(
                    schema: "public",
                    target: PostgresGrantTarget.Schema,
                    privileges: ["USAGE"],
                    roles: [names.AppUserRole]
                ),
                SchemaTestData.PostgresGrant(
                    schema: "public",
                    target: PostgresGrantTarget.AllTablesInSchema,
                    privileges: ["SELECT", "INSERT", "UPDATE", "DELETE"],
                    roles: [names.AppUserRole]
                ),
            ]
        );

    private static TableDefinition TenantMembersTable() =>
        SchemaTestData.Table(
            schema: "public",
            name: "tenant_members",
            columns: [RequiredUuid("id"), RequiredUuid("tenant_id"), RequiredUuid("user_id")],
            primaryKey: new PrimaryKeyDefinition { Columns = ["id"] }
        );

    private static TableDefinition DocumentsTable(Names names) =>
        SchemaTestData.Table(
            schema: "public",
            name: "documents",
            columns:
            [
                RequiredUuid("id"),
                RequiredUuid("tenant_id"),
                SchemaTestData.Column(name: "title", type: PortableTypes.Text, isNullable: false),
            ],
            primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
            rowLevelSecurity: SchemaTestData.RlsPolicySet(
                forced: true,
                policies:
                [
                    SchemaTestData.RlsPolicy(
                        name: "documents_member",
                        roles: [names.AppUserRole],
                        usingLql: $"tenant_id = {names.AppTenantFunction}() and {names.IsMemberFunction}({names.AppUserFunction}(), {names.AppTenantFunction}())",
                        withCheckLql: $"tenant_id = {names.AppTenantFunction}() and {names.IsMemberFunction}({names.AppUserFunction}(), {names.AppTenantFunction}())"
                    ),
                ]
            )
        );

    private static ColumnDefinition RequiredUuid(string name) =>
        SchemaTestData.Column(name: name, type: PortableTypes.Uuid, isNullable: false);

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
