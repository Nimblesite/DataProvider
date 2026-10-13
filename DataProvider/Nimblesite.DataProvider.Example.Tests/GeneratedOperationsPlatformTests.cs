using System.Collections.Immutable;
using System.Data.Common;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Nimblesite.DataProvider.Core.CodeGeneration;
using Nimblesite.DataProvider.Migration.Core;
using Npgsql;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using ColumnsOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.DataProvider.Core.DatabaseColumn>,
    Nimblesite.Sql.Model.SqlError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.DataProvider.Core.DatabaseColumn>,
    Nimblesite.Sql.Model.SqlError
>;

namespace Nimblesite.DataProvider.Example.Tests;

/// <summary>Exercises generated customer operations against each supported database provider.</summary>
public sealed partial class GeneratedOperationsPlatformTests
{
    /// <summary>Inserts, updates, and reads customers on every database provider.</summary>
    [Theory]
    [MemberData(
        nameof(GeneratedCustomerOperations_PersistTwoCustomersAndOnlyUpdateTheTargetInlineCases)
    )]
    public async Task GeneratedCustomerOperations_PersistTwoCustomersAndOnlyUpdateTheTarget(
        string provider
    ) => await WithProviderAsync(provider, VerifyOperationsAsync).ConfigureAwait(false);

    private static async Task WithProviderAsync(
        string provider,
        Func<DbConnection, string, string, string, Task> verify
    )
    {
        if (provider == "sqlite")
        {
            await VerifySqliteAsync(verify).ConfigureAwait(false);
            return;
        }
        if (provider == "postgres")
        {
            await VerifyPostgresAsync(verify).ConfigureAwait(false);
            return;
        }
        Assert.Equal("sqlserver", provider);
        await VerifySqlServerAsync(verify).ConfigureAwait(false);
    }

