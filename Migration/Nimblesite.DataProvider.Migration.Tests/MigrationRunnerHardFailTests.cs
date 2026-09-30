using System.Collections.Immutable;
using MigrationRunnerResult = Outcome.Result<
    bool,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial record MigrationPlatformRegressionTests
{
    // Implements [MIG-RUNNER-HARD-FAIL] and [MIG-TEST-CROSS-PLATFORM].
    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("postgres", false)]
    [InlineData("sqlserver", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgres", true)]
    [InlineData("sqlserver", true)]
    public async Task Runner_FailedOperationReturnsErrorAndHonorsContinueOnError(
        string provider,
        bool continueOnError
    )
    {
        await WithTargetAsync(provider, target => AssertRunnerHardFail(target, continueOnError))
            .ConfigureAwait(true);
    }

    private static void AssertRunnerHardFail(MigrationTarget target, bool continueOnError)
    {
        var baseline = new SchemaDefinition
        {
            Name = "runner_hard_fail",
            Tables = [ParentTable("runner_baseline")],
        };
        Migrate(target, baseline);
        AssertBaseline(target);
        var before = new CreateTableOperation(ParentTable("runner_before"));
        var broken = new CreateTableOperation(ParentTable("runner_broken"));
        var after = new CreateTableOperation(ParentTable("runner_after"));
        var generated = ImmutableArray.CreateBuilder<SchemaOperation>();
        var result = ApplyFailureScenario(
            target,
            continueOnError,
            before,
            broken,
            after,
            generated
        );
        AssertRunnerFailure(result, continueOnError, before, broken, after, generated);
        AssertBaseline(target);
        AssertTableExists(target, "runner_before", expected: false);
        AssertTableExists(target, "runner_broken", expected: false);
        AssertTableExists(target, "runner_after", expected: false);
        Migrate(target, baseline);
        AssertBaseline(target);
    }

    private static MigrationRunnerResult ApplyFailureScenario(
        MigrationTarget target,
        bool continueOnError,
        CreateTableOperation before,
        CreateTableOperation broken,
        CreateTableOperation after,
        ImmutableArray<SchemaOperation>.Builder generated
    )
    {
        string GenerateDdl(SchemaOperation operation)
        {
            generated.Add(operation);
            return operation == broken ? "SELECT * FROM missing_runner_table" : "SELECT 1";
        }
        return MigrationRunner.Apply(
            connection: target.Connection,
            operations: [before, broken, after],
            generateDdl: GenerateDdl,
            options: new MigrationOptions
            {
                ContinueOnError = continueOnError,
                UseTransaction = false,
            }
        );
    }

    private static void AssertRunnerFailure(
        MigrationRunnerResult result,
        bool continueOnError,
        SchemaOperation before,
        SchemaOperation broken,
        SchemaOperation after,
        ImmutableArray<SchemaOperation>.Builder generated
    )
    {
        var failure = Assert.IsType<MigrationApplyResultError>(result);
        Assert.False(string.IsNullOrWhiteSpace(failure.Value.Message));
        Assert.Contains(
            "missing_runner_table",
            failure.Value.Message,
            StringComparison.OrdinalIgnoreCase
        );
        Assert.Equal(continueOnError ? 3 : 2, generated.Count);
        Assert.Same(before, generated[0]);
        Assert.Same(broken, generated[1]);
        if (continueOnError)
        {
            Assert.Same(after, generated[2]);
        }
    }

    private static void AssertBaseline(MigrationTarget target)
    {
        AssertTableExists(target, "runner_baseline", expected: true);
        AssertColumnExists(target, "runner_baseline", "id", expected: true);
        Assert.False(ColumnIsNullable(target, "runner_baseline", "id"));
    }
}
