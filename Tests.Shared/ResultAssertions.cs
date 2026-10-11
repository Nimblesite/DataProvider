namespace Tests.Shared;

// Implements [CI-TEST-SHARED-RESULT].
internal static class ResultAssertions
{
    internal static TExpected Expected<TExpected>(object? actual, string? message = null)
    {
        Xunit.Assert.True(condition: actual is TExpected, userMessage: message);
        return Xunit.Assert.IsAssignableFrom<TExpected>(@object: actual);
    }

    internal static Outcome.Result<TValue, TError>.Ok<TValue, TError> Ok<TValue, TError>(
        Outcome.Result<TValue, TError> actual,
        string? message = null
    ) =>
        Expected<Outcome.Result<TValue, TError>.Ok<TValue, TError>>(
            actual: actual,
            message: message
        );
    internal static IReadOnlyList<T> Rows<T>(
        Func<Outcome.Result<IReadOnlyList<T>, Nimblesite.Sql.Model.SqlError>> read,
        bool exactType = false
    )
    {
        var result = read();
        return exactType
            ? Xunit.Assert.IsType<Outcome.Result<IReadOnlyList<T>, Nimblesite.Sql.Model.SqlError>.Ok<IReadOnlyList<T>, Nimblesite.Sql.Model.SqlError>>(@object: result).Value
            : Ok(actual: result).Value;
    }
}
