// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class LqlDefaultTranslatorTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToPostgres_TimestampFunctions_TranslatesCorrectlyInlineCases()
    {
        yield return ["now()", "CURRENT_TIMESTAMP"];
        yield return ["NOW()", "CURRENT_TIMESTAMP"];
        yield return ["  now()  ", "CURRENT_TIMESTAMP"];
        yield return ["current_timestamp()", "CURRENT_TIMESTAMP"];
        yield return ["CURRENT_TIMESTAMP()", "CURRENT_TIMESTAMP"];
        yield return ["current_date()", "CURRENT_DATE"];
        yield return ["CURRENT_DATE()", "CURRENT_DATE"];
        yield return ["current_time()", "CURRENT_TIME"];
        yield return ["CURRENT_TIME()", "CURRENT_TIME"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToSqlite_TimestampFunctions_TranslatesCorrectlyInlineCases()
    {
        yield return ["now()", "(datetime('now'))"];
        yield return ["NOW()", "(datetime('now'))"];
        yield return ["  now()  ", "(datetime('now'))"];
        yield return ["current_timestamp()", "CURRENT_TIMESTAMP"];
        yield return ["current_date()", "(date('now'))"];
        yield return ["current_time()", "(time('now'))"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToPostgres_UuidFunctions_TranslatesToGenRandomUuidInlineCases()
    {
        yield return ["gen_uuid()"];
        yield return ["GEN_UUID()"];
        yield return ["uuid()"];
        yield return ["UUID()"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToSqlite_UuidFunctions_TranslatesToHexExpressionInlineCases()
    {
        yield return ["gen_uuid()"];
        yield return ["GEN_UUID()"];
        yield return ["uuid()"];
        yield return ["UUID()"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToPostgres_BooleanLiterals_TranslatesCorrectlyInlineCases()
    {
        yield return ["true", "true"];
        yield return ["TRUE", "true"];
        yield return ["True", "true"];
        yield return ["  true  ", "true"];
        yield return ["false", "false"];
        yield return ["FALSE", "false"];
        yield return ["False", "false"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToSqlite_BooleanLiterals_TranslatesToIntegerValuesInlineCases()
    {
        yield return ["true", "1"];
        yield return ["TRUE", "1"];
        yield return ["True", "1"];
        yield return ["false", "0"];
        yield return ["FALSE", "0"];
        yield return ["False", "0"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToPostgres_IntegerLiterals_PassThroughInlineCases()
    {
        yield return ["0", "0"];
        yield return ["1", "1"];
        yield return ["42", "42"];
        yield return ["-100", "-100"];
        yield return ["2147483647", "2147483647"];
        yield return ["-2147483648", "-2147483648"];
        yield return ["  123  ", "123"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToSqlite_IntegerLiterals_PassThroughInlineCases()
    {
        yield return ["0", "0"];
        yield return ["1", "1"];
        yield return ["42", "42"];
        yield return ["-100", "-100"];
        yield return ["2147483647", "2147483647"];
        yield return ["-2147483648", "-2147483648"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToPostgres_DecimalLiterals_PassThroughInlineCases()
    {
        yield return ["0.0", "0.0"];
        yield return ["3.14", "3.14"];
        yield return ["-99.99", "-99.99"];
        yield return ["0.00000001", "0.00000001"];
        yield return ["3.1415926535", "3.1415926535"];
        yield return ["  1.5  ", "1.5"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToSqlite_DecimalLiterals_PassThroughInlineCases()
    {
        yield return ["0.0", "0.0"];
        yield return ["3.14", "3.14"];
        yield return ["-99.99", "-99.99"];
        yield return ["0.00000001", "0.00000001"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToPostgres_StringLiterals_PassThroughInlineCases()
    {
        yield return ["'hello'", "'hello'"];
        yield return ["'Hello World'", "'hello world'"];
        yield return ["''", "''"];
        yield return ["'test123'", "'test123'"];
        yield return ["'foo-bar-baz'", "'foo-bar-baz'"];
        yield return ["'snake_case'", "'snake_case'"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToSqlite_StringLiterals_PassThroughInlineCases()
    {
        yield return ["'hello'", "'hello'"];
        yield return ["'Hello World'", "'hello world'"];
        yield return ["''", "''"];
        yield return ["'test123'", "'test123'"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToPostgres_LowerFunction_TranslatesCorrectlyInlineCases()
    {
        yield return ["lower(name)", "lower(name)"];
        yield return ["LOWER(name)", "lower(name)"];
        yield return ["lower(column_name)", "lower(column_name)"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToSqlite_LowerFunction_TranslatesCorrectlyInlineCases()
    {
        yield return ["lower(name)", "lower(name)"];
        yield return ["LOWER(name)", "lower(name)"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToPostgres_UpperFunction_TranslatesCorrectlyInlineCases()
    {
        yield return ["upper(name)", "upper(name)"];
        yield return ["UPPER(name)", "upper(name)"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToSqlite_UpperFunction_TranslatesCorrectlyInlineCases()
    {
        yield return ["upper(name)", "upper(name)"];
        yield return ["UPPER(name)", "upper(name)"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToPostgres_CoalesceFunction_TranslatesCorrectlyInlineCases()
    {
        yield return ["coalesce(a, b)", "COALESCE(a, b)"];
        yield return ["COALESCE(x, y, z)", "COALESCE(x, y, z)"];
        yield return ["coalesce(name, 'default')", "COALESCE(name, 'default')"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToSqlite_CoalesceFunction_TranslatesCorrectlyInlineCases()
    {
        yield return ["coalesce(a, b)", "coalesce(a, b)"];
        yield return ["COALESCE(x, y, z)", "coalesce(x, y, z)"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToPostgres_LengthFunction_TranslatesCorrectlyInlineCases()
    {
        yield return ["length(name)", "length(name)"];
        yield return ["LENGTH(text)", "length(text)"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToSqlite_LengthFunction_TranslatesCorrectlyInlineCases()
    {
        yield return ["length(name)", "length(name)"];
        yield return ["LENGTH(text)", "length(text)"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToPostgres_SubstringWith3Args_UsesFromForSyntax_CasesInlineCases()
    {
        yield return ["substring(text, 1, 5)", "substring(text from 1 for 5)"];
        yield return ["substring(text, 1)", "substring(text, 1)"];
        yield return ["custom_func(arg1, arg2)", "custom_func(arg1, arg2)"];
        yield return ["my_function()", "my_function()"];
        yield return ["column_name", "column_name"];
        yield return ["some_expression + 1", "some_expression + 1"];
        yield return ["coalesce( a , b , c )", "COALESCE(a, b, c)"];
        yield return ["lower()", "lower()"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToSqlite_SubstringWith3Args_UsesSubstrFunction_CasesInlineCases()
    {
        yield return ["substring(text, 1, 5)", "substr(text, 1, 5)"];
        yield return ["substring(text, 1)", "substr(text, 1)"];
        yield return ["concat(a, b)", "a || b"];
        yield return ["concat(first, ' ', last)", "first || ' ' || last"];
        yield return ["concat()", "''"];
        yield return ["custom_func(arg1, arg2)", "custom_func(arg1, arg2)"];
        yield return ["my_function()", "my_function()"];
        yield return ["column_name", "column_name"];
        yield return ["some_expression + 1", "some_expression + 1"];
        yield return ["coalesce( a , b , c )", "coalesce(a, b, c)"];
        yield return ["lower()", "lower()"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToPostgres_TrimFunction_TranslatesCorrectlyInlineCases()
    {
        yield return ["trim(name)", "trim(name)"];
        yield return ["TRIM(text)", "trim(text)"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToSqlite_TrimFunction_TranslatesCorrectlyInlineCases()
    {
        yield return ["trim(name)", "trim(name)"];
        yield return ["TRIM(text)", "trim(text)"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToPostgres_ConcatFunction_TranslatesCorrectlyInlineCases()
    {
        yield return ["concat(a, b)", "concat(a, b)"];
        yield return ["concat(first, ' ', last)", "concat(first, ' ', last)"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToPostgres_AbsFunction_TranslatesCorrectlyInlineCases()
    {
        yield return ["abs(value)", "abs(value)"];
        yield return ["ABS(-10)", "abs(-10)"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToSqlite_AbsFunction_TranslatesCorrectlyInlineCases()
    {
        yield return ["abs(value)", "abs(value)"];
        yield return ["ABS(-10)", "abs(-10)"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToPostgres_RoundFunction_TranslatesCorrectlyInlineCases()
    {
        yield return ["round(value)", "round(value)"];
        yield return ["round(price, 2)", "round(price, 2)"];
        yield return ["ROUND(amount, 0)", "round(amount, 0)"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ToSqlite_RoundFunction_TranslatesCorrectlyInlineCases()
    {
        yield return ["round(value)", "round(value)"];
        yield return ["round(price, 2)", "round(price, 2)"];
        yield return ["ROUND(amount, 0)", "round(amount, 0)"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> BothPlatforms_SameLql_ProduceValidSqlInlineCases()
    {
        yield return ["42"];
        yield return ["3.14"];
        yield return ["'hello'"];
        yield return ["lower(name)"];
        yield return ["upper(name)"];
        yield return ["length(text)"];
        yield return ["trim(value)"];
        yield return ["abs(-5)"];
        yield return ["round(price, 2)"];
    }
}
