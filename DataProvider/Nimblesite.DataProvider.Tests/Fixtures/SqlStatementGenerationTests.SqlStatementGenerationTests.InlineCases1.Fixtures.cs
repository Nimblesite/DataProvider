// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Tests;

public partial class SqlStatementGenerationTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> AllDialects_SimpleSelectFromSingleTable_GeneratesExpectedSqlInlineCases()
    {
        yield return ["sqlite"];
        yield return ["postgres"];
        yield return ["sqlserver"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> AllDialects_SelectAllWildcard_WhenNoColumnsSelected_GeneratesStarInlineCases()
    {
        yield return ["sqlite"];
        yield return ["postgres"];
        yield return ["sqlserver"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> AllDialects_WithWhereComparison_FormatsConditionInlineCases()
    {
        yield return ["sqlite"];
        yield return ["postgres"];
        yield return ["sqlserver"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> AllDialects_WithWhereLogicalOperators_FormatsSequenceInlineCases()
    {
        yield return ["sqlite"];
        yield return ["postgres"];
        yield return ["sqlserver"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> AllDialects_WithParenthesesInWhere_FormatsParensInlineCases()
    {
        yield return ["sqlite"];
        yield return ["postgres"];
        yield return ["sqlserver"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> AllDialects_WithJoin_OutputsInnerJoinInlineCases()
    {
        yield return ["sqlite"];
        yield return ["postgres"];
        yield return ["sqlserver"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> AllDialects_WithGroupByAndHaving_OutputsClausesInlineCases()
    {
        yield return ["sqlite"];
        yield return ["postgres"];
        yield return ["sqlserver"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> AllDialects_WithOrderBy_OutputsOrderByInlineCases()
    {
        yield return ["sqlite"];
        yield return ["postgres"];
        yield return ["sqlserver"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> AllDialects_WithDistinctAndPaging_OutputsDistinctLimitOffsetInlineCases()
    {
        yield return ["sqlite"];
        yield return ["postgres"];
        yield return ["sqlserver"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> AllDialects_WithWildcardAndAlias_FormatsCorrectlyInlineCases()
    {
        yield return ["sqlite"];
        yield return ["postgres"];
        yield return ["sqlserver"];
    }
}
