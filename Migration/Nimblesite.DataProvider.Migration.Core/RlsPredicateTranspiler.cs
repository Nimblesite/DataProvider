using System.Text;
using Nimblesite.Lql.Core;
using Nimblesite.Lql.Postgres;
using Nimblesite.Lql.SQLite;
using Nimblesite.Lql.SqlServer;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Migration.Core;

// Implements [RLS-CORE-LQL] from docs/specs/rls-spec.md.

/// <summary>
/// Target platform for an RLS predicate transpilation.
/// </summary>
public enum RlsPlatform
{
    /// <summary>PostgreSQL native row-level security.</summary>
    Postgres,

    /// <summary>SQLite (trigger-based emulation).</summary>
    Sqlite,

    /// <summary>SQL Server (deferred).</summary>
    SqlServer,
}

/// <summary>
/// Transpiles LQL row-level security predicates to platform-specific SQL.
/// Handles the <c>current_user_id()</c> built-in by per-platform substitution
/// (Postgres: <c>current_setting</c>, SQLite: <c>__rls_context</c> lookup,
/// SQL Server: <c>SESSION_CONTEXT</c>) and delegates <c>exists(pipeline)</c>
/// subquery wrappers to the LQL pipeline transpiler.
/// </summary>
public static class RlsPredicateTranspiler
{
    /// <summary>
    /// Sentinel placeholder used to mark <c>current_user_id()</c> calls
    /// inside LQL pipelines so they survive transpilation and can be
    /// substituted afterward with the platform-specific session-context
    /// expression.
    /// </summary>
    internal const string CurrentUserIdSentinel = "__RLS_CURRENT_USER_ID__";

    /// <summary>
    /// Translate an LQL predicate to platform-specific SQL.
    /// </summary>
    /// <param name="lql">LQL predicate. Either a simple expression like
    /// <c>OwnerId = current_user_id()</c> or an <c>exists(pipeline)</c>
    /// wrapper containing a pipeline.</param>
    /// <param name="platform">Target platform.</param>
    /// <param name="policyName">Policy name -- used for error messages.</param>
    public static Result<string, MigrationError> Translate(
        string lql,
        RlsPlatform platform,
        string policyName
    )
    {
        if (string.IsNullOrWhiteSpace(lql))
        {
            return new Result<string, MigrationError>.Error<string, MigrationError>(
                MigrationError.RlsEmptyPredicate(policyName)
            );
        }

        var trimmed = lql.Trim();
        return TryParseExistsWrapper(trimmed, out var inner)
            ? TranslateExistsSubquery(inner, platform, policyName)
            : TranslateSimplePredicate(trimmed, platform, policyName);
    }

    /// <summary>
    /// Returns the platform-specific expression that yields the current
    /// user identifier.
    /// </summary>
    public static string CurrentUserIdExpression(RlsPlatform platform) =>
        platform switch
        {
            RlsPlatform.Postgres => "current_setting('rls.current_user_id', true)",
            RlsPlatform.Sqlite => "(SELECT current_user_id FROM [__rls_context] LIMIT 1)",
            RlsPlatform.SqlServer => "CAST(SESSION_CONTEXT(N'current_user_id') AS NVARCHAR(450))",
            _ => throw new NotSupportedException($"Unknown platform: {platform}"),
        };

