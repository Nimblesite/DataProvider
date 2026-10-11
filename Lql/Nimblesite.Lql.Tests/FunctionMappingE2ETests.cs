using Nimblesite.Lql.Core.FunctionMapping;
using Nimblesite.Lql.Postgres;
using Nimblesite.Lql.SQLite;
using Nimblesite.Lql.SqlServer;
using Xunit;
using static global::Tests.Shared.StringAssertions;

namespace Nimblesite.Lql.Tests;

/// <summary>
/// E2E tests for function mapping providers across all SQL dialects.
/// Tests function lookup, transpilation, syntax mapping, and special handlers.
/// </summary>
public sealed partial class FunctionMappingE2ETests
{
    private static readonly IFunctionMappingProvider[] AllProviders =
    [
        PostgreSqlFunctionMapping.Instance,
        PostgreSqlFunctionMappingLocal.Instance,
        SqlServerFunctionMapping.Instance,
        SQLiteFunctionMappingLocal.Instance,
    ];

    [Theory]
    [MemberData(nameof(CoreAggregateFunctions_AllProviders_AreMappedInlineCases))]
    public void CoreAggregateFunctions_AllProviders_AreMapped(string functionName)
    {
        foreach (var provider in AllProviders)
        {
            var mapping = provider.GetFunctionMapping(functionName);
            Assert.NotNull(mapping);
            Assert.Equal(functionName, mapping.LqlFunction);
            Assert.NotEmpty(mapping.SqlFunction);
        }
    }

    [Theory]
    [MemberData(nameof(StringFunctions_AllProviders_AreMappedInlineCases))]
    public void StringFunctions_AllProviders_AreMapped(string functionName)
    {
        foreach (var provider in AllProviders)
        {
            var mapping = provider.GetFunctionMapping(functionName);
            Assert.NotNull(mapping);
            Assert.NotEmpty(mapping.SqlFunction);
        }
    }

    [Fact]
    public void CountStar_AllProviders_SpecialHandlerProducesCorrectSQL()
    {
        foreach (var provider in AllProviders)
        {
            var result = provider.TranspileFunction("count", "*");
            Assert.Equal("COUNT(*)", result);
        }
    }

    [Theory]
    // CountWithColumn_AllProviders_ProducesCorrectSQL
    [MemberData(nameof(CountWithColumn_AllProviders_ProducesCorrectSQL_CasesInlineCases))]
    // SumFunction_AllProviders_TranspilesCorrectly

    // UpperFunction_AllProviders_TranspilesCorrectly

    // LowerFunction_AllProviders_TranspilesCorrectly

    // AvgFunction_AllProviders_TranspilesCorrectly

