using System.Globalization;
using System.Text.RegularExpressions;

namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Translates LQL default expressions to platform-specific SQL.
/// Provides consistent behavior across PostgreSQL and SQLite.
/// </summary>
public static partial class LqlDefaultTranslator
{
    /// <summary>
    /// Translates an LQL default expression to PostgreSQL SQL.
    /// </summary>
    /// <param name="lqlExpression">The LQL expression (e.g., "now()", "gen_uuid()").</param>
    /// <returns>PostgreSQL-specific SQL expression.</returns>
    public static string ToPostgres(string lqlExpression)
    {
        ArgumentNullException.ThrowIfNull(lqlExpression);

        var normalized = lqlExpression.Trim().ToLowerInvariant();

        // Handle common LQL functions
        return normalized switch
        {
            // Timestamp functions
            "now()" => "CURRENT_TIMESTAMP",
            "current_timestamp()" => "CURRENT_TIMESTAMP",
            "current_date()" => "CURRENT_DATE",
            "current_time()" => "CURRENT_TIME",

            // UUID generation
            "gen_uuid()" => "gen_random_uuid()",
            "uuid()" => "gen_random_uuid()",

            // Boolean literals
            "true" => "true",
            "false" => "false",

            // Numeric literals (pass through)
            var n when int.TryParse(n, out _) => n,
            var d when double.TryParse(d, CultureInfo.InvariantCulture, out _) => d,

            // String literals (already quoted with single quotes)
            var s when s.StartsWith('\'') && s.EndsWith('\'') => s,

            // Function calls (lower, upper, coalesce, etc.)
            _ => TranslateFunctionCall(lqlExpression, ToPostgresFunction),
        };
    }

    /// <summary>
    /// Translates an LQL default expression to SQLite SQL.
    /// </summary>
    /// <param name="lqlExpression">The LQL expression (e.g., "now()", "gen_uuid()").</param>
    /// <returns>SQLite-specific SQL expression.</returns>
    public static string ToSqlite(string lqlExpression)
    {
        ArgumentNullException.ThrowIfNull(lqlExpression);

        var normalized = lqlExpression.Trim().ToLowerInvariant();

        // Handle common LQL functions
        return normalized switch
        {
            // Timestamp functions - SQLite uses datetime/date/time functions
            "now()" => "(datetime('now'))",
            "current_timestamp()" => "CURRENT_TIMESTAMP",
            "current_date()" => "(date('now'))",
            "current_time()" => "(time('now'))",

            // UUID generation - SQLite needs manual UUID v4 construction
            "gen_uuid()" => UuidV4SqliteExpression,
            "uuid()" => UuidV4SqliteExpression,

            // Boolean literals - SQLite uses 0/1
            "true" => "1",
            "false" => "0",

            // Numeric literals (pass through)
            var n when int.TryParse(n, out _) => n,
            var d when double.TryParse(d, CultureInfo.InvariantCulture, out _) => d,

            // String literals (already quoted with single quotes)
            var s when s.StartsWith('\'') && s.EndsWith('\'') => s,

            // Function calls
            _ => TranslateFunctionCall(lqlExpression, ToSqliteFunction),
        };
    }

    // SQLite UUID v4 generation expression
    private const string UuidV4SqliteExpression =
        "(lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-4' || "
        + "substr(lower(hex(randomblob(2))),2) || '-' || "
        + "substr('89ab',abs(random()) % 4 + 1, 1) || substr(lower(hex(randomblob(2))),2) || '-' || "
        + "lower(hex(randomblob(6))))";

    private static string TranslateFunctionCall(
        string expression,
        Func<string, string[], string> functionTranslator
    )
    {
        // Match function calls like: lower(name), coalesce(a, b), substring(text, 1, 5)
        var match = FunctionCallRegex().Match(expression);
        if (!match.Success)
        {
            // Not a function call, return as-is (could be a literal or column reference)
            return expression;
        }

        var functionName = match.Groups["func"].Value.ToLowerInvariant();
        var argsString = match.Groups["args"].Value;

        // Parse arguments (simple split, doesn't handle nested functions with commas)
        var args = string.IsNullOrWhiteSpace(argsString)
            ? []
            : argsString.Split(',').Select(a => a.Trim()).ToArray();

        return functionTranslator(functionName, args);
    }

    private static string ToPostgresFunction(string functionName, string[] args) =>
        FormatDefaultFunction(functionName: functionName, args: args, sqlite: false);

    private static string ToSqliteFunction(string functionName, string[] args) =>
        FormatDefaultFunction(functionName: functionName, args: args, sqlite: true);

    [GeneratedRegex(@"^(?<func>\w+)\s*\((?<args>.*)\)$", RegexOptions.Singleline)]
    private static partial Regex FunctionCallRegex();

    // Implements [MIG-DEFAULT-SHARED-FUNCTION-FORMATTING].
    private static string FormatDefaultFunction(string functionName, string[] args, bool sqlite) =>
        functionName switch
        {
            "coalesce" => FormatCall(functionName: sqlite ? "coalesce" : "COALESCE", args: args),
            "substring" => args.Length >= 3
                ? (
                    sqlite
                        ? $"substr({args[0]}, {args[1]}, {args[2]})"
                        : $"substring({args[0]} from {args[1]} for {args[2]})"
                )
                : FormatCall(functionName: sqlite ? "substr" : "substring", args: args),
            "concat" when sqlite => args.Length > 0 ? string.Join(" || ", args) : "''",
            _ => FormatCall(functionName: functionName, args: args),
        };

    private static string FormatCall(string functionName, string[] args) =>
        $"{functionName}({string.Join(", ", args)})";
}
