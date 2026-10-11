using static global::Tests.Shared.StringAssertions;

namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// Unit tests for LqlDefaultTranslator covering all code paths.
/// Tests both ToPostgres() and ToSqlite() methods for complete coverage.
/// </summary>
public sealed partial class LqlDefaultTranslatorTests
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
    [MemberData(nameof(ToPostgres_TimestampFunctions_TranslatesCorrectlyInlineCases))]
    // Case insensitive
    // Whitespace trimmed

    public void ToPostgres_TimestampFunctions_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(ToSqlite_TimestampFunctions_TranslatesCorrectlyInlineCases))]
    // Case insensitive
    // Whitespace trimmed

    public void ToSqlite_TimestampFunctions_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);
        Assert.Equal(expected, result);
    }

    // =========================================================================
    // UUID FUNCTIONS - gen_uuid(), uuid()
    // =========================================================================

    [Theory]
    [MemberData(nameof(ToPostgres_UuidFunctions_TranslatesToGenRandomUuidInlineCases))]
    // Case insensitive

    public void ToPostgres_UuidFunctions_TranslatesToGenRandomUuid(string input)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal("gen_random_uuid()", result);
    }

    [Theory]
    [MemberData(nameof(ToSqlite_UuidFunctions_TranslatesToHexExpressionInlineCases))]
    public void ToSqlite_UuidFunctions_TranslatesToHexExpression(string input)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);

        // Should contain the SQLite UUID v4 expression parts
        ContainsAll(actual: result, expected: ["hex(randomblob", "'-'", "'-4'", "'89ab'"]); // UUID variant bits
    }

    // =========================================================================
    // BOOLEAN LITERALS - true, false
    // =========================================================================

    [Theory]
    [MemberData(nameof(ToPostgres_BooleanLiterals_TranslatesCorrectlyInlineCases))]
    public void ToPostgres_BooleanLiterals_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(ToSqlite_BooleanLiterals_TranslatesToIntegerValuesInlineCases))]
    public void ToSqlite_BooleanLiterals_TranslatesToIntegerValues(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);
        Assert.Equal(expected, result);
    }

    // =========================================================================
    // NUMERIC LITERALS - integers and decimals
    // =========================================================================

    [Theory]
    [MemberData(nameof(ToPostgres_IntegerLiterals_PassThroughInlineCases))]
    // int32 max
    // int32 min
    // Whitespace trimmed
    public void ToPostgres_IntegerLiterals_PassThrough(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(ToSqlite_IntegerLiterals_PassThroughInlineCases))]
    public void ToSqlite_IntegerLiterals_PassThrough(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(ToPostgres_DecimalLiterals_PassThroughInlineCases))]
    // Whitespace trimmed
    public void ToPostgres_DecimalLiterals_PassThrough(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(ToSqlite_DecimalLiterals_PassThroughInlineCases))]
    public void ToSqlite_DecimalLiterals_PassThrough(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);
        Assert.Equal(expected, result);
    }

    // =========================================================================
    // STRING LITERALS - single-quoted strings
    // =========================================================================

    [Theory]
    [MemberData(nameof(ToPostgres_StringLiterals_PassThroughInlineCases))]
    // Gets lowercased during normalization

    public void ToPostgres_StringLiterals_PassThrough(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(ToSqlite_StringLiterals_PassThroughInlineCases))]
    // Gets lowercased during normalization

    public void ToSqlite_StringLiterals_PassThrough(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);
        Assert.Equal(expected, result);
    }

    // =========================================================================
    // FUNCTION CALLS - lower(), upper(), coalesce(), length(), etc.
    // =========================================================================

    [Theory]
    [MemberData(nameof(ToPostgres_LowerFunction_TranslatesCorrectlyInlineCases))]
    public void ToPostgres_LowerFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(ToSqlite_LowerFunction_TranslatesCorrectlyInlineCases))]
    public void ToSqlite_LowerFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(ToPostgres_UpperFunction_TranslatesCorrectlyInlineCases))]
    public void ToPostgres_UpperFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(ToSqlite_UpperFunction_TranslatesCorrectlyInlineCases))]
    public void ToSqlite_UpperFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(ToPostgres_CoalesceFunction_TranslatesCorrectlyInlineCases))]
    public void ToPostgres_CoalesceFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(ToSqlite_CoalesceFunction_TranslatesCorrectlyInlineCases))]
    public void ToSqlite_CoalesceFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(ToPostgres_LengthFunction_TranslatesCorrectlyInlineCases))]
    public void ToPostgres_LengthFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(ToSqlite_LengthFunction_TranslatesCorrectlyInlineCases))]
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
    [MemberData(nameof(ToPostgres_SubstringWith3Args_UsesFromForSyntax_CasesInlineCases))]
    // ToPostgres_SubstringWith2Args_UsesCommaSyntax

    // ToPostgres_UnknownFunction_PassThroughWithArgs

    // ToPostgres_UnknownFunctionNoArgs_PassThrough

    // ToPostgres_ColumnReference_PassThrough

    // ToPostgres_ComplexExpression_PassThrough

    // ToPostgres_FunctionWithWhitespaceInArgs_PreservesWhitespace

    // ToPostgres_FunctionWithEmptyArgs_HandlesGracefully

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
    [MemberData(nameof(ToSqlite_SubstringWith3Args_UsesSubstrFunction_CasesInlineCases))]
    // ToSqlite_SubstringWith2Args_UsesSubstrFunction

    // ToSqlite_ConcatWith2Args_UsesConcatOperator

    // ToSqlite_ConcatWith3Args_UsesConcatOperator

    // ToSqlite_ConcatWithNoArgs_ReturnsEmptyString

    // ToSqlite_UnknownFunction_PassThroughWithArgs

    // ToSqlite_UnknownFunctionNoArgs_PassThrough

    // ToSqlite_ColumnReference_PassThrough

    // ToSqlite_ComplexExpression_PassThrough

    // ToSqlite_FunctionWithWhitespaceInArgs_PreservesWhitespace

    // ToSqlite_FunctionWithEmptyArgs_HandlesGracefully

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
    [MemberData(nameof(ToPostgres_TrimFunction_TranslatesCorrectlyInlineCases))]
    public void ToPostgres_TrimFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(ToSqlite_TrimFunction_TranslatesCorrectlyInlineCases))]
    public void ToSqlite_TrimFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);
        Assert.Equal(expected, result);
    }

    // =========================================================================
    // CONCAT FUNCTION - different syntax between Postgres and SQLite
    // =========================================================================

    [Theory]
    [MemberData(nameof(ToPostgres_ConcatFunction_TranslatesCorrectlyInlineCases))]
    public void ToPostgres_ConcatFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    // =========================================================================
    // ABS FUNCTION
    // =========================================================================

    [Theory]
    [MemberData(nameof(ToPostgres_AbsFunction_TranslatesCorrectlyInlineCases))]
    public void ToPostgres_AbsFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(ToSqlite_AbsFunction_TranslatesCorrectlyInlineCases))]
    public void ToSqlite_AbsFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);
        Assert.Equal(expected, result);
    }

    // =========================================================================
    // ROUND FUNCTION
    // =========================================================================

    [Theory]
    [MemberData(nameof(ToPostgres_RoundFunction_TranslatesCorrectlyInlineCases))]
    public void ToPostgres_RoundFunction_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(ToSqlite_RoundFunction_TranslatesCorrectlyInlineCases))]
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
    [MemberData(nameof(BothPlatforms_SameLql_ProduceValidSqlInlineCases))]
    public void BothPlatforms_SameLql_ProduceValidSql(string lql)
    {
        // These should not throw and should produce non-empty results
        var pgResult = LqlDefaultTranslator.ToPostgres(lql);
        var sqliteResult = LqlDefaultTranslator.ToSqlite(lql);

        Assert.False(string.IsNullOrEmpty(pgResult));
        Assert.False(string.IsNullOrEmpty(sqliteResult));
    }
}
