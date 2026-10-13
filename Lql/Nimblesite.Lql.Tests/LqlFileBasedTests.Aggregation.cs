namespace Nimblesite.Lql.Tests;

/// <summary>
/// File-based tests for all LQL operations and dialects.
/// </summary>
public partial class LqlFileBasedTests
{
    [Theory]
    [MemberData(nameof(Aggregation_FileBasedTest_ShouldTransformCorrectlyInlineCases))]
    [MemberData(nameof(AdvancedQueries_FileBasedTest_ShouldTransformCorrectlyInlineCases))]
    [MemberData(nameof(Joins_FileBasedTest_ShouldTransformCorrectlyInlineCases))]
    [MemberData(nameof(Arithmetic_FileBasedTest_ShouldTransformCorrectlyInlineCases))]
    [MemberData(nameof(BasicOperations_FileBasedTest_ShouldTransformCorrectlyInlineCases))]
    public void FileBasedTest_ShouldTransformCorrectly(string testCaseName, string dialect) =>
        ExecuteFileBasedTest(testCaseName, dialect);
}
