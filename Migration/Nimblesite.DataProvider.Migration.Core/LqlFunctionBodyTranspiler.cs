using Outcome;
using StringError = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Error<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;
using StringOk = Outcome.Result<string, Nimblesite.DataProvider.Migration.Core.MigrationError>.Ok<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;

namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Transpiles LQL scalar expressions into PostgreSQL SQL-language function bodies.
/// </summary>
public static class LqlFunctionBodyTranspiler
{
    /// <summary>
    /// Translates a PostgreSQL function <c>bodyLql</c> expression to a SQL function body.
    /// </summary>
    /// <param name="bodyLql">LQL scalar expression, optionally prefixed with <c>SELECT</c>.</param>
    /// <param name="functionName">Function name used in diagnostic messages.</param>
    /// <returns>A SQL-language function body beginning with <c>SELECT</c>.</returns>
    public static Result<string, MigrationError> TranslatePostgresBody(
        string bodyLql,
        string functionName
    )
    {
        var expression = StripSelectPrefix(StripTrailingSemicolon(bodyLql.Trim()));
        if (string.IsNullOrWhiteSpace(expression))
        {
            return new StringError(
                MigrationError.RlsLqlParse(functionName, "function bodyLql is empty")
            );
        }

        var result = RlsPredicateTranspiler.Translate(
            expression,
            RlsPlatform.Postgres,
            functionName
        );
        return result switch
        {
            StringOk ok => new StringOk($"SELECT {ok.Value.Trim()}"),
            StringError err => new StringError(err.Value),
        };
    }

    private static string StripTrailingSemicolon(string value) =>
        value.EndsWith(';') ? value[..^1].TrimEnd() : value;

    private static string StripSelectPrefix(string value)
    {
        const string select = "select";
        if (!value.StartsWith(select, StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        return value.Length == select.Length || char.IsWhiteSpace(value[select.Length])
            ? value[select.Length..].TrimStart()
            : value;
    }
}
