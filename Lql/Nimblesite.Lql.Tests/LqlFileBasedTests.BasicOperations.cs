using Xunit;

namespace Nimblesite.Lql.Tests;

/// <summary>
/// File-based tests for basic LQL operations - select, filter, simple joins
/// </summary>
public partial class LqlFileBasedTests
{
    [Theory]
    [MemberData(nameof(BasicOperations_FileBasedTest_ShouldTransformCorrectlyInlineCases))]
    public void BasicOperations_FileBasedTest_ShouldTransformCorrectly(
        string testCaseName,
        string dialect
    ) => ExecuteFileBasedTest(testCaseName, dialect);
}
