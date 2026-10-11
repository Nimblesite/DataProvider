namespace Tests.Shared;

// Implements [CI-TEST-SHARED-RESULT].
internal static class ResultAssertions
{
    internal static Outcome.Result<TValue, TError>.Ok<TValue, TError> Ok<TValue, TError>(
        Func<Outcome.Result<TValue, TError>> read,
        string? message = null
    ) => Ok(actual: read(), message: message);

    internal static Outcome.Result<TValue, TError>.Error<TValue, TError> Error<TValue, TError>(
        Func<Outcome.Result<TValue, TError>> read,
        string? message = null
    ) => Error(actual: read(), message: message);

    // Implements [CI-TEST-SHARED-RESULT].
    internal static Outcome.Result<TValue, TError>.Ok<TValue, TError> ExactOk<TValue, TError>(
        Outcome.Result<TValue, TError>? actual
    ) => Xunit.Assert.IsType<Outcome.Result<TValue, TError>.Ok<TValue, TError>>(@object: actual);

    // Implements [CI-TEST-SHARED-RESULT].
    internal static void VerifyOk<TValue, TError>(
        Outcome.Result<TValue, TError> actual,
        Action<Outcome.Result<TValue, TError>.Ok<TValue, TError>> verify
    )
    {
        Xunit.Assert.True(condition: actual is Outcome.Result<TValue, TError>.Ok<TValue, TError>);
        if (actual is Outcome.Result<TValue, TError>.Ok<TValue, TError> ok)
            verify(obj: ok);
    }

    // Implements [CI-TEST-SHARED-RESULT].
    internal static void EqualValue<TValue, TError>(
        TValue expected,
        Outcome.Result<TValue, TError>? actual
    ) => Xunit.Assert.Equal(expected: expected, actual: ExactOk(actual: actual).Value);

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

    internal static Outcome.Result<TValue, TError>.Error<TValue, TError> Error<TValue, TError>(
        Outcome.Result<TValue, TError> actual,
        string? message = null
    ) =>
        Expected<Outcome.Result<TValue, TError>.Error<TValue, TError>>(
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
            ? Xunit
                .Assert.IsType<Outcome.Result<IReadOnlyList<T>, Nimblesite.Sql.Model.SqlError>.Ok<
                    IReadOnlyList<T>,
                    Nimblesite.Sql.Model.SqlError
                >>(@object: result)
                .Value
            : Ok(actual: result).Value;
    }

    // Implements [CI-TEST-SHARED-RESULT].
    internal static void Types<T>(System.Collections.Immutable.ImmutableArray<object?> values)
    {
        foreach (var value in values)
            Xunit.Assert.IsType<T>(@object: value);
    }
}
