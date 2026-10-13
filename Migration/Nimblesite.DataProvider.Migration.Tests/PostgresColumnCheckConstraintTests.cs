namespace Nimblesite.DataProvider.Migration.Tests;

[Collection(PostgresTestSuite.Name)]
public sealed partial class PostgresColumnCheckConstraintTests(PostgresContainerFixture fixture)
{
    private const string SchemaName = "public";
    private static readonly ILogger Logger = NullLogger.Instance;

    [Fact]
    public async Task ApplyYaml_WhenColumnCheckConstraintHasName_CreatesNamedPostgresConstraints()
    {
        // Implements [MIG-PG-NAMED-COLUMN-CHECK-CONSTRAINT].
        var connection = await fixture
            .CreateDatabaseAsync("column_check_constraints")
            .ConfigureAwait(true);

        try
        {
            var schema = SchemaYamlSerializer.FromYaml(
                Data_ApplyYaml_WhenColumnCheckConstraintHasName_CreatesNamedPostgresConstraints_1
            );

            PostgresTestDb.Apply(
                connection,
                PostgresTestDb.Calculate(
                    PostgresTestDb.Inspect(connection, Logger),
                    schema,
                    Logger
                ),
                Logger
            );

            Assert.Equal(
                [
                    "bundles_sha256_fmt_chk",
                    "bundles_size_bytes_nonneg_chk",
                    "uq_bundles_tenant_sha",
                ],
                ConstraintNames(connection)
            );

            var inspected = PostgresTestDb
                .Inspect(connection, Logger)
                .Tables.Single(t => t.Name == "bundles");
            var sha256 = inspected.Columns.Single(c => c.Name == "sha256");
            var sizeBytes = inspected.Columns.Single(c => c.Name == "size_bytes");

            Assert.Equal("bundles_sha256_fmt_chk", sha256.CheckConstraintName);
            Assert.Equal("bundles_size_bytes_nonneg_chk", sizeBytes.CheckConstraintName);
            Assert.DoesNotContain(
                PostgresTestDb.Calculate(
                    PostgresTestDb.Inspect(connection, Logger),
                    schema,
                    Logger
                ),
                operation => operation is AddCheckConstraintOperation
            );
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(true);
            NpgsqlConnection.ClearPool(connection);
        }
    }

    private static string[] ConstraintNames(NpgsqlConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = Data_ConstraintNames_1;
        command.Parameters.AddWithValue("@schema", SchemaName);

        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names.ToArray();
    }
}
