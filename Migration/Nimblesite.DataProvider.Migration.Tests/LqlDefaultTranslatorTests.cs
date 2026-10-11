namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// Unit tests for LqlDefaultTranslator covering all code paths.
/// Tests both ToPostgres() and ToSqlite() methods for complete coverage.
/// </summary>
public sealed class LqlDefaultTranslatorTests
{
    // =========================================================================
    // NULL HANDLING - ArgumentNullException
    // =========================================================================

    [Fact]
    public void ToPostgres_NullInput_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => LqlDefaultTranslator.ToPostgres(null!));

    [Fact]
    public void ToSqlite_NullInput_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => LqlDefaultTranslator.ToSqlite(null!));

    // =========================================================================
    // TIMESTAMP FUNCTIONS - now(), current_timestamp(), current_date(), current_time()
    // =========================================================================

    [Theory]
    [InlineData("now()", "CURRENT_TIMESTAMP")]
    [InlineData("NOW()", "CURRENT_TIMESTAMP")] // Case insensitive
    [InlineData("  now()  ", "CURRENT_TIMESTAMP")] // Whitespace trimmed
    [InlineData("current_timestamp()", "CURRENT_TIMESTAMP")]
    [InlineData("CURRENT_TIMESTAMP()", "CURRENT_TIMESTAMP")]
    [InlineData("current_date()", "CURRENT_DATE")]
    [InlineData("CURRENT_DATE()", "CURRENT_DATE")]
    [InlineData("current_time()", "CURRENT_TIME")]
    [InlineData("CURRENT_TIME()", "CURRENT_TIME")]
    public void ToPostgres_TimestampFunctions_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("now()", "(datetime('now'))")]
    [InlineData("NOW()", "(datetime('now'))")] // Case insensitive
    [InlineData("  now()  ", "(datetime('now'))")] // Whitespace trimmed
    [InlineData("current_timestamp()", "CURRENT_TIMESTAMP")]
    [InlineData("current_date()", "(date('now'))")]
    [InlineData("current_time()", "(time('now'))")]
    public void ToSqlite_TimestampFunctions_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);
        Assert.Equal(expected, result);
    }

    // =========================================================================
    // UUID FUNCTIONS - gen_uuid(), uuid()
    // =========================================================================

    [Theory]
    [InlineData("gen_uuid()")]
    [InlineData("GEN_UUID()")] // Case insensitive
    [InlineData("uuid()")]
    [InlineData("UUID()")]
    public void ToPostgres_UuidFunctions_TranslatesToGenRandomUuid(string input)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal("gen_random_uuid()", result);
    }

    [Theory]
    [InlineData("gen_uuid()")]
    [InlineData("GEN_UUID()")]
    [InlineData("uuid()")]
    [InlineData("UUID()")]
    public void ToSqlite_UuidFunctions_TranslatesToHexExpression(string input)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);

        // Should contain the SQLite UUID v4 expression parts
        Assert.Contains("hex(randomblob", result);
        Assert.Contains("'-'", result);
        Assert.Contains("'-4'", result); // UUID v4 marker
        Assert.Contains("'89ab'", result); // UUID variant bits
    }

    // =========================================================================
    // BOOLEAN LITERALS - true, false
    // =========================================================================

    [Theory]
    [InlineData("true", "true")]
    [InlineData("TRUE", "true")]
    [InlineData("True", "true")]
    [InlineData("  true  ", "true")]
    [InlineData("false", "false")]
    [InlineData("FALSE", "false")]
    [InlineData("False", "false")]
    public void ToPostgres_BooleanLiterals_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("true", "1")]
    [InlineData("TRUE", "1")]
    [InlineData("True", "1")]
    [InlineData("false", "0")]
    [InlineData("FALSE", "0")]
    [InlineData("False", "0")]
    public void ToSqlite_BooleanLiterals_TranslatesToIntegerValues(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);
        Assert.Equal(expected, result);
    }

    // =========================================================================
    // NUMERIC LITERALS - integers and decimals
    // =========================================================================

    [Theory]
    [InlineData("0", "0")]
    [InlineData("1", "1")]
    [InlineData("42", "42")]
    [InlineData("-100", "-100")]
    [InlineData("2147483647", "2147483647")] // int32 max
    [InlineData("-2147483648", "-2147483648")] // int32 min
    [InlineData("  123  ", "123")] // Whitespace trimmed
    public void ToPostgres_IntegerLiterals_PassThrough(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("1", "1")]
    [InlineData("42", "42")]
    [InlineData("-100", "-100")]
    [InlineData("2147483647", "2147483647")]
    [InlineData("-2147483648", "-2147483648")]
    public void ToSqlite_IntegerLiterals_PassThrough(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0.0", "0.0")]
    [InlineData("3.14", "3.14")]
    [InlineData("-99.99", "-99.99")]
    [InlineData("0.00000001", "0.00000001")]
    [InlineData("3.1415926535", "3.1415926535")]
    [InlineData("  1.5  ", "1.5")] // Whitespace trimmed
    public void ToPostgres_DecimalLiterals_PassThrough(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0.0", "0.0")]
    [InlineData("3.14", "3.14")]
    [InlineData("-99.99", "-99.99")]
    [InlineData("0.00000001", "0.00000001")]
    public void ToSqlite_DecimalLiterals_PassThrough(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);
        Assert.Equal(expected, result);
    }

    // =========================================================================
    // STRING LITERALS - single-quoted strings
    // =========================================================================

    [Theory]
    [InlineData("'hello'", "'hello'")]
    [InlineData("'Hello World'", "'hello world'")] // Gets lowercased during normalization
    [InlineData("''", "''")]
    [InlineData("'test123'", "'test123'")]
    [InlineData("'foo-bar-baz'", "'foo-bar-baz'")]
    [InlineData("'snake_case'", "'snake_case'")]
    public void ToPostgres_StringLiterals_PassThrough(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("'hello'", "'hello'")]
    [InlineData("'Hello World'", "'hello world'")] // Gets lowercased during normalization
    [InlineData("''", "''")]
    [InlineData("'test123'", "'test123'")]
    public void ToSqlite_StringLiterals_PassThrough(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);
        Assert.Equal(expected, result);
    }

    // =========================================================================
    // FUNCTION CALLS - lower(), upper(), coalesce(), length(), etc.
    // =========================================================================

    [Theory]
    [InlineData("lower(name)", "lower(name)")]
    [InlineData("LOWER(name)", "lower(name)")]
    [InlineData("lower(column_name)", "lower(column_name)")]
    public void ToPostgres_LowerFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("lower(name)", "lower(name)")]
    [InlineData("LOWER(name)", "lower(name)")]
    public void ToSqlite_LowerFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("upper(name)", "upper(name)")]
    [InlineData("UPPER(name)", "upper(name)")]
    public void ToPostgres_UpperFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("upper(name)", "upper(name)")]
    [InlineData("UPPER(name)", "upper(name)")]
    public void ToSqlite_UpperFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("coalesce(a, b)", "COALESCE(a, b)")]
    [InlineData("COALESCE(x, y, z)", "COALESCE(x, y, z)")]
    [InlineData("coalesce(name, 'default')", "COALESCE(name, 'default')")]
    public void ToPostgres_CoalesceFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("coalesce(a, b)", "coalesce(a, b)")]
    [InlineData("COALESCE(x, y, z)", "coalesce(x, y, z)")]
    public void ToSqlite_CoalesceFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("length(name)", "length(name)")]
    [InlineData("LENGTH(text)", "length(text)")]
    public void ToPostgres_LengthFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("length(name)", "length(name)")]
    [InlineData("LENGTH(text)", "length(text)")]
    public void ToSqlite_LengthFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);
        Assert.Equal(expected, result);
    }

    // =========================================================================
    // SUBSTRING FUNCTION - different syntax between Postgres and SQLite
    // =========================================================================

    [Theory]
    // ToPostgres_SubstringWith3Args_UsesFromForSyntax
    [InlineData("substring(text, 1, 5)", "substring(text from 1 for 5)")]
    // ToPostgres_SubstringWith2Args_UsesCommaSyntax
    [InlineData("substring(text, 1)", "substring(text, 1)")]
    // ToPostgres_UnknownFunction_PassThroughWithArgs
    [InlineData("custom_func(arg1, arg2)", "custom_func(arg1, arg2)")]
    // ToPostgres_UnknownFunctionNoArgs_PassThrough
    [InlineData("my_function()", "my_function()")]
    // ToPostgres_ColumnReference_PassThrough
    [InlineData("column_name", "column_name")]
    // ToPostgres_ComplexExpression_PassThrough
    [InlineData("some_expression + 1", "some_expression + 1")]
    // ToPostgres_FunctionWithWhitespaceInArgs_PreservesWhitespace
    [InlineData("coalesce( a , b , c )", "COALESCE(a, b, c)")]
    // ToPostgres_FunctionWithEmptyArgs_HandlesGracefully
    [InlineData("lower()", "lower()")]
    public void ToPostgres_SubstringWith3Args_UsesFromForSyntax_Cases(
        string caseValue0,
        string caseValue1
    )
    {
        var result = LqlDefaultTranslator.ToPostgres(caseValue0);
        Assert.Equal(caseValue1, result);
    }

    [Theory]
    // ToSqlite_SubstringWith3Args_UsesSubstrFunction
    [InlineData("substring(text, 1, 5)", "substr(text, 1, 5)")]
    // ToSqlite_SubstringWith2Args_UsesSubstrFunction
    [InlineData("substring(text, 1)", "substr(text, 1)")]
    // ToSqlite_ConcatWith2Args_UsesConcatOperator
    [InlineData("concat(a, b)", "a || b")]
    // ToSqlite_ConcatWith3Args_UsesConcatOperator
    [InlineData("concat(first, ' ', last)", "first || ' ' || last")]
    // ToSqlite_ConcatWithNoArgs_ReturnsEmptyString
    [InlineData("concat()", "''")]
    // ToSqlite_UnknownFunction_PassThroughWithArgs
    [InlineData("custom_func(arg1, arg2)", "custom_func(arg1, arg2)")]
    // ToSqlite_UnknownFunctionNoArgs_PassThrough
    [InlineData("my_function()", "my_function()")]
    // ToSqlite_ColumnReference_PassThrough
    [InlineData("column_name", "column_name")]
    // ToSqlite_ComplexExpression_PassThrough
    [InlineData("some_expression + 1", "some_expression + 1")]
    // ToSqlite_FunctionWithWhitespaceInArgs_PreservesWhitespace
    [InlineData("coalesce( a , b , c )", "coalesce(a, b, c)")]
    // ToSqlite_FunctionWithEmptyArgs_HandlesGracefully
    [InlineData("lower()", "lower()")]
    public void ToSqlite_SubstringWith3Args_UsesSubstrFunction_Cases(
        string caseValue0,
        string caseValue1
    )
    {
        var result = LqlDefaultTranslator.ToSqlite(caseValue0);
        Assert.Equal(caseValue1, result);
    }

    // =========================================================================
    // TRIM FUNCTION
    // =========================================================================

    [Theory]
    [InlineData("trim(name)", "trim(name)")]
    [InlineData("TRIM(text)", "trim(text)")]
    public void ToPostgres_TrimFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("trim(name)", "trim(name)")]
    [InlineData("TRIM(text)", "trim(text)")]
    public void ToSqlite_TrimFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);
        Assert.Equal(expected, result);
    }

    // =========================================================================
    // CONCAT FUNCTION - different syntax between Postgres and SQLite
    // =========================================================================

    [Theory]
    [InlineData("concat(a, b)", "concat(a, b)")]
    [InlineData("concat(first, ' ', last)", "concat(first, ' ', last)")]
    public void ToPostgres_ConcatFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    // =========================================================================
    // ABS FUNCTION
    // =========================================================================

    [Theory]
    [InlineData("abs(value)", "abs(value)")]
    [InlineData("ABS(-10)", "abs(-10)")]
    public void ToPostgres_AbsFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("abs(value)", "abs(value)")]
    [InlineData("ABS(-10)", "abs(-10)")]
    public void ToSqlite_AbsFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);
        Assert.Equal(expected, result);
    }

    // =========================================================================
    // ROUND FUNCTION
    // =========================================================================

    [Theory]
    [InlineData("round(value)", "round(value)")]
    [InlineData("round(price, 2)", "round(price, 2)")]
    [InlineData("ROUND(amount, 0)", "round(amount, 0)")]
    public void ToPostgres_RoundFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("round(value)", "round(value)")]
    [InlineData("round(price, 2)", "round(price, 2)")]
    [InlineData("ROUND(amount, 0)", "round(amount, 0)")]
    public void ToSqlite_RoundFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);
        Assert.Equal(expected, result);
    }

    // =========================================================================
    // UNKNOWN FUNCTIONS - pass through with function name preserved
    // =========================================================================

    // =========================================================================
    // NON-FUNCTION EXPRESSIONS - column references, pass through
    // =========================================================================

    // =========================================================================
    // EDGE CASES - whitespace, mixed case, empty args
    // =========================================================================

    // =========================================================================
    // PLATFORM EQUIVALENCE - Same LQL = Same semantic result
    // =========================================================================

    [Theory]
    [InlineData("42")]
    [InlineData("3.14")]
    [InlineData("'hello'")]
    [InlineData("lower(name)")]
    [InlineData("upper(name)")]
    [InlineData("length(text)")]
    [InlineData("trim(value)")]
    [InlineData("abs(-5)")]
    [InlineData("round(price, 2)")]
    public void BothPlatforms_SameLql_ProduceValidSql(string lql)
    {
        // These should not throw and should produce non-empty results
        var pgResult = LqlDefaultTranslator.ToPostgres(lql);
        var sqliteResult = LqlDefaultTranslator.ToSqlite(lql);

        Assert.False(string.IsNullOrEmpty(pgResult));
        Assert.False(string.IsNullOrEmpty(sqliteResult));
    }
}
