using static global::Tests.Shared.StringAssertions;

namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// Unit tests for LqlDefaultTranslator covering all code paths.
/// Tests both ToPostgres() and ToSqlite() methods for complete coverage.
/// </summary>
/// <remarks>
/// Every <c>(input, expected)</c> case is driven by the shared inline case
/// fixtures and asserted by a single theory per platform. The case category is
/// encoded in the <see cref="MemberDataAttribute"/> source name.
/// </remarks>
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
    // POSTGRES TRANSLATION - timestamp/uuid/boolean/numeric/string literals and
    // function calls (lower, upper, coalesce, length, substring, trim, concat,
    // abs, round) all reduce to the same input -> expected translation contract.
    // =========================================================================

    [Theory]
    [MemberData(nameof(ToPostgres_TimestampFunctions_TranslatesCorrectlyInlineCases))]
    [MemberData(nameof(ToPostgres_BooleanLiterals_TranslatesCorrectlyInlineCases))]
    [MemberData(nameof(ToPostgres_IntegerLiterals_PassThroughInlineCases))]
    [MemberData(nameof(ToPostgres_DecimalLiterals_PassThroughInlineCases))]
    [MemberData(nameof(ToPostgres_StringLiterals_PassThroughInlineCases))]
    [MemberData(nameof(ToPostgres_LowerFunction_TranslatesCorrectlyInlineCases))]
    [MemberData(nameof(ToPostgres_UpperFunction_TranslatesCorrectlyInlineCases))]
    [MemberData(nameof(ToPostgres_CoalesceFunction_TranslatesCorrectlyInlineCases))]
    [MemberData(nameof(ToPostgres_LengthFunction_TranslatesCorrectlyInlineCases))]
    [MemberData(nameof(ToPostgres_SubstringWith3Args_UsesFromForSyntax_CasesInlineCases))]
    [MemberData(nameof(ToPostgres_TrimFunction_TranslatesCorrectlyInlineCases))]
    [MemberData(nameof(ToPostgres_ConcatFunction_TranslatesCorrectlyInlineCases))]
    [MemberData(nameof(ToPostgres_AbsFunction_TranslatesCorrectlyInlineCases))]
    [MemberData(nameof(ToPostgres_RoundFunction_TranslatesCorrectlyInlineCases))]
    public void ToPostgres_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToPostgres(input);
        Assert.Equal(expected, result);
    }

    // =========================================================================
    // SQLITE TRANSLATION - same contract as ToPostgres above, different syntax.
    // =========================================================================

    [Theory]
    [MemberData(nameof(ToSqlite_TimestampFunctions_TranslatesCorrectlyInlineCases))]
    [MemberData(nameof(ToSqlite_BooleanLiterals_TranslatesToIntegerValuesInlineCases))]
    [MemberData(nameof(ToSqlite_IntegerLiterals_PassThroughInlineCases))]
    [MemberData(nameof(ToSqlite_DecimalLiterals_PassThroughInlineCases))]
    [MemberData(nameof(ToSqlite_StringLiterals_PassThroughInlineCases))]
    [MemberData(nameof(ToSqlite_LowerFunction_TranslatesCorrectlyInlineCases))]
    [MemberData(nameof(ToSqlite_UpperFunction_TranslatesCorrectlyInlineCases))]
    [MemberData(nameof(ToSqlite_CoalesceFunction_TranslatesCorrectlyInlineCases))]
    [MemberData(nameof(ToSqlite_LengthFunction_TranslatesCorrectlyInlineCases))]
    [MemberData(nameof(ToSqlite_SubstringWith3Args_UsesSubstrFunction_CasesInlineCases))]
    [MemberData(nameof(ToSqlite_TrimFunction_TranslatesCorrectlyInlineCases))]
    [MemberData(nameof(ToSqlite_AbsFunction_TranslatesCorrectlyInlineCases))]
    [MemberData(nameof(ToSqlite_RoundFunction_TranslatesCorrectlyInlineCases))]
    public void ToSqlite_TranslatesCorrectly(string input, string expected)
    {
        var result = LqlDefaultTranslator.ToSqlite(input);
        Assert.Equal(expected, result);
    }

    // =========================================================================
    // UUID FUNCTIONS - gen_uuid(), uuid()
    // =========================================================================

    [Theory]
    [MemberData(nameof(ToPostgres_UuidFunctions_TranslatesToGenRandomUuidInlineCases))]
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
