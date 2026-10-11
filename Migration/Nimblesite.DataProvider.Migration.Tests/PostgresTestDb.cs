// Implements [MIG-TEST-SCHEMA-FIXTURES].
// Implements [CI-TEST-SHARED-COMMAND].

namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// Shared PostgreSQL E2E test helper: applies a desired schema via
/// inspect → diff → apply and asserts the migration succeeded.
/// </summary>
internal static class PostgresTestDb
{
    private const string SchemaName = "public";

    public static void ApplySchema(
        NpgsqlConnection connection,
        SchemaDefinition desired,
        ILogger logger,
        bool allowDestructive = false
    )
    {
        var current = global::Tests
            .Shared.ResultAssertions.ExactOk(
                actual: PostgresSchemaInspector.Inspect(connection, "public", logger)
            )
            .Value;
        var ops = Assert
            .IsType<OperationsResultOk>(
                SchemaDiff.Calculate(current, desired, allowDestructive, logger: logger)
            )
            .Value;

        var apply = PostgresTestDb.TryApply(
            connection: connection,
            operations: ops,
            logger: logger,
            options: allowDestructive ? MigrationOptions.Destructive : MigrationOptions.Default
        );
        var error = apply is MigrationApplyResultError failure ? failure.Value.Message : null;
        Assert.True(apply is MigrationApplyResultOk, $"Migration failed: {error}");
    }

    /// <summary>Executes a non-query SQL statement on the connection.</summary>
    internal static void Exec(NpgsqlConnection conn, string sql)
    {
        DatabaseCommands.Execute(connection: conn, configure: command => command.CommandText = sql);
    }

