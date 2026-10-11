namespace Tests.Shared;

// Implements [CI-TEST-SHARED-SCENARIOS]: fixed metadata callback used by the
// custom and extended code-generation tests.
internal static class CodeGenerationTestDoubles
{
    internal static Task<Outcome.Result<
        IReadOnlyList<DatabaseColumn>,
        Nimblesite.Sql.Model.SqlError
    >> ColumnMetadata(IReadOnlyList<DatabaseColumn> columns) =>
        Task.FromResult<Outcome.Result<
            IReadOnlyList<DatabaseColumn>,
            Nimblesite.Sql.Model.SqlError
        >>(
            new Outcome.Result<IReadOnlyList<DatabaseColumn>, Nimblesite.Sql.Model.SqlError>.Ok<
                IReadOnlyList<DatabaseColumn>,
                Nimblesite.Sql.Model.SqlError
            >(columns)
        );
}