    private static async Task VerifySqliteAsync(
        Func<DbConnection, string, string, string, Task> verify
    )
    {
        var path = Path.Combine(Path.GetTempPath(), $"generated_ops_{Guid.NewGuid():N}.db");
        try
        {
            using var connection = new SqliteConnection($"Data Source={path}");
            await connection.OpenAsync().ConfigureAwait(false);
            await verify(connection, "sqlite", path, "main").ConfigureAwait(false);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task VerifyPostgresAsync(
        Func<DbConnection, string, string, string, Task> verify
    )
    {
        var container = new PostgreSqlBuilder()
            .WithDatabase("generated_ops")
            .WithUsername("test")
            .WithPassword("test")
            .Build();
        try
        {
            await container.StartAsync().ConfigureAwait(false);
            // An opened NpgsqlConnection drops the password from ConnectionString, so
            // the migrate CLI gets the container's full connection string.
            using var connection = new NpgsqlConnection(container.GetConnectionString());
            await connection.OpenAsync().ConfigureAwait(false);
            await verify(connection, "postgres", container.GetConnectionString(), "public")
                .ConfigureAwait(false);
        }
        finally
        {
            await container.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task VerifySqlServerAsync(
        Func<DbConnection, string, string, string, Task> verify
    )
    {
        var container = new MsSqlBuilder().Build();
        try
        {
            await container.StartAsync().ConfigureAwait(false);
            using var admin = new SqlConnection(container.GetConnectionString());
            await admin.OpenAsync().ConfigureAwait(false);
            using var create = admin.CreateCommand();
            create.CommandText = "CREATE DATABASE [generated_ops]";
            await create.ExecuteNonQueryAsync().ConfigureAwait(false);
            var builder = new SqlConnectionStringBuilder(container.GetConnectionString())
            {
                InitialCatalog = "generated_ops",
            };
            using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            await verify(connection, "sqlserver", builder.ConnectionString, "dbo")
                .ConfigureAwait(false);
        }
        finally
        {
            await container.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task VerifyOperationsAsync(
        DbConnection connection,
        string provider,
        string output,
        string schema
    )
    {
        MigrateCustomerSchema(provider, output, schema);
        var metadata = await new AdoNetDatabaseEffects(
            connectionFactory: value =>
                provider switch
                {
                    "sqlite" => new SqliteConnection(value),
                    "postgres" => new NpgsqlConnection(value),
                    _ => new SqlConnection(value),
                },
            typeMapper: (type, _, nullable) => type.FullName + (nullable ? "?" : ""),
            behavior: System.Data.CommandBehavior.SchemaOnly | System.Data.CommandBehavior.KeyInfo
        ).GetColumnMetadataFromSqlAsync(connectionString: provider == "sqlite" ? connection.ConnectionString : output, sql: $"SELECT * FROM \"{schema}\".\"Customer\"", parameters: []).ConfigureAwait(false);
        Assert.Equal(
            ["Id", "CustomerName", "Email", "Phone", "CreatedDate"],
            Assert.IsType<ColumnsOk>(metadata).Value.Select(column => column.Name)
        );
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        using (var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false))
        {
            await VerifyReusableWorkflow5(
                    input0: transaction,
                    input1: firstId,
                    value0: "Alpha Customer",
                    value1: "alpha@example.test",
                    value2: "555-0001",
                    value3: "2024-01-01"
                )
                .ConfigureAwait(false);
            await VerifyReusableWorkflow5(
                    input0: transaction,
                    input1: secondId,
                    value0: "Beta Customer",
                    value1: "beta@example.test",
                    value2: "555-0002",
                    value3: "2024-02-02"
                )
                .ConfigureAwait(false);
            await transaction.CommitAsync().ConfigureAwait(false);
        }
        Assert.Collection(
            ReadCustomers(connection),
            actual => Assert.Equal(Dataset_VerifyOperationsAsync_1(firstId: firstId), actual),
            actual => Assert.Equal(Dataset_VerifyOperationsAsync_2(secondId: secondId), actual)
        );

        using (var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false))
        {
            var updated = await transaction
                .UpdateCustomerAsync(
                    firstId.ToString(),
                    "Alpha Updated",
                    "updated@example.test",
                    "555-9999",
                    "2025-03-03"
                )
                .ConfigureAwait(false);
            Assert.Equal(1, global::Tests.Shared.ResultAssertions.ExactOk(actual: updated).Value);
            var missing = await transaction
                .UpdateCustomerAsync(
                    Guid.NewGuid().ToString(),
                    "Missing Customer",
                    "missing@example.test",
                    "555-9998",
                    "2025-04-04"
                )
                .ConfigureAwait(false);
            Assert.Equal(0, global::Tests.Shared.ResultAssertions.ExactOk(actual: missing).Value);
            await transaction.CommitAsync().ConfigureAwait(false);
        }
        Assert.Collection(
            ReadCustomers(connection),
            actual => Assert.Equal(Dataset_VerifyOperationsAsync_3(firstId: firstId), actual),
            actual => Assert.Equal(Dataset_VerifyOperationsAsync_4(secondId: secondId), actual)
        );
    }

    private static void MigrateCustomerSchema(string provider, string output, string schemaName) =>
        MigrateSchema(
            provider,
            output,
            new SchemaDefinition
            {
                Name = "generated_operations",
                Tables = [CustomerTable(provider, schemaName)],
            }
        );

    private static void MigrateSchema(string provider, string output, SchemaDefinition schema)
    {
        var schemaPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(schemaPath, SchemaYamlSerializer.ToYaml(schema));
            var exitCode = DataProviderMigrate.Program.Main([
                "migrate",
                "--schema",
                schemaPath,
                "--provider",
                provider,
                "--output",
                output,
            ]);
            Assert.True(
                exitCode == 0,
                $"{provider} schema migration failed with exit code {exitCode}"
            );
        }
        finally
        {
            File.Delete(schemaPath);
        }
    }

    // The generated operations bind ids as strings: the Example schema they are generated
    // from declares text ids. PostgreSQL has no implicit text -> uuid assignment cast, so
    // the ids are text columns there; SQLite and SQL Server convert the strings to UUIDs.
    private static ColumnDefinition IdColumn(string provider, string name) =>
        provider == "postgres" ? TextColumn(name) : UuidColumn(name);

    private static ImmutableArray<CustomerSnapshot> ReadCustomers(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """SELECT "Id", "CustomerName", "Email", "Phone", "CreatedDate" FROM "Customer" ORDER BY "CustomerName" """;
        using var reader = command.ExecuteReader();
        var rows = ImmutableArray.CreateBuilder<CustomerSnapshot>();
        while (reader.Read())
        {
            var rawId = Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture);
            Assert.True(Guid.TryParse(rawId, out var id), $"Invalid customer ID: {rawId}");
            rows.Add(
                new CustomerSnapshot(
                    id,
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4)
                )
            );
        }
        return rows.ToImmutable();
    }

    private sealed record CustomerSnapshot(
        Guid Id,
        string Name,
        string Email,
        string Phone,
        string CreatedDate
    );

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static async global::System.Threading.Tasks.Task VerifyReusableWorkflow5(
        DbTransaction input0,
        Guid input1,
        string value0,
        string value1,
        string value2,
        string value3
    )
    {
        var first = await input0
            .InsertCustomerAsync(input1.ToString(), value0, value1, value2, value3)
            .ConfigureAwait(false);
        Assert.Equal(1, global::Tests.Shared.ResultAssertions.ExactOk(actual: first).Value);
    }
}
