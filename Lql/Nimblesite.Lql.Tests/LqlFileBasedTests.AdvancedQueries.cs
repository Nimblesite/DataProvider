using Xunit;

namespace Nimblesite.Lql.Tests;

/// <summary>
/// File-based tests for advanced LQL features - subqueries, CTEs, window functions, etc.
/// </summary>
public partial class LqlFileBasedTests
{
    [Theory]
    [MemberData(nameof(AdvancedQueries_FileBasedTest_ShouldTransformCorrectlyInlineCases))]
    public void AdvancedQueries_FileBasedTest_ShouldTransformCorrectly(
        string testCaseName,
        string dialect
    ) => ExecuteFileBasedTest(testCaseName, dialect);
}
