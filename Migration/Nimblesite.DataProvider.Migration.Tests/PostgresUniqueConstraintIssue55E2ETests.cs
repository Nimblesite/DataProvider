namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// Implements [MIG-UNIQUE-CONSTRAINT-DIFF] (#55): re-running the migrator
/// after a YAML <c>uniqueConstraints</c> entry was added to an existing
/// table must detect that the constraint is missing in PostgreSQL and emit
/// an <see cref="AddUniqueConstraintOperation"/>. The CLI used to report
/// "Schema is up to date" because an inspector + diff path mishandled the
/// case where a foreign key was declared in the same table.
/// </summary>
[Collection(PostgresTestSuite.Name)]
public sealed class PostgresUniqueConstraintIssue55E2ETests(PostgresContainerFixture fixture)
{
    private const string SchemaName = "public";
    private static readonly ILogger Logger = NullLogger.Instance;

    [Fact]
    public async Task Calculate_AddingCompositeUniqueOnExistingTableWithFk_EmitsAddUniqueOp()
    {
        var fixtureConnectionString = await fixture
            .CreateDatabaseConnectionStringAsync("issue55_unique")
            .ConfigureAwait(true);
        var connectionString = new NpgsqlConnectionStringBuilder(fixtureConnectionString)
        {
            Pooling = false,
        }.ConnectionString;
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(true);

        try
        {
            PostgresTestDb.Apply(
                connection,
                PostgresTestDb.Calculate(
                    PostgresTestDb.Inspect(connection, Logger),
                    SchemaWithoutUnique(),
                    Logger
                ),
                Logger
            );

            var live = PostgresTestDb.Inspect(connection, Logger);
            var agentConfigs = live.Tables.Single(t => t.Name == "agent_configs");
            Assert.Empty(agentConfigs.UniqueConstraints);

            var upgrade = PostgresTestDb.Calculate(
                PostgresTestDb.Inspect(connection, Logger),
                SchemaWithUnique(),
                Logger
            );

            Assert.Contains(
                upgrade,
                op =>
                    op is AddUniqueConstraintOperation add
                    && add.UniqueConstraint.Name == "uq_agent_configs_tenant_name"
                    && add.UniqueConstraint.Columns.SequenceEqual(["tenant_id", "name"])
            );

            PostgresTestDb.Apply(connection, upgrade, Logger);

            var converged = PostgresTestDb
                .Inspect(connection, Logger)
                .Tables.Single(t => t.Name == "agent_configs");
            Assert.Contains(
                converged.UniqueConstraints,
                c =>
                    c.Name == "uq_agent_configs_tenant_name"
                    && c.Columns.SequenceEqual(["tenant_id", "name"])
            );

            var replay = PostgresTestDb.Calculate(
                PostgresTestDb.Inspect(connection, Logger),
                SchemaWithUnique(),
                Logger
            );
            Assert.DoesNotContain(replay, op => op is AddUniqueConstraintOperation);
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(true);
            NpgsqlConnection.ClearPool(connection);
        }
    }

    private static SchemaDefinition SchemaWithoutUnique() => Schema(includeUnique: false);

    private static SchemaDefinition SchemaWithUnique() => Schema(includeUnique: true);

    private static SchemaDefinition Schema(bool includeUnique)
    {
        var tenants = new TableDefinition
        {
            Schema = SchemaName,
            Name = "tenants",
            Columns =
            [
                new ColumnDefinition
                {
                    Name = "id",
                    Type = PortableTypes.Uuid,
                    IsNullable = false,
                },
            ],
            PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
        };
        var agentConfigs = new TableDefinition
        {
            Schema = SchemaName,
            Name = "agent_configs",
            Columns =
            [
                new ColumnDefinition
                {
                    Name = "id",
                    Type = PortableTypes.Uuid,
                    IsNullable = false,
                },
                new ColumnDefinition
                {
                    Name = "tenant_id",
                    Type = PortableTypes.Uuid,
                    IsNullable = false,
                },
                new ColumnDefinition
                {
                    Name = "name",
                    Type = PortableTypes.Text,
                    IsNullable = false,
                },
            ],
            PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
            ForeignKeys =
            [
                new ForeignKeyDefinition
                {
                    Columns = ["tenant_id"],
                    ReferencedSchema = SchemaName,
                    ReferencedTable = "tenants",
                    ReferencedColumns = ["id"],
                    OnDelete = ForeignKeyAction.Cascade,
                },
            ],
            UniqueConstraints = includeUnique
                ?
                [
                    new UniqueConstraintDefinition
                    {
                        Name = "uq_agent_configs_tenant_name",
                        Columns = ["tenant_id", "name"],
                    },
                ]
                : [],
        };
        return new SchemaDefinition { Name = "issue55", Tables = [tenants, agentConfigs] };
    }
}
