namespace Tests.Shared;

// Implements [CI-TEST-SHARED-CASE-MATRIX]: shared runners and result-shape
// assertions for the connection/transaction extension case matrices. The
// adapters are typed Func delegates so one workflow runs against either the
// native connection or the native transaction receiver.
internal static class ExtensionTestCases
{
    // Guards a named case label, then runs the case body.
    internal static void RunCase(string caseName, Action caseBody)
    {
        Xunit.Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        caseBody();
    }

    // Expects an Error result from a query-style operation.
    internal static void ExpectQueryError<T>(
        string sql,
        Func<string, Outcome.Result<IReadOnlyList<T>, Nimblesite.Sql.Model.SqlError>> query
    ) => ResultAssertions.Error(actual: query(arg: sql));

    // Expects an Error result from an execute-style operation.
    internal static void ExpectExecuteError(
        string sql,
        Func<string, Outcome.Result<int, Nimblesite.Sql.Model.SqlError>> execute
    ) => ResultAssertions.Error(actual: execute(arg: sql));

    // Expects that a scalar-style operation does not return an Ok result.
    internal static void ExpectScalarNotOk<T>(
        string sql,
        Func<string, Outcome.Result<T, Nimblesite.Sql.Model.SqlError>> scalar
    ) =>
        Xunit.Assert.False(
            condition: scalar(arg: sql)
                is Outcome.Result<T, Nimblesite.Sql.Model.SqlError>.Ok<
                    T,
                    Nimblesite.Sql.Model.SqlError
                >
        );

    // Expects an Ok result whose value collection is empty.
    internal static void ExpectOkEmpty<TItem, TError>(
        Outcome.Result<IReadOnlyList<TItem>, TError> actual
    ) =>
        ResultAssertions.VerifyOk(
            actual: actual,
            verify: ok => Xunit.Assert.Empty(collection: ok.Value)
        );
}
