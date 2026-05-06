namespace Nimblesite.DataProvider.Migration.Tests;

[Collection(PostgresTestSuite.Name)]
public sealed class PostgresColumnCheckConstraintTests(PostgresContainerFixture fixture)
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
                """
                name: issue51
                tables:
                  - schema: public
                    name: bundles
                    columns:
                      - name: id
                        type: Uuid
                        isNullable: false
                      - name: tenant_id
                        type: Uuid
                        isNullable: false
                      - name: sha256
                        type: Text
                        isNullable: false
                        checkConstraintName: bundles_sha256_fmt_chk
                        checkConstraint: sha256 ~ '^[0-9a-f]{64}$'
                      - name: size_bytes
                        type: BigInt
                        isNullable: false
                        checkConstraintName: bundles_size_bytes_nonneg_chk
                        checkConstraint: size_bytes >= 0
                    primaryKey:
                      columns:
                        - id
                    uniqueConstraints:
                      - name: uq_bundles_tenant_sha
                        columns:
                          - tenant_id
                          - sha256
                """
            );

            Apply(connection, Calculate(Inspect(connection), schema));

            Assert.Equal(
                [
                    "bundles_sha256_fmt_chk",
                    "bundles_size_bytes_nonneg_chk",
                    "uq_bundles_tenant_sha",
                ],
                ConstraintNames(connection)
            );

            var inspected = Inspect(connection).Tables.Single(t => t.Name == "bundles");
            var sha256 = inspected.Columns.Single(c => c.Name == "sha256");
            var sizeBytes = inspected.Columns.Single(c => c.Name == "size_bytes");

            Assert.Equal("bundles_sha256_fmt_chk", sha256.CheckConstraintName);
            Assert.Equal("bundles_size_bytes_nonneg_chk", sizeBytes.CheckConstraintName);
            Assert.DoesNotContain(
                Calculate(Inspect(connection), schema),
                operation => operation is AddCheckConstraintOperation
            );
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(true);
            NpgsqlConnection.ClearPool(connection);
        }
    }

    private static SchemaDefinition Inspect(NpgsqlConnection connection)
    {
        var result = PostgresSchemaInspector.Inspect(connection, SchemaName, Logger);
        if (result is SchemaResultOk ok)
        {
            return ok.Value;
        }

        Assert.Fail("Expected PostgreSQL schema inspection to succeed.");
        return Schema.Define("failed").Build();
    }

    private static IReadOnlyList<SchemaOperation> Calculate(
        SchemaDefinition current,
        SchemaDefinition desired
    )
    {
        var result = SchemaDiff.Calculate(current, desired, logger: Logger);
        if (result is OperationsResultOk ok)
        {
            return ok.Value;
        }

        Assert.Fail("Expected PostgreSQL schema diff to succeed.");
        return [];
    }

    private static void Apply(
        NpgsqlConnection connection,
        IReadOnlyList<SchemaOperation> operations
    )
    {
        var result = MigrationRunner.Apply(
            connection,
            operations,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            Logger
        );
        var failure = result is MigrationApplyResultError error ? error.Value.ToString() : "";

        Assert.True(result is MigrationApplyResultOk, $"Migration failed: {failure}");
    }

    private static string[] ConstraintNames(NpgsqlConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.conname
            FROM pg_constraint c
            JOIN pg_class t ON t.oid = c.conrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE n.nspname = @schema
            AND t.relname = 'bundles'
            AND c.conname IN (
                'bundles_sha256_fmt_chk',
                'bundles_size_bytes_nonneg_chk',
                'uq_bundles_tenant_sha'
            )
            ORDER BY c.conname
            """;
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