    /// <summary>Executes a non-query SQL statement within the given transaction.</summary>
    internal static void Exec(NpgsqlConnection conn, NpgsqlTransaction tx, string sql)
    {
        using var command = conn.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>Inspects the live PostgreSQL schema, asserting success.</summary>
    internal static SchemaDefinition Inspect(NpgsqlConnection connection, ILogger logger)
    {
        var result = PostgresSchemaInspector.Inspect(connection, SchemaName, logger);
        if (result is SchemaResultOk ok)
        {
            return ok.Value;
        }

        Assert.Fail("Expected PostgreSQL schema inspection to succeed.");
        return SchemaTestData.Schema(name: "failed", tables: []);
    }

    /// <summary>Calculates the schema diff between current and desired, asserting success.</summary>
    internal static IReadOnlyList<SchemaOperation> Calculate(
        SchemaDefinition current,
        SchemaDefinition desired,
        ILogger logger,
        bool allowDestructive = false
    )
    {
        var result = SchemaDiff.Calculate(current, desired, allowDestructive, logger);
        if (result is OperationsResultOk ok)
        {
            return ok.Value;
        }

        Assert.Fail("Expected PostgreSQL schema diff to succeed.");
        return [];
    }

    /// <summary>Calculates the schema diff between current and desired, asserting success.</summary>
    internal static IReadOnlyList<SchemaOperation> CalculateChanges(
        NpgsqlConnection connection,
        SchemaDefinition desired,
        ILogger logger
    ) => Calculate(current: Inspect(connection, logger), desired: desired, logger: logger);

    /// <summary>Applies the operations against PostgreSQL, asserting the migration succeeded.</summary>
    internal static void Apply(
        NpgsqlConnection connection,
        IReadOnlyList<SchemaOperation> operations,
        ILogger logger,
        MigrationOptions? options = null
    )
    {
        var result = TryApply(
            connection: connection,
            operations: operations,
            logger: logger,
            options: options
        );
        SqliteTestDb.AssertApplyOk(result: result);
    }

    // Implements [MIG-TEST-POSTGRES-RUNNER].
    internal static MigrationApplyResult TryApply(
        NpgsqlConnection connection,
        IReadOnlyList<SchemaOperation> operations,
        ILogger logger,
        MigrationOptions? options = null
    ) =>
        SqliteTestDb.ApplyWith(
            connection,
            operations,
            PostgresDdlGenerator.Generate,
            logger,
            options
        );

    // Implements [MIG-TEST-SHARED-TENANT-SESSION].
    internal static void SetTenantSession(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string role,
        Guid? tenant,
        Guid? user,
        string settingNamespace
    )
    {
        Exec(conn: connection, tx: transaction, sql: $"SET LOCAL ROLE {role}");
        Exec(
            conn: connection,
            tx: transaction,
            sql: $"SET LOCAL {settingNamespace}.tenant_id = '{tenant?.ToString() ?? string.Empty}'"
        );
        Exec(
            conn: connection,
            tx: transaction,
            sql: $"SET LOCAL {settingNamespace}.user_id = '{user?.ToString() ?? string.Empty}'"
        );
    }

    internal static int CountDocuments(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        using var command = connection.CreateCommand();
        VerifyReusableWorkflow8(
            input0: command,
            input1: transaction,
            value0: "SELECT count(*) FROM public.documents"
        );
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    internal static void InsertTenantDocument(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenant,
        string title
    ) =>
        Exec(
            conn: connection,
            tx: transaction,
            sql: $"INSERT INTO public.documents(id, tenant_id, title) VALUES ('{Guid.NewGuid()}', '{tenant}', '{title}')"
        );

    internal static void SeedTenantDocuments(
        NpgsqlConnection connection,
        Guid tenantA,
        Guid tenantB,
        Guid userA,
        bool includeWriterRole
    )
    {
        var roleColumns = includeWriterRole ? ", role" : string.Empty;
        var roleValues = includeWriterRole ? ", 'writer'" : string.Empty;
        Exec(
            conn: connection,
            sql: $"INSERT INTO public.tenant_members(id, tenant_id, user_id{roleColumns}) VALUES ('{Guid.NewGuid()}', '{tenantA}', '{userA}'{roleValues})"
        );
        Exec(
            conn: connection,
            sql: $"INSERT INTO public.documents(id, tenant_id, title) VALUES ('{Guid.NewGuid()}', '{tenantA}', 'visible')"
        );
        Exec(
            conn: connection,
            sql: $"INSERT INTO public.documents(id, tenant_id, title) VALUES ('{Guid.NewGuid()}', '{tenantB}', 'hidden')"
        );
    }

    // Implements [MIG-TEST-SHARED-TENANT-SESSION].
    internal static void WithTenantTransaction(
        NpgsqlConnection connection,
        string role,
        Guid? tenant,
        Guid? user,
        string settingNamespace,
        Action<NpgsqlTransaction> run,
        bool commit
    )
    {
        using var transaction = connection.BeginTransaction();
        SetTenantSession(
            connection: connection,
            transaction: transaction,
            role: role,
            tenant: tenant,
            user: user,
            settingNamespace: settingNamespace
        );
        run(obj: transaction);
        if (commit)
        {
            transaction.Commit();
        }
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void AssertTwoAddColumns(IReadOnlyList<SchemaOperation> upgradeOps)
    {
        Assert.Equal(2, upgradeOps.Count);
        Assert.All(upgradeOps, op => Assert.IsType<AddColumnOperation>(op));
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void AssertApplyResult(MigrationApplyResult apply)
    {
        var failure = apply is MigrationApplyResultError error ? error.Value : null;
        Assert.True(apply is MigrationApplyResultOk, $"Migration failed: {failure}");
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void InsertAsTenant(
        NpgsqlConnection connection,
        string role,
        Guid? tenant,
        Guid? user,
        string settingNamespace,
        params string[] sqlStatements
    ) =>
        WithTenantTransaction(
            connection: connection,
            role: role,
            tenant: tenant,
            user: user,
            settingNamespace: settingNamespace,
            run: transaction =>
            {
                foreach (var sql in sqlStatements)
                {
                    Exec(conn: connection, tx: transaction, sql: sql);
                }
            },
            commit: true
        );

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static long CountAsTenant(
        NpgsqlConnection connection,
        string role,
        Guid? tenant,
        Guid? user,
        string settingNamespace,
        string sql
    )
    {
        long count = 0;
        WithTenantTransaction(
            connection: connection,
            role: role,
            tenant: tenant,
            user: user,
            settingNamespace: settingNamespace,
            run: transaction =>
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = sql;
                var scalar = command.ExecuteScalar();
                count = scalar is long value ? value : 0;
            },
            commit: false
        );
        return count;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void AssertTenantSeesSingle(
        NpgsqlConnection connection,
        string role,
        Guid tenant,
        Guid user,
        Guid expectedId,
        string sql
    ) =>
        WithTenantTransaction(
            connection: connection,
            role: role,
            tenant: tenant,
            user: user,
            settingNamespace: "rls",
            run: transaction =>
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = sql;
                var ids = DatabaseCommands.ReadAll(command, reader => reader.GetGuid(0));
                Assert.Single(ids);
                Assert.Equal(expectedId, ids[0]);
            },
            commit: false
        );

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void WithFirstRow(
        System.Data.Common.DbConnection connection,
        string sql,
        Action<System.Data.Common.DbDataReader> assert
    )
    {
        using var command = connection.CreateCommand();
        using var reader = DatabaseCommands.ReadFirstRow(
            command,
            configure: command => command.CommandText = sql
        );
        assert(obj: reader);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void SeedTwoTenantsAndAssertIsolation(
        NpgsqlConnection connection,
        string role,
        string table,
        Guid expectedId,
        Guid tenantA,
        Guid tenantB,
        Guid userA,
        Guid userB,
        string selectSql
    )
    {
        InsertAsTenant(
            connection,
            role,
            tenantA,
            userA,
            "rls",
            $"INSERT INTO {table}(id, tenant_id, title) VALUES ('{expectedId}', '{tenantA}', 'a')"
        );
        InsertAsTenant(
            connection,
            role,
            tenantB,
            userB,
            "rls",
            $"INSERT INTO {table}(id, tenant_id, title) VALUES ('{Guid.NewGuid()}', '{tenantB}', 'b')"
        );
        AssertTenantSeesSingle(connection, role, tenantA, userA, expectedId, selectSql);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow8(
        NpgsqlCommand input0,
        NpgsqlTransaction input1,
        string value0
    )
    {
        input0.Transaction = input1;
        input0.CommandText = value0;
    }
}