    public void CountWithColumn_AllProviders_ProducesCorrectSQL_Cases(
        string caseValue0,
        string caseValue1,
        string caseValue2,
        string caseValue3
    )
    {
        foreach (var provider in AllProviders)
        {
            var result = provider.TranspileFunction(caseValue0, caseValue1);
            Assert.Contains(caseValue2, result, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(caseValue3, result, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void UnknownFunction_AllProviders_FallsBackToDefault()
    {
        foreach (var provider in AllProviders)
        {
            var result = provider.TranspileFunction("nonexistent_func", "arg1", "arg2");
            Assert.Contains("NONEXISTENT_FUNC", result, StringComparison.OrdinalIgnoreCase);
            ContainsAll(
                actual: result,
                expected: ["arg1", "arg2"],
                comparison: StringComparison.Ordinal
            );
        }
    }

    [Fact]
    public void GetFunctionMapping_NullInput_Throws()
    {
        foreach (var provider in AllProviders)
        {
            Assert.Throws<ArgumentNullException>(() => provider.GetFunctionMapping(null!));
        }
    }

    [Fact]
    public void TranspileFunction_NullFunctionName_Throws()
    {
        foreach (var provider in AllProviders)
        {
            Assert.Throws<ArgumentNullException>(() => provider.TranspileFunction(null!, "arg"));
        }
    }

    [Fact]
    public void CurrentDate_SpecialHandler_ProducesNoParens()
    {
        var pgMapping = PostgreSqlFunctionMapping.Instance.GetFunctionMapping("current_date");
        Assert.NotNull(pgMapping);
        Assert.True(pgMapping.RequiresSpecialHandling);
        Assert.NotNull(pgMapping.SpecialHandler);
        var result = pgMapping.SpecialHandler([]);
        Assert.Equal("CURRENT_DATE", result);

        var pgLocalMapping = PostgreSqlFunctionMappingLocal.Instance.GetFunctionMapping(
            "current_date"
        );
        Assert.NotNull(pgLocalMapping);
        Assert.True(pgLocalMapping.RequiresSpecialHandling);
    }

    [Fact]
    public void SyntaxMapping_AllProviders_HasValidValues()
    {
        foreach (var provider in AllProviders)
        {
            var syntax = provider.GetSyntaxMapping();
            Assert.NotNull(syntax);
            Assert.NotEmpty(syntax.LimitClause);
            Assert.NotEmpty(syntax.OffsetClause);
        }
    }

    [Fact]
    public void FormatLimitClause_AllProviders_ProducesValidSQL()
    {
        foreach (var provider in AllProviders)
        {
            var result = provider.FormatLimitClause("10");
            Assert.NotEmpty(result);
            Assert.Contains("10", result, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void FormatOffsetClause_AllProviders_ProducesValidSQL()
    {
        foreach (var provider in AllProviders)
        {
            var result = provider.FormatOffsetClause("20");
            Assert.NotEmpty(result);
            Assert.Contains("20", result, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void FormatIdentifier_AllProviders_QuotesCorrectly()
    {
        foreach (var provider in AllProviders)
        {
            var result = provider.FormatIdentifier("column_name");
            Assert.Contains("column_name", result, StringComparison.Ordinal);
            Assert.True(result.Length > "column_name".Length, "Should add quote characters");
        }
    }

    [Fact]
    public void PostgreSqlSpecificFunctions_AreMapped()
    {
        var pg = PostgreSqlFunctionMapping.Instance;
        Assert.NotNull(pg.GetFunctionMapping("extract"));
        Assert.NotNull(pg.GetFunctionMapping("date_trunc"));
        Assert.NotNull(pg.GetFunctionMapping("coalesce"));

        var pgLocal = PostgreSqlFunctionMappingLocal.Instance;
        Assert.NotNull(pgLocal.GetFunctionMapping("extract"));
        Assert.NotNull(pgLocal.GetFunctionMapping("date_trunc"));
    }

    [Fact]
    public void SqlServerSpecificFunctions_AreMapped()
    {
        var ss = SqlServerFunctionMapping.Instance;

        // SQL Server maps extract to DATEPART
        var extractMapping = ss.GetFunctionMapping("extract");
        Assert.NotNull(extractMapping);
        Assert.True(extractMapping.RequiresSpecialHandling);
        Assert.NotNull(extractMapping.SpecialHandler);
        var datePartResult = extractMapping.SpecialHandler(["year", "order_date"]);
        Assert.Contains("DATEPART", datePartResult, StringComparison.OrdinalIgnoreCase);

        // SQL Server maps date_trunc specially
        var dateTruncMapping = ss.GetFunctionMapping("date_trunc");
        Assert.NotNull(dateTruncMapping);
    }

    [Fact]
    public void SQLiteSpecificFunctions_AreMapped()
    {
        var sl = SQLiteFunctionMappingLocal.Instance;
        Assert.NotNull(sl.GetFunctionMapping("length"));

        // SQLite maps substring to SUBSTR
        var substrMapping = sl.GetFunctionMapping("substring");
        Assert.NotNull(substrMapping);
        Assert.Equal("SUBSTR", substrMapping.SqlFunction);
        Assert.True(substrMapping.RequiresSpecialHandling);
    }

    [Fact]
    public void CoalesceFunction_AllProviders_TranspilesWithMultipleArgs()
    {
        foreach (var provider in AllProviders)
        {
            var result = provider.TranspileFunction("coalesce", "col1", "col2", "'default'");
            Assert.Contains("COALESCE", result, StringComparison.OrdinalIgnoreCase);
            ContainsAll(
                actual: result,
                expected: ["col1", "col2", "'default'"],
                comparison: StringComparison.Ordinal
            );
        }
    }

    [Fact]
    public void MinMaxFunctions_AllProviders_TranspileCorrectly()
    {
        foreach (var provider in AllProviders)
        {
            var minResult = provider.TranspileFunction("min", "price");
            Assert.Contains("MIN", minResult, StringComparison.OrdinalIgnoreCase);

            var maxResult = provider.TranspileFunction("max", "price");
            Assert.Contains("MAX", maxResult, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FunctionNameCaseInsensitive_AllProviders_FindsMapping()
    {
        foreach (var provider in AllProviders)
        {
            Assert.NotNull(provider.GetFunctionMapping("COUNT"));
            Assert.NotNull(provider.GetFunctionMapping("Count"));
            Assert.NotNull(provider.GetFunctionMapping("count"));
            Assert.NotNull(provider.GetFunctionMapping("SUM"));
            Assert.NotNull(provider.GetFunctionMapping("Sum"));
        }
    }
}