    private static bool TryParseExistsWrapper(string trimmed, out string inner)
    {
        // Match exists( ... ) at top level. Tolerant of whitespace/newlines
        // between "exists" and "(", and balances parens to find the closing.
        inner = string.Empty;
        const string keyword = "exists";
        if (!trimmed.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var i = keyword.Length;
        while (i < trimmed.Length && char.IsWhiteSpace(trimmed[i]))
        {
            i++;
        }
        if (i >= trimmed.Length || trimmed[i] != '(')
        {
            return false;
        }
        var openIdx = i;
        var depth = 1;
        i++;
        while (i < trimmed.Length && depth > 0)
        {
            switch (trimmed[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    if (depth == 0)
                    {
                        if (i != trimmed.Length - 1)
                        {
                            return false;
                        }
                        inner = trimmed[(openIdx + 1)..i];
                        return true;
                    }
                    break;
            }
            i++;
        }
        return false;
    }

    private static Result<string, MigrationError> TranslateExistsSubquery(
        string innerLql,
        RlsPlatform platform,
        string policyName
    )
    {
        // Substitute current_user_id() with a sentinel string literal that
        // survives LQL transpilation, then transpile the pipeline, then
        // replace the sentinel with the platform-specific expression.
        var withSentinel = SubstituteCurrentUserIdWithSentinel(innerLql);
        var withSettings = RlsCurrentSettingRewriter.ReplaceCallsForPipeline(
            withSentinel,
            platform,
            policyName
        );
        if (
            withSettings
            is Result<RlsCurrentSettingRewrite, MigrationError>.Error<
                RlsCurrentSettingRewrite,
                MigrationError
            > settingsErr
        )
        {
            return new Result<string, MigrationError>.Error<string, MigrationError>(
                settingsErr.Value
            );
        }

        if (
            withSettings
            is not Result<RlsCurrentSettingRewrite, MigrationError>.Ok<
                RlsCurrentSettingRewrite,
                MigrationError
            > settingsOk
        )
        {
            return new Result<string, MigrationError>.Error<string, MigrationError>(
                MigrationError.RlsLqlParse(policyName, "unknown current_setting rewrite failure")
            );
        }

        var statementResult = LqlStatementConverter.ToStatement(settingsOk.Value.Text);
        if (statementResult is Result<LqlStatement, SqlError>.Error<LqlStatement, SqlError> sErr)
        {
            return new Result<string, MigrationError>.Error<string, MigrationError>(
                MigrationError.RlsLqlParse(policyName, sErr.Value.Message)
            );
        }

        if (statementResult is not Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError> sOk)
        {
            return new Result<string, MigrationError>.Error<string, MigrationError>(
                MigrationError.RlsLqlParse(policyName, "unknown LQL parse failure")
            );
        }

        var sqlResult = platform switch
        {
            RlsPlatform.Postgres => sOk.Value.ToPostgreSql(),
            RlsPlatform.Sqlite => sOk.Value.ToSQLite(),
            RlsPlatform.SqlServer => sOk.Value.ToSqlServer(),
            _ => throw new NotSupportedException($"Unknown platform: {platform}"),
        };

        if (sqlResult is Result<string, SqlError>.Error<string, SqlError> tErr)
        {
            return new Result<string, MigrationError>.Error<string, MigrationError>(
                MigrationError.RlsLqlTranspile(policyName, tErr.Value.Message)
            );
        }

        if (sqlResult is not Result<string, SqlError>.Ok<string, SqlError> tOk)
        {
            return new Result<string, MigrationError>.Error<string, MigrationError>(
                MigrationError.RlsLqlTranspile(policyName, "unknown LQL transpile failure")
            );
        }

        var sql = ReplaceSentinelInSql(tOk.Value, platform);
        sql = RlsCurrentSettingRewriter.RestoreSentinels(sql, settingsOk.Value.Replacements);
        return new Result<string, MigrationError>.Ok<string, MigrationError>($"EXISTS ({sql})");
    }

    private static Result<string, MigrationError> TranslateSimplePredicate(
        string predicate,
        RlsPlatform platform,
        string policyName
    )
    {
        var withSettings = RlsCurrentSettingRewriter.ReplaceCallsForSimplePredicate(
            predicate,
            platform,
            policyName
        );

        if (
            withSettings is Result<string, MigrationError>.Error<string, MigrationError> settingsErr
        )
        {
            return new Result<string, MigrationError>.Error<string, MigrationError>(
                settingsErr.Value
            );
        }

        if (
            withSettings is not Result<string, MigrationError>.Ok<string, MigrationError> settingsOk
        )
        {
            return new Result<string, MigrationError>.Error<string, MigrationError>(
                MigrationError.RlsLqlParse(policyName, "unknown current_setting rewrite failure")
            );
        }

        // Replace current_user_id() literal with platform expression after
        // current_setting() LQL calls have been rewritten. The Postgres
        // current_user_id() expansion itself uses current_setting(..., true).
        var withSession = ReplaceCurrentUserIdLiteral(settingsOk.Value, platform);
        var translated = platform switch
        {
            RlsPlatform.Postgres => QuoteSimpleIdentifiers(withSession, '"', '"'),
            RlsPlatform.Sqlite => QuoteSimpleIdentifiers(withSession, '[', ']'),
            RlsPlatform.SqlServer => QuoteSimpleIdentifiers(withSession, '[', ']'),
            _ => withSession,
        };

        return new Result<string, MigrationError>.Ok<string, MigrationError>(translated);
    }

    private static string SubstituteCurrentUserIdWithSentinel(string lql) =>
        ReplaceFunctionCall(lql, "current_user_id", $"'{CurrentUserIdSentinel}'");

    private static string ReplaceCurrentUserIdLiteral(string predicate, RlsPlatform platform) =>
        ReplaceFunctionCall(predicate, "current_user_id", CurrentUserIdExpression(platform));

    private static string ReplaceSentinelInSql(string sql, RlsPlatform platform)
    {
        var expr = CurrentUserIdExpression(platform);
        return sql.Replace($"'{CurrentUserIdSentinel}'", expr, StringComparison.Ordinal);
    }

    private static string ReplaceFunctionCall(string source, string fnName, string replacement)
    {
        // Replace `fnName ( )` (with optional whitespace) by replacement.
        // Identifier-boundary aware; skips occurrences inside string literals.
        var sb = new StringBuilder(source.Length + replacement.Length);
        var i = 0;
        while (i < source.Length)
        {
            var c = source[i];
            if (c == '\'')
            {
                // copy verbatim through closing quote (handles '' escape)
                sb.Append(c);
                i++;
                while (i < source.Length)
                {
                    sb.Append(source[i]);
                    if (source[i] == '\'')
                    {
                        if (i + 1 < source.Length && source[i + 1] == '\'')
                        {
                            sb.Append(source[i + 1]);
                            i += 2;
                            continue;
                        }
                        i++;
                        break;
                    }
                    i++;
                }
                continue;
            }
            if (
                (char.IsLetter(c) || c == '_')
                && (i == 0 || (!char.IsLetterOrDigit(source[i - 1]) && source[i - 1] != '_'))
            )
            {
                var start = i;
                while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_'))
                {
                    i++;
                }
                var word = source[start..i];
                var savedI = i;
                while (i < source.Length && char.IsWhiteSpace(source[i]))
                {
                    i++;
                }
                if (
                    word.Equals(fnName, StringComparison.Ordinal)
                    && i < source.Length
                    && source[i] == '('
                )
                {
                    i++;
                    while (i < source.Length && char.IsWhiteSpace(source[i]))
                    {
                        i++;
                    }
                    if (i < source.Length && source[i] == ')')
                    {
                        sb.Append(replacement);
                        i++;
                        continue;
                    }
                    // Not the empty-arg form; emit verbatim and rewind.
                    sb.Append(word);
                    i = savedI;
                    continue;
                }
                sb.Append(word);
                i = savedI;
                continue;
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    private static string QuoteSimpleIdentifiers(string predicate, char open, char close)
    {
        // Conservative tokenizer: any bare identifier (letters/_/digits, not
        // starting with a digit) that is not a SQL keyword and not preceded
        // by `.` and not followed by `(` (function call) gets quoted with
        // open/close. Skips contents of string literals and already-quoted
        // identifiers.
        var sb = new StringBuilder(predicate.Length + 16);
        var i = 0;
        while (i < predicate.Length)
        {
            var c = predicate[i];
            if (c == '\'')
            {
                sb.Append(c);
                i++;
                while (i < predicate.Length)
                {
                    sb.Append(predicate[i]);
                    if (predicate[i] == '\'')
                    {
                        if (i + 1 < predicate.Length && predicate[i + 1] == '\'')
                        {
                            sb.Append(predicate[i + 1]);
                            i += 2;
                            continue;
                        }
                        i++;
                        break;
                    }
                    i++;
                }
                continue;
            }
            if (c == open)
            {
                sb.Append(c);
                i++;
                while (i < predicate.Length)
                {
                    sb.Append(predicate[i]);
                    if (predicate[i] == close)
                    {
                        i++;
                        break;
                    }
                    i++;
                }
                continue;
            }
            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                i++;
                while (
                    i < predicate.Length
                    && (char.IsLetterOrDigit(predicate[i]) || predicate[i] == '_')
                )
                {
                    i++;
                }
                var word = predicate[start..i];

                var prev = start - 1;
                while (prev >= 0 && char.IsWhiteSpace(predicate[prev]))
                {
                    prev--;
                }
                var qualified = prev >= 0 && predicate[prev] == '.';
                // ::type cast — the word after `::` is a type name, do not quote.
                var afterCast = prev >= 1 && predicate[prev] == ':' && predicate[prev - 1] == ':';

                var nextIdx = i;
                while (nextIdx < predicate.Length && char.IsWhiteSpace(predicate[nextIdx]))
                {
                    nextIdx++;
                }
                var isCall = nextIdx < predicate.Length && predicate[nextIdx] == '(';

                if (qualified || afterCast || isCall || IsKeyword(word) || IsBuiltinExpr(word))
                {
                    sb.Append(word);
                }
                else
                {
                    sb.Append(open).Append(word).Append(close);
                }
                continue;
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    private static bool IsKeyword(string word)
    {
        var u = word.ToUpperInvariant();
        return u
            is "AND"
                or "OR"
                or "NOT"
                or "NULL"
                or "TRUE"
                or "FALSE"
                or "IS"
                or "IN"
                or "LIKE"
                or "BETWEEN"
                or "CASE"
                or "WHEN"
                or "THEN"
                or "ELSE"
                or "END"
                or "EXISTS"
                or "SELECT"
                or "FROM"
                or "WHERE"
                or "AS"
                or "ON";
    }

    // True if the word looks like part of an already-translated session-context
    // expression (e.g. "current_setting", "SELECT", "SESSION_CONTEXT", etc.).
    // These show up after current_user_id() substitution and must not be
    // quoted as column identifiers.
    private static bool IsBuiltinExpr(string word) =>
        word.Equals("current_setting", StringComparison.Ordinal)
        || word.Equals("current_user_id", StringComparison.Ordinal)
        || word.Equals("SESSION_CONTEXT", StringComparison.Ordinal)
        || word.Equals("CAST", StringComparison.Ordinal)
        || word.Equals("NVARCHAR", StringComparison.Ordinal)
        || word.Equals("LIMIT", StringComparison.Ordinal)
        || word.Equals("__rls_context", StringComparison.Ordinal);
}
