using Xunit;

namespace Nimblesite.Lql.Tests;

/// <summary>
/// File-based tests for LQL join operations
/// </summary>
public partial class LqlFileBasedTests
{
    [Theory]
    [MemberData(nameof(Joins_FileBasedTest_ShouldTransformCorrectlyInlineCases))]
    public void Joins_FileBasedTest_ShouldTransformCorrectly(string testCaseName, string dialect) =>
        ExecuteFileBasedTest(testCaseName, dialect);
}
