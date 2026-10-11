using static global::Tests.Shared.ResultAssertions;

// Implements [MIG-TEST-SCHEMA-FIXTURES].
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
public sealed partial class PostgresSupportReadbackE2ETests(PostgresContainerFixture fixture)
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
        var connection = global::Tests.Shared.ResultAssertions.Expected<NpgsqlConnection>(
            actual: _connection,
            message: "PostgreSQL connection not initialized."
        );

        var desired = SupportSchema();
        var current = Inspect(connection);
        var operations = Diff(current, desired);

        var apply = PostgresTestDb.TryApply(
            connection: connection,
            operations: operations,
            logger: _logger
        );

        PostgresTestDb.AssertApplyResult(apply: apply);

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
        (Ok(actual: PostgresSchemaInspector.Inspect(connection, "public", _logger))).Value;

    private IReadOnlyList<SchemaOperation> Diff(
        SchemaDefinition current,
        SchemaDefinition desired
    ) => (Ok(actual: SchemaDiff.Calculate(current, desired, logger: _logger))).Value;
}
