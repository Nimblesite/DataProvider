using static global::Tests.Shared.ResultAssertions;

// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

[Collection(PostgresTestSuite.Name)]
public sealed partial class PostgresGrantRunAsE2ETests(PostgresContainerFixture fixture)
{
    [Fact]
    public void GrantRunAs_AppliesNapAuthShapeGrantsThroughSchemaOwner()
    {
        var (suffix, owner, migrate, appUser) = NewGrantRoles();
        var appAdmin = $"grant_app_admin_{suffix}";
        var schema = $"auth_{suffix}";

        using var connection = fixture.CreateDatabase("grant_run_as");
        PostgresTestDb.Exec(connection, $"CREATE ROLE {Q(owner)} NOLOGIN");
        PostgresTestDb.Exec(connection, $"CREATE ROLE {Q(migrate)} LOGIN PASSWORD 'test'");
        PostgresTestDb.Exec(connection, $"CREATE ROLE {Q(appUser)} NOLOGIN");
        PostgresTestDb.Exec(connection, $"CREATE ROLE {Q(appAdmin)} NOLOGIN");
        PostgresTestDb.Exec(connection, $"GRANT {Q(owner)} TO {Q("test")}");
        PostgresTestDb.Exec(connection, $"GRANT {Q(owner)} TO {Q(migrate)}");
        PostgresTestDb.Exec(
            connection,
            $"GRANT CONNECT ON DATABASE {Q(connection.Database)} TO {Q(migrate)}"
        );
        PostgresTestDb.Exec(connection, $"CREATE SCHEMA {Q(schema)} AUTHORIZATION {Q(owner)}");
        PostgresTestDb.Exec(
            connection,
            $"SET ROLE {Q(owner)}; CREATE TABLE {Q(schema)}.{Q("users")}(id uuid PRIMARY KEY); RESET ROLE"
        );
        using var migrateConnection = OpenRoleConnection(connection, migrate);

        var result = PostgresTestDb.TryApply(
            connection: migrateConnection,
            operations: NapAuthGrants(schema, owner, appUser, appAdmin),
            logger: NullLogger.Instance
        );

        Assert.True(result is MigrationApplyResultOk);
        Assert.True(HasSchemaPrivilege(connection, appUser, schema, "USAGE"));
        Assert.True(HasSchemaPrivilege(connection, appAdmin, schema, "USAGE"));
        Assert.True(HasTablePrivilege(connection, appUser, schema, "users", "SELECT"));
        Assert.True(HasTablePrivilege(connection, appAdmin, schema, "users", "INSERT"));
    }

    [Fact]
    public void GrantRunAs_MissingRoleMembership_ReturnsClearGrantToMessage()
    {
        var (_, owner, migrate, appUser) = NewGrantRoles();

        using var connection = fixture.CreateDatabase("grant_run_as_missing");
        PostgresTestDb.Exec(connection, $"CREATE ROLE {Q(owner)} NOLOGIN");
        PostgresTestDb.Exec(connection, $"CREATE ROLE {Q(migrate)} LOGIN PASSWORD 'test'");
        PostgresTestDb.Exec(connection, $"CREATE ROLE {Q(appUser)} NOLOGIN");
        PostgresTestDb.Exec(
            connection,
            $"GRANT CONNECT ON DATABASE {Q(connection.Database)} TO {Q(migrate)}"
        );
        using var migrateConnection = OpenRoleConnection(connection, migrate);

        var result = PostgresTestDb.TryApply(
            connection: migrateConnection,
            operations:
            [
                Dataset_GrantRunAs_MissingRoleMembership_ReturnsClearGrantToMessage_1(
                    appUser: appUser,
                    owner: owner
                ),
            ],
            logger: NullLogger.Instance
        );

        var error = Expected<MigrationApplyResultError>(actual: result).Value;
        Assert.Contains("MIG-E-PG-GRANT-RUN-AS-MISSING-MEMBERSHIP", error.Message);
        Assert.Contains($"GRANT \"{owner}\" TO \"{migrate}\"", error.Message);
    }

    private static (string Suffix, string Owner, string Migrate, string AppUser) NewGrantRoles()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return (
            suffix,
            $"grant_owner_{suffix}",
            $"grant_migrate_{suffix}",
            $"grant_app_user_{suffix}"
        );
    }

    private static IReadOnlyList<SchemaOperation> NapAuthGrants(
        string schema,
        string owner,
        string appUser,
        string appAdmin
    ) =>
        [
            new GrantPrivilegesOperation(
                SchemaTestData.PostgresGrant(
                    schema: schema,
                    target: PostgresGrantTarget.Schema,
                    privileges: ["USAGE"],
                    roles: [appUser, appAdmin],
                    runAs: owner
                )
            ),
            new GrantPrivilegesOperation(
                SchemaTestData.PostgresGrant(
                    schema: schema,
                    target: PostgresGrantTarget.Table,
                    objectName: "users",
                    privileges: ["SELECT", "INSERT"],
                    roles: [appAdmin],
                    runAs: owner
                )
            ),
            new GrantPrivilegesOperation(
                SchemaTestData.PostgresGrant(
                    schema: schema,
                    target: PostgresGrantTarget.Table,
                    objectName: "users",
                    privileges: ["SELECT"],
                    roles: [appUser],
                    runAs: owner
                )
            ),
        ];

    private static bool HasSchemaPrivilege(
        NpgsqlConnection connection,
        string role,
        string schema,
        string privilege
    ) =>
        ScalarBool(
            connection,
            "SELECT has_schema_privilege(@role, @schema, @privilege)",
            ("role", role),
            ("schema", schema),
            ("privilege", privilege)
        );

    private static bool HasTablePrivilege(
        NpgsqlConnection connection,
        string role,
        string schema,
        string table,
        string privilege
    ) =>
        ScalarBool(
            connection,
            "SELECT has_table_privilege(@role, @table, @privilege)",
            ("role", role),
            ("table", $"{schema}.{table}"),
            ("privilege", privilege)
        );

    private static bool ScalarBool(
        NpgsqlConnection connection,
        string sql,
        params (string Name, string Value)[] parameters
    )
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        }
        return command.ExecuteScalar() is true;
    }

    private static NpgsqlConnection OpenRoleConnection(
        NpgsqlConnection adminConnection,
        string role
    )
    {
        var connectionString = new NpgsqlConnectionStringBuilder(adminConnection.ConnectionString)
        {
            Username = role,
            Password = "test",
            Pooling = false,
        }.ConnectionString;
        var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static string Q(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
