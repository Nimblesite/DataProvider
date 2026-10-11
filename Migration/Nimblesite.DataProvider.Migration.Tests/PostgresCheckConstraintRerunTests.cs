// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

[Collection(PostgresTestSuite.Name)]
public sealed class PostgresCheckConstraintRerunTests(PostgresContainerFixture fixture)
{
    private static readonly ILogger Logger = NullLogger.Instance;

    [Fact]
    public void ExistingOptionalChecks_AdditiveAndDestructiveRerunsAreEmpty()
    {
        using var connection = fixture.CreateDatabase("optional_check_reruns");
        var schema = Schema();
        PostgresTestDb.Apply(
            connection,
            PostgresTestDb.Calculate(PostgresTestDb.Inspect(connection, Logger), schema, Logger),
            Logger
        );

        var additive = PostgresTestDb.Calculate(
            PostgresTestDb.Inspect(connection, Logger),
            schema,
            Logger
        );
        var destructive = PostgresTestDb.Calculate(
            PostgresTestDb.Inspect(connection, Logger),
            schema,
            Logger,
            allowDestructive: true
        );

        Assert.Empty(additive);
        Assert.Empty(destructive);
    }

    private static SchemaDefinition Schema() =>
        SchemaTestData.Schema(
            name: "optional_check_reruns",
            tables:
            [
                SchemaTestData.Table(
                    name: "agent_configs",
                    columns:
                    [
                        SchemaTestData.Column(
                            name: "id",
                            type: PortableTypes.Uuid,
                            isNullable: false
                        ),
                        Checked(
                            "workspace_host_kind",
                            "workspace_host_kind IS NULL OR workspace_host_kind IN ('fly','docker')"
                        ),
                        Checked(
                            "workspace_size",
                            "workspace_size IS NULL OR workspace_size IN ('small','medium','large')"
                        ),
                        Checked(
                            "provisioning_bundle_sha256",
                            "provisioning_bundle_sha256 IS NULL OR provisioning_bundle_sha256 ~ '^[0-9a-f]{64}$'"
                        ),
                        SchemaTestData.Column(
                            name: "workspace_dev_access",
                            type: PortableTypes.Boolean,
                            checkConstraint: "workspace_dev_access = false"
                        ),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] }
                ),
            ]
        );

    private static ColumnDefinition Checked(string name, string expression) =>
        SchemaTestData.Column(name: name, type: PortableTypes.Text, checkConstraint: expression);
}
