// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

[Collection(PostgresTestSuite.Name)]
public sealed partial class PostgresCheckConstraintRerunTests(PostgresContainerFixture fixture)
{
    private static readonly ILogger Logger = NullLogger.Instance;

    [Fact]
    public void ExistingOptionalChecks_AdditiveAndDestructiveRerunsAreEmpty()
    {
        using var connection = fixture.CreateDatabase("optional_check_reruns");
        var schema =
            global::Nimblesite.DataProvider.Migration.Tests.PostgresDropConstraintBackedIndexTests.VerifyReusableWorkflow30(
                input0: Schema,
                input1: connection,
                input2: Logger
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

    private static ColumnDefinition Checked(string name, string expression) =>
        SchemaTestData.Column(name: name, type: PortableTypes.Text, checkConstraint: expression);
}
