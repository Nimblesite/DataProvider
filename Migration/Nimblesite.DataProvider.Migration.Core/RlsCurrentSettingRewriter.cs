using System.Text;
using Outcome;
using RewriteError = Outcome.Result<
    Nimblesite.DataProvider.Migration.Core.RlsCurrentSettingRewrite,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Error<
    Nimblesite.DataProvider.Migration.Core.RlsCurrentSettingRewrite,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;
using RewriteOk = Outcome.Result<
    Nimblesite.DataProvider.Migration.Core.RlsCurrentSettingRewrite,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<
    Nimblesite.DataProvider.Migration.Core.RlsCurrentSettingRewrite,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;
using StringError = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Error<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;
using StringOk = Outcome.Result<string, Nimblesite.DataProvider.Migration.Core.MigrationError>.Ok<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;

namespace Nimblesite.DataProvider.Migration.Core;

internal sealed record RlsCurrentSettingReplacement(string Sentinel, string SqlExpression);

internal sealed record RlsCurrentSettingRewrite(
    string Text,
    IReadOnlyList<RlsCurrentSettingReplacement> Replacements
);

internal static class RlsCurrentSettingRewriter
{
    private const string FunctionName = "current_setting";
    private const string SentinelPrefix = "__RLS_CURRENT_SETTING_";

    internal static Result<string, MigrationError> ReplaceCallsForSimplePredicate(
        string source,
        RlsPlatform platform,
        string contextName
    )
    {
        var rewritten = RewriteCalls(source, platform, contextName, useSentinels: false);
        return rewritten switch
        {
            RewriteOk ok => new StringOk(ok.Value.Text),
            RewriteError err => new StringError(err.Value),
        };
    }

    internal static Result<RlsCurrentSettingRewrite, MigrationError> ReplaceCallsForPipeline(
        string source,
        RlsPlatform platform,
        string contextName
    ) => RewriteCalls(source, platform, contextName, useSentinels: true);

    internal static string RestoreSentinels(
        string sql,
        IReadOnlyList<RlsCurrentSettingReplacement> replacements
    )
    {
        var restored = sql;
        foreach (var replacement in replacements)
        {
            restored = restored.Replace(
                $"'{replacement.Sentinel}'",
                replacement.SqlExpression,
                StringComparison.Ordinal
            );
        }
        return restored;
    }

    private static Result<RlsCurrentSettingRewrite, MigrationError> RewriteCalls(
        string source,
        RlsPlatform platform,
        string contextName,
        bool useSentinels
    )
    {
        var sb = new StringBuilder(source.Length);
        var replacements = new List<RlsCurrentSettingReplacement>();
        var i = 0;

        while (i < source.Length)
        {
            if (source[i] == '\'')
            {
                CopyStringLiteral(source, sb, ref i);
                continue;
            }

            if (!TryReadIdentifier(source, i, out var word, out var afterWord))
            {
                sb.Append(source[i]);
                i++;
                continue;
            }

            if (!word.Equals(FunctionName, StringComparison.Ordinal))
            {
                sb.Append(word);
                i = afterWord;
                continue;
            }

            var afterWhitespace = afterWord;
            SkipWhitespace(source, ref afterWhitespace);
            if (afterWhitespace >= source.Length || source[afterWhitespace] != '(')
            {
                sb.Append(word);
                i = afterWord;
                continue;
            }

            var call = TryParseCurrentSettingCall(source, i, contextName);
            if (
                call
                is Result<CurrentSettingCall, MigrationError>.Error<
                    CurrentSettingCall,
                    MigrationError
                > callErr
            )
            {
                return new RewriteError(callErr.Value);
            }

            if (
                call
                is not Result<CurrentSettingCall, MigrationError>.Ok<
                    CurrentSettingCall,
                    MigrationError
                > callOk
            )
            {
                return new RewriteError(
                    MigrationError.RlsLqlParse(contextName, "current_setting() could not be parsed")
                );
            }

            if (platform != RlsPlatform.Postgres)
            {
                return new RewriteError(
                    MigrationError.RlsLqlTranspile(
                        contextName,
                        "current_setting(<string>) is currently supported only for PostgreSQL"
                    )
                );
            }

            var sqlExpression =
                $"current_setting({callOk.Value.StringLiteral}, true){callOk.Value.Cast}";
            if (useSentinels)
            {
                var sentinel = $"{SentinelPrefix}{replacements.Count}__";
                sb.Append('\'').Append(sentinel).Append('\'');
                replacements.Add(new RlsCurrentSettingReplacement(sentinel, sqlExpression));
            }
            else
            {
                sb.Append(sqlExpression);
            }

            i = callOk.Value.EndIndex;
        }

        return new RewriteOk(new RlsCurrentSettingRewrite(sb.ToString(), replacements));
    }

