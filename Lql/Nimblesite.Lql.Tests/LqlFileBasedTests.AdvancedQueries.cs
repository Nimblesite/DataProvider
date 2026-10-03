using Xunit;

namespace Nimblesite.Lql.Tests;

/// <summary>
/// File-based tests for advanced LQL features - subqueries, CTEs, window functions, etc.
/// </summary>
public partial class LqlFileBasedTests
{
    [Theory]
    [InlineData("window_function", "PostgreSql")]
    [InlineData("window_function", "SqlServer")]
    [InlineData("window_function", "SQLite")]
    [InlineData("subquery_nested", "PostgreSql")]
    [InlineData("subquery_nested", "SqlServer")]
    [InlineData("subquery_nested", "SQLite")]
    [InlineData("cte_with", "PostgreSql")]
    [InlineData("cte_with", "SqlServer")]
    [InlineData("cte_with", "SQLite")]
    [InlineData("exists_subquery", "PostgreSql")]
    [InlineData("exists_subquery", "SqlServer")]
    [InlineData("exists_subquery", "SQLite")]
    [InlineData("in_subquery", "PostgreSql")]
    [InlineData("in_subquery", "SqlServer")]
    [InlineData("in_subquery", "SQLite")]
    public void AdvancedQueries_FileBasedTest_ShouldTransformCorrectly(
        string testCaseName,
        string dialect
    ) => ExecuteFileBasedTest(testCaseName, dialect);
}
