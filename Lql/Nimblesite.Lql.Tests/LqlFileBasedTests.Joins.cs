using Xunit;

namespace Nimblesite.Lql.Tests;

/// <summary>
/// File-based tests for LQL join operations
/// </summary>
public partial class LqlFileBasedTests
{
    [Theory]
    [InlineData("join_simple", "PostgreSql")]
    [InlineData("join_simple", "SqlServer")]
    [InlineData("join_simple", "SQLite")]
    [InlineData("join_multiple", "PostgreSql")]
    [InlineData("join_multiple", "SqlServer")]
    [InlineData("join_multiple", "SQLite")]
    [InlineData("join_left", "PostgreSql")]
    [InlineData("join_left", "SqlServer")]
    [InlineData("join_left", "SQLite")]
    [InlineData("complex_join_union", "PostgreSql")]
    [InlineData("complex_join_union", "SqlServer")]
    [InlineData("complex_join_union", "SQLite")]
    public void Joins_FileBasedTest_ShouldTransformCorrectly(string testCaseName, string dialect) =>
        ExecuteFileBasedTest(testCaseName, dialect);
}
