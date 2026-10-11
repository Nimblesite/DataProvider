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
public sealed partial class PostgresSupportE2ETests(PostgresContainerFixture fixture)
    : IAsyncLifetime
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
        PostgresLqlOnlyE2ETests.ApplyOperations(
            connection: _connection,
            operations: ops,
            logger: _logger
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

    private static SchemaDefinition SupportSchema(SupportNames names) =>
        SchemaTestData.Schema(
            name: "support_objects",
            roles:
            [
                SchemaTestData.PostgresRole(name: names.AppUserRole, grantTo: ["test"]),
                SchemaTestData.PostgresRole(name: names.AppAdminRole, grantTo: ["test"]),
            ],
            tables: [TenantMembersTable(), DocumentsTable(names)],
            functions: SupportFunctions(names),
            grants:
            [
                SchemaTestData.PostgresGrant(
                    schema: "public",
                    target: PostgresGrantTarget.Schema,
                    privileges: ["USAGE"],
                    roles: [names.AppUserRole, names.AppAdminRole]
                ),
                SchemaTestData.PostgresGrant(
                    schema: "public",
                    target: PostgresGrantTarget.AllTablesInSchema,
                    privileges: ["SELECT", "INSERT", "UPDATE", "DELETE"],
                    roles: [names.AppUserRole, names.AppAdminRole]
                ),
            ]
        );

    private static TableDefinition TenantMembersTable() =>
        SchemaTestData.Table(
            schema: "public",
            name: "tenant_members",
            columns:
            [
                RequiredUuid("id"),
                RequiredUuid("tenant_id"),
                RequiredUuid("user_id"),
                SchemaTestData.Column(name: "role", type: PortableTypes.Text, isNullable: false),
            ],
            primaryKey: new PrimaryKeyDefinition { Columns = ["id"] }
        );

    private static TableDefinition DocumentsTable(SupportNames names) =>
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
                        usingSql: $"tenant_id = {names.TenantFunction}() AND {names.MemberFunction}({names.TenantFunction}(), {names.UserFunction}())",
                        withCheckSql: $"tenant_id = {names.TenantFunction}() AND {names.MemberFunction}({names.TenantFunction}(), {names.UserFunction}())"
                    ),
                    SchemaTestData.RlsPolicy(
                        name: "documents_admin",
                        roles: [names.AppAdminRole],
                        usingSql: "true",
                        withCheckSql: "true"
                    ),
                ]
            )
        );

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
        SchemaTestData.PostgresFunction(
            name: name,
            returns: returns,
            body: body,
            securityDefiner: securityDefiner,
            executeRoles: [names.AppUserRole, names.AppAdminRole]
        );

    private static PostgresFunctionDefinition MembershipFunction(
        string name,
        string roleClause,
        SupportNames names
    ) =>
        Function(
            name,
            "boolean",
            SqlData_MembershipFunction_1(roleClause: roleClause),
            true,
            names
        ) with
        {
            Arguments =
            [
                SchemaTestData.PostgresFunctionArgument(name: "p_tenant_id", type: "uuid"),
                SchemaTestData.PostgresFunctionArgument(name: "p_user_id", type: "uuid"),
            ],
        };

    private static ColumnDefinition RequiredUuid(string name) =>
        SchemaTestData.Column(name: name, type: PortableTypes.Uuid, isNullable: false);

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
