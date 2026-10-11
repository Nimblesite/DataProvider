using IntegrityOk = Outcome.Result<
    System.Collections.Immutable.ImmutableArray<string>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<
    System.Collections.Immutable.ImmutableArray<string>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;

namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [RLS-DIFF]. Issue #119: view qualification must not become policy drift.
[Collection(Postgres15TestSuite.Name)]
public sealed partial class PostgresPolicyCatalogCliTests(Postgres15ContainerFixture fixture)
{
    [Theory]
    [MemberData(
        nameof(ColumnPredicates_CreateReapplyAndChangeWithoutFalseIntegrityDriftInlineCases)
    )]
    public void ColumnPredicates_CreateReapplyAndChangeWithoutFalseIntegrityDrift(
        string? initialUsing,
        string? changedUsing,
        string? initialCheck,
        string? changedCheck,
        bool sql
    )
    {
        using var connection = fixture.CreateDatabase(namePrefix: "policy_column_repro");
        var initial = PolicyYaml(predicate: initialUsing, check: initialCheck, sql: sql);
        var changed = PolicyYaml(predicate: changedUsing, check: changedCheck, sql: sql);
        AssertSuccessfulMigration(
            connection: connection,
            yaml: initial,
            expected: "CreateRlsPolicyOperation"
        );
        var before = ReadPolicy(connection: connection);
        AssertSuccessfulMigration(
            connection: connection,
            yaml: initial,
            expected: "no operations needed"
        );
        AssertSuccessfulMigration(
            connection: connection,
            yaml: changed,
            expected: "AlterRlsPolicyOperation"
        );
        var after = ReadPolicy(connection: connection);
        Assert.Equal(
            expected: initialUsing == changedUsing,
            actual: before.UsingSql == after.UsingSql
        );
        Assert.Equal(
            expected: initialCheck == changedCheck,
            actual: before.WithCheckSql == after.WithCheckSql
        );
        AssertSuccessfulMigration(
            connection: connection,
            yaml: changed,
            expected: "no operations needed"
        );
    }

    [Fact]
    public void RealPredicateDrift_FailsIntegrityAndRollsBackPolicyAlteration()
    {
        using var connection = fixture.CreateDatabase(namePrefix: "policy_drift_rollback");
        var yaml = PolicyYaml(predicate: "id = 1", check: null, sql: false);
        AssertSuccessfulMigration(
            connection: connection,
            yaml: yaml,
            expected: "CreateRlsPolicyOperation"
        );
        var desired = SchemaYamlSerializer.FromYaml(yaml: yaml);
        var before = ReadPolicy(connection: connection);
        var result = MigrationRunner.Apply(
            connection: connection,
            operations:
            [
                Dataset_RealPredicateDrift_FailsIntegrityAndRollsBackPolicyAlteration_1(
                    before: before
                ),
            ],
            generateDdl: PostgresDdlGenerator.Generate,
            options: MigrationOptions.Default,
            verifyBeforeCommit: () => RejectDrift(connection: connection, desired: desired)
        );
        var error = Assert.IsType<MigrationApplyResultError>(@object: result);
        Assert.Equal(
            expected: "Schema integrity verification failed before commit",
            actual: error.Value.Message
        );
        Assert.Equivalent(
            expected: before,
            actual: ReadPolicy(connection: connection),
            strict: true
        );
    }

    private static bool RejectDrift(NpgsqlConnection connection, SchemaDefinition desired)
    {
        var live = Assert
            .IsType<SchemaResultOk>(PostgresPolicyCatalogNormalizer.Inspect(connection: connection))
            .Value;
        var normalized = Assert
            .IsType<SchemaResultOk>(
                PostgresPolicyCatalogNormalizer.Normalize(
                    connection: connection,
                    live: live,
                    desired: desired
                )
            )
            .Value;
        var mismatches = Assert
            .IsType<IntegrityOk>(SchemaIntegrityVerifier.Verify(live: live, desired: normalized))
            .Value;
        Assert.Contains(
            expected: "public.items: policy item_visible USING predicate drifted",
            collection: mismatches
        );
        return mismatches.IsEmpty;
    }

    private static void AssertSuccessfulMigration(
        NpgsqlConnection connection,
        string yaml,
        string expected
    )
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path: path, contents: yaml);
            var result = MigrationCliConsole.Migrate(
                schemaPath: path,
                provider: "postgres",
                output: connection.ConnectionString
            );
            Assert.True(condition: result.ExitCode == 0, userMessage: result.Output);
            Assert.Contains(
                expectedSubstring: expected,
                actualString: result.Output,
                comparisonType: StringComparison.Ordinal
            );
        }
        finally
        {
            File.Delete(path: path);
        }
    }

    private static RlsPolicyDefinition ReadPolicy(NpgsqlConnection connection) =>
        Assert.Single(
            Assert
                .Single(
                    Assert
                        .IsType<SchemaResultOk>(
                            PostgresSchemaInspector.Inspect(connection: connection)
                        )
                        .Value.Tables
                )
                .RowLevelSecurity?.Policies
                ?? []
        );

    private static string PolicyYaml(string? predicate, string? check, bool sql) =>
        SqlData_PolicyYaml_1(check: check, predicate: predicate, sql: sql);
}
