namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [RLS-PG-SUPPORT-DDL].

/// <summary>
/// E2E coverage for PostgreSQL support-object inspection after real migrations.
/// </summary>
[Collection(PostgresTestSuite.Name)]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Usage",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposed via IAsyncLifetime.DisposeAsync"
)]
public sealed class PostgresSupportReadbackE2ETests(PostgresContainerFixture fixture)
    : IAsyncLifetime
{
    private NpgsqlConnection? _connection;
    private readonly ILogger _logger = NullLogger.Instance;

    public async Task InitializeAsync()
    {
        _connection = await fixture
            .CreateDatabaseAsync("pg_support_readback")
            .ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        if (_connection is NpgsqlConnection connection)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Fact]
    public void SupportObjects_InspectAfterApply_DoesNotEmitSecondDiff()
    {
        if (_connection is not NpgsqlConnection connection)
        {
            Assert.Fail("PostgreSQL connection not initialized.");
            return;
        }

        var desired = SupportSchema();
        var current = Inspect(connection);
        var operations = Diff(current, desired);

        var apply = MigrationRunner.Apply(
            connection,
            operations,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        Assert.True(
            apply is MigrationApplyResultOk,
            $"Migration failed: {(apply as MigrationApplyResultError)?.Value}"
        );

        var inspected = Inspect(connection);
        var secondDiff = Diff(inspected, desired);

        Assert.Contains(
            inspected.Roles,
            role =>
                role.Name == "nap_app_user"
                && !role.Login
                && !role.BypassRls
                && role.GrantTo.Count == 0
        );
        Assert.Contains(
            inspected.Functions,
            function =>
                function.Schema == "public"
                && function.Name == "current_tenant_id"
                && function.Returns == "uuid"
                && function.Language == "sql"
                && function.Volatility == "stable"
                && function.SecurityDefiner
                && function.RevokePublicExecute
                && function.ExecuteRoles.SequenceEqual(["nap_app_user"])
        );
        Assert.Contains(
            inspected.Grants,
            grant =>
                grant.Target == PostgresGrantTarget.Schema
                && grant.Roles.SequenceEqual(["nap_app_user"])
                && grant.Privileges.SequenceEqual(["USAGE"])
        );
        Assert.Contains(
            inspected.Grants,
            grant =>
                grant.Target == PostgresGrantTarget.Table
                && grant.ObjectName == "documents"
                && grant.Roles.SequenceEqual(["nap_app_user"])
                && grant.Privileges.SequenceEqual(["SELECT"])
        );
        Assert.DoesNotContain(secondDiff, op => op is CreateOrAlterRoleOperation);
        Assert.DoesNotContain(secondDiff, op => op is CreateOrReplaceFunctionOperation);
        Assert.DoesNotContain(secondDiff, op => op is GrantPrivilegesOperation);
    }

    private SchemaDefinition Inspect(NpgsqlConnection connection) =>
        ((SchemaResultOk)PostgresSchemaInspector.Inspect(connection, "public", _logger)).Value;

    private IReadOnlyList<SchemaOperation> Diff(
        SchemaDefinition current,
        SchemaDefinition desired
    ) => ((OperationsResultOk)SchemaDiff.Calculate(current, desired, logger: _logger)).Value;

    private static SchemaDefinition SupportSchema() =>
        new()
        {
            Name = "support_readback",
            Roles =
            [
                new PostgresRoleDefinition
                {
                    Name = "nap_app_user",
                    Login = false,
                    BypassRls = false,
                },
            ],
            Functions =
            [
                new PostgresFunctionDefinition
                {
                    Schema = "public",
                    Name = "current_tenant_id",
                    Returns = "uuid",
                    Language = "sql",
                    Volatility = "stable",
                    SecurityDefiner = true,
                    Body = "SELECT current_setting('app.tenant_id', true)::uuid",
                    ExecuteRoles = ["nap_app_user"],
                    RevokePublicExecute = true,
                },
            ],
            Grants =
            [
                new PostgresGrantDefinition
                {
                    Schema = "public",
                    Target = PostgresGrantTarget.Schema,
                    Privileges = ["USAGE"],
                    Roles = ["nap_app_user"],
                },
                new PostgresGrantDefinition
                {
                    Schema = "public",
                    Target = PostgresGrantTarget.Table,
                    ObjectName = "documents",
                    Privileges = ["SELECT"],
                    Roles = ["nap_app_user"],
                },
            ],
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
                    ],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                },
            ],
        };
}
