namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// Implements [MIG-RUNNER-HARD-FAIL]: when any operation fails, MigrationRunner.Apply
/// must return an Error result, even when ContinueOnError=true. The purpose of
/// ContinueOnError is to keep applying remaining operations after a failure for
/// diagnostic visibility; it must NEVER cause the runner to claim success when
/// operations were missed. Closes the spirit of issues #53 and #55: the migrator
/// must hard-fail when schema migrations are missed.
/// </summary>
public sealed class MigrationRunnerHardFailTests
{
    [Fact]
    public void Apply_WithContinueOnError_FailedOperationCausesErrorResult()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"hardfail_{Guid.NewGuid()}.db");
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        try
        {
            var goodOp = new CreateTableOperation(
                new TableDefinition
                {
                    Schema = "main",
                    Name = "good_table",
                    Columns =
                    [
                        new ColumnDefinition
                        {
                            Name = "id",
                            Type = PortableTypes.BigInt,
                            IsNullable = false,
                        },
                    ],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                }
            );

            var brokenOp = new CreateTableOperation(
                new TableDefinition
                {
                    Schema = "main",
                    Name = "broken_table",
                    Columns =
                    [
                        new ColumnDefinition
                        {
                            Name = "id",
                            Type = PortableTypes.BigInt,
                            IsNullable = false,
                        },
                    ],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                }
            );

            string GenerateDdl(SchemaOperation op) =>
                op == brokenOp
                    ? "INSERT INTO no_such_table (col) VALUES ('forced failure')"
                    : SqliteDdlGenerator.Generate(op);

            var result = MigrationRunner.Apply(
                connection: connection,
                operations: [goodOp, brokenOp],
                generateDdl: GenerateDdl,
                options: new MigrationOptions { ContinueOnError = true, UseTransaction = false }
            );

            Assert.True(
                condition: result is MigrationApplyResultError,
                userMessage: "Apply must return Error when any operation fails, even with "
                    + "ContinueOnError=true. The runner reported success while a migration "
                    + "was missed."
            );
        }
        finally
        {
            connection.Close();
            if (File.Exists(dbPath))
            {
                try
                {
                    File.Delete(dbPath);
                }
                catch
                {
                    /* file may be locked */
                }
            }
        }
    }
}
