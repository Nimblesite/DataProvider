using System.Collections.Immutable;
using System.Data.Common;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Nimblesite.DataProvider.Migration.Core;
using Npgsql;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;
using AffectedRowsOk = Outcome.Result<int, Nimblesite.Sql.Model.SqlError>.Ok<
    int,
    Nimblesite.Sql.Model.SqlError
>;

namespace Nimblesite.DataProvider.Example.Tests;

/// <summary>Exercises generated customer operations against each supported database provider.</summary>
public sealed partial class GeneratedOperationsPlatformTests
{
    /// <summary>Inserts, updates, and reads customers on every database provider.</summary>
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
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
            using var connection = new NpgsqlConnection(container.GetConnectionString());
            await connection.OpenAsync().ConfigureAwait(false);
            await verify(connection, "postgres", connection.ConnectionString, "public")
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
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        using (var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false))
        {
            var first = await transaction
                .InsertCustomerAsync(
                    firstId.ToString(),
                    "Alpha Customer",
                    "alpha@example.test",
                    "555-0001",
                    "2024-01-01"
                )
                .ConfigureAwait(false);
            Assert.Equal(1, Assert.IsType<AffectedRowsOk>(first).Value);
            var second = await transaction
                .InsertCustomerAsync(
                    secondId.ToString(),
                    "Beta Customer",
                    "beta@example.test",
                    "555-0002",
                    "2024-02-02"
                )
                .ConfigureAwait(false);
            Assert.Equal(1, Assert.IsType<AffectedRowsOk>(second).Value);
            await transaction.CommitAsync().ConfigureAwait(false);
        }
        Assert.Collection(
            ReadCustomers(connection),
            actual =>
                Assert.Equal(
                    new CustomerSnapshot(
                        firstId,
                        "Alpha Customer",
                        "alpha@example.test",
                        "555-0001",
                        "2024-01-01"
                    ),
                    actual
                ),
            actual =>
                Assert.Equal(
                    new CustomerSnapshot(
                        secondId,
                        "Beta Customer",
                        "beta@example.test",
                        "555-0002",
                        "2024-02-02"
                    ),
                    actual
                )
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
            Assert.Equal(1, Assert.IsType<AffectedRowsOk>(updated).Value);
            var missing = await transaction
                .UpdateCustomerAsync(
                    Guid.NewGuid().ToString(),
                    "Missing Customer",
                    "missing@example.test",
                    "555-9998",
                    "2025-04-04"
                )
                .ConfigureAwait(false);
            Assert.Equal(0, Assert.IsType<AffectedRowsOk>(missing).Value);
            await transaction.CommitAsync().ConfigureAwait(false);
        }
        Assert.Collection(
            ReadCustomers(connection),
            actual =>
                Assert.Equal(
                    new CustomerSnapshot(
                        firstId,
                        "Alpha Updated",
                        "updated@example.test",
                        "555-9999",
                        "2025-03-03"
                    ),
                    actual
                ),
            actual =>
                Assert.Equal(
                    new CustomerSnapshot(
                        secondId,
                        "Beta Customer",
                        "beta@example.test",
                        "555-0002",
                        "2024-02-02"
                    ),
                    actual
                )
        );
    }

    private static void MigrateCustomerSchema(string provider, string output, string schemaName) =>
        MigrateSchema(
            provider,
            output,
            new SchemaDefinition
            {
                Name = "generated_operations",
                Tables = [CustomerTable(schemaName)],
            }
        );

    private static TableDefinition CustomerTable(string schemaName) =>
        new()
        {
            Schema = schemaName,
            Name = "Customer",
            Columns =
            [
                UuidColumn("Id"),
                TextColumn("CustomerName"),
                TextColumn("Email"),
                TextColumn("Phone"),
                TextColumn("CreatedDate"),
            ],
            PrimaryKey = new PrimaryKeyDefinition { Columns = ["Id"] },
        };

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

    private static ColumnDefinition TextColumn(string name) =>
        new()
        {
            Name = name,
            Type = PortableTypes.Text,
            IsNullable = false,
        };

    private static ColumnDefinition UuidColumn(string name) =>
        new()
        {
            Name = name,
            Type = PortableTypes.Uuid,
            IsNullable = false,
        };

    private static ImmutableArray<CustomerSnapshot> ReadCustomers(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT Id, CustomerName, Email, Phone, CreatedDate FROM Customer ORDER BY CustomerName";
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
}
