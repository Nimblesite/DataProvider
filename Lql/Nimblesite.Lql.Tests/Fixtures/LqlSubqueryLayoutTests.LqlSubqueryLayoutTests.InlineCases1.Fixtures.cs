// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Lql.Tests;

public partial class LqlSubqueryLayoutTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ExistsBody_WithTwoFilters_KeepsBothAndGroupsEachInlineCases()
    {
        yield return ["SQLite"];
        yield return ["PostgreSql"];
        yield return ["SqlServer"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> TwoCtes_JoinedAndPaged_RenderPerDialectPagingInlineCases()
    {
        yield return ["SQLite", "", "ORDER BY a.id ASC\nLIMIT 5"];
        yield return ["PostgreSql", "", "ORDER BY a.id ASC\nLIMIT 5"];
        yield return ["SqlServer", "TOP 5 ", "ORDER BY a.id ASC"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> DerivedTableJoin_WithOffsetAndLimit_RendersPagingAfterJoinInlineCases()
    {
        yield return ["SQLite", "LIMIT 5\nOFFSET 10"];
        yield return ["PostgreSql", "LIMIT 5\nOFFSET 10"];
        yield return ["SqlServer", "OFFSET 10 ROWS\nFETCH NEXT 5 ROWS ONLY"];
    }
}