    private static Result<CurrentSettingCall, MigrationError> TryParseCurrentSettingCall(
        string source,
        int start,
        string contextName
    )
    {
        var i = start + FunctionName.Length;
        SkipWhitespace(source, ref i);
        i++;
        SkipWhitespace(source, ref i);

        if (!TryReadStringLiteral(source, i, out var literal, out var afterLiteral))
        {
            return new Result<CurrentSettingCall, MigrationError>.Error<
                CurrentSettingCall,
                MigrationError
            >(
                MigrationError.RlsLqlParse(
                    contextName,
                    "current_setting() requires exactly one string-literal argument"
                )
            );
        }

        i = afterLiteral;
        SkipWhitespace(source, ref i);
        if (i >= source.Length || source[i] != ')')
        {
            return new Result<CurrentSettingCall, MigrationError>.Error<
                CurrentSettingCall,
                MigrationError
            >(
                MigrationError.RlsLqlParse(
                    contextName,
                    "current_setting() requires exactly one string-literal argument"
                )
            );
        }

        i++;
        var cast = ReadOptionalCast(source, ref i, contextName);
        return cast switch
        {
            StringOk ok => new Result<CurrentSettingCall, MigrationError>.Ok<
                CurrentSettingCall,
                MigrationError
            >(new CurrentSettingCall(literal, ok.Value, i)),
            StringError err => new Result<CurrentSettingCall, MigrationError>.Error<
                CurrentSettingCall,
                MigrationError
            >(err.Value),
        };
    }

    private static Result<string, MigrationError> ReadOptionalCast(
        string source,
        ref int i,
        string contextName
    )
    {
        if (i + 1 >= source.Length || source[i] != ':' || source[i + 1] != ':')
        {
            return new StringOk(string.Empty);
        }

        var castStart = i;
        i += 2;
        var typeStart = i;
        while (
            i < source.Length
            && (
                char.IsLetterOrDigit(source[i])
                || source[i] == '_'
                || source[i] == '.'
                || source[i] == '['
                || source[i] == ']'
            )
        )
        {
            i++;
        }

        return i == typeStart
            ? new StringError(
                MigrationError.RlsLqlParse(
                    contextName,
                    "current_setting() cast requires a type name after ::"
                )
            )
            : new StringOk(source[castStart..i]);
    }

    private static bool TryReadIdentifier(
        string source,
        int start,
        out string word,
        out int afterWord
    )
    {
        word = string.Empty;
        afterWord = start;
        if (!IsIdentifierStart(source, start))
        {
            return false;
        }

        var i = start + 1;
        while (i < source.Length && IsIdentifierPart(source[i]))
        {
            i++;
        }

        word = source[start..i];
        afterWord = i;
        return true;
    }

    private static bool IsIdentifierStart(string source, int index) =>
        index < source.Length
        && (char.IsLetter(source[index]) || source[index] == '_')
        && (index == 0 || !IsIdentifierPart(source[index - 1]));

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static void SkipWhitespace(string source, ref int i)
    {
        while (i < source.Length && char.IsWhiteSpace(source[i]))
        {
            i++;
        }
    }

    private static void CopyStringLiteral(string source, StringBuilder sb, ref int i)
    {
        sb.Append(source[i]);
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
    }

    private static bool TryReadStringLiteral(
        string source,
        int start,
        out string literal,
        out int afterLiteral
    )
    {
        literal = string.Empty;
        afterLiteral = start;
        if (start >= source.Length || source[start] != '\'')
        {
            return false;
        }

        var i = start + 1;
        while (i < source.Length)
        {
            if (source[i] == '\'')
            {
                if (i + 1 < source.Length && source[i + 1] == '\'')
                {
                    i += 2;
                    continue;
                }
                afterLiteral = i + 1;
                literal = source[start..afterLiteral];
                return true;
            }
            i++;
        }

        return false;
    }

    private sealed record CurrentSettingCall(string StringLiteral, string Cast, int EndIndex);
}
