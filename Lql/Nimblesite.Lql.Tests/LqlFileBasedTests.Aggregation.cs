using Xunit;

namespace Nimblesite.Lql.Tests;

/// <summary>
/// File-based tests for LQL aggregation operations - group by, having, etc.
/// </summary>
public partial class LqlFileBasedTests
{
    [Theory]
    [MemberData(nameof(Aggregation_FileBasedTest_ShouldTransformCorrectlyInlineCases))]
    public void Aggregation_FileBasedTest_ShouldTransformCorrectly(
        string testCaseName,
        string dialect
    ) => ExecuteFileBasedTest(testCaseName, dialect);
}
