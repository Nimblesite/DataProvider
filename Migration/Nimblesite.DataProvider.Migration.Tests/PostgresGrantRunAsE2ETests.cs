namespace Nimblesite.DataProvider.Migration.Tests;

[Collection(PostgresTestSuite.Name)]
public sealed class PostgresGrantRunAsE2ETests(PostgresContainerFixture fixture)
{
    [Fact]
    public void GrantRunAs_AppliesNapAuthShapeGrantsThroughSchemaOwner()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var owner = $"grant_owner_{suffix}";
        var migrate = $"grant_migrate_{suffix}";
        var appUser = $"grant_app_user_{suffix}";
        var appAdmin = $"grant_app_admin_{suffix}";
        var schema = $"auth_{suffix}";

        using var connection = fixture.CreateDatabase("grant_run_as");
        Exec(connection, $"CREATE ROLE {Q(owner)} NOLOGIN");
        Exec(connection, $"CREATE ROLE {Q(migrate)} LOGIN PASSWORD 'test'");
        Exec(connection, $"CREATE ROLE {Q(appUser)} NOLOGIN");
        Exec(connection, $"CREATE ROLE {Q(appAdmin)} NOLOGIN");
        Exec(connection, $"GRANT {Q(owner)} TO {Q("test")}");
        Exec(connection, $"GRANT {Q(owner)} TO {Q(migrate)}");
        Exec(connection, $"GRANT CONNECT ON DATABASE {Q(connection.Database)} TO {Q(migrate)}");
        Exec(connection, $"CREATE SCHEMA {Q(schema)} AUTHORIZATION {Q(owner)}");
        Exec(
            connection,
            $"SET ROLE {Q(owner)}; CREATE TABLE {Q(schema)}.{Q("users")}(id uuid PRIMARY KEY); RESET ROLE"
        );
        using var migrateConnection = OpenRoleConnection(connection, migrate);

        var result = MigrationRunner.Apply(
            migrateConnection,
            NapAuthGrants(schema, owner, appUser, appAdmin),
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            NullLogger.Instance
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
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var owner = $"grant_owner_{suffix}";
        var migrate = $"grant_migrate_{suffix}";
        var appUser = $"grant_app_user_{suffix}";

        using var connection = fixture.CreateDatabase("grant_run_as_missing");
        Exec(connection, $"CREATE ROLE {Q(owner)} NOLOGIN");
        Exec(connection, $"CREATE ROLE {Q(migrate)} LOGIN PASSWORD 'test'");
        Exec(connection, $"CREATE ROLE {Q(appUser)} NOLOGIN");
        Exec(connection, $"GRANT CONNECT ON DATABASE {Q(connection.Database)} TO {Q(migrate)}");
        using var migrateConnection = OpenRoleConnection(connection, migrate);

        var result = MigrationRunner.Apply(
            migrateConnection,
            [
                new GrantPrivilegesOperation(
                    new PostgresGrantDefinition
                    {
                        Schema = "public",
                        Target = PostgresGrantTarget.Schema,
                        Privileges = ["USAGE"],
                        Roles = [appUser],
                        RunAs = owner,
                    }
                ),
            ],
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            NullLogger.Instance
        );

        Assert.True(result is MigrationApplyResultError);
        var error = ((MigrationApplyResultError)result).Value;
        Assert.Contains("MIG-E-PG-GRANT-RUN-AS-MISSING-MEMBERSHIP", error.Message);
        Assert.Contains($"GRANT \"{owner}\" TO \"{migrate}\"", error.Message);
    }

    private static IReadOnlyList<SchemaOperation> NapAuthGrants(
        string schema,
        string owner,
        string appUser,
        string appAdmin
    ) =>
        [
            new GrantPrivilegesOperation(
                new PostgresGrantDefinition
                {
                    Schema = schema,
                    Target = PostgresGrantTarget.Schema,
                    Privileges = ["USAGE"],
                    Roles = [appUser, appAdmin],
                    RunAs = owner,
                }
            ),
            new GrantPrivilegesOperation(
                new PostgresGrantDefinition
                {
                    Schema = schema,
                    Target = PostgresGrantTarget.Table,
                    ObjectName = "users",
                    Privileges = ["SELECT", "INSERT"],
                    Roles = [appAdmin],
                    RunAs = owner,
                }
            ),
            new GrantPrivilegesOperation(
                new PostgresGrantDefinition
                {
                    Schema = schema,
                    Target = PostgresGrantTarget.Table,
                    ObjectName = "users",
                    Privileges = ["SELECT"],
                    Roles = [appUser],
                    RunAs = owner,
                }
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

    private static void Exec(NpgsqlConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
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
