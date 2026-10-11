using Xunit;

namespace Nimblesite.Lql.Tests;

/// <summary>
/// File-based tests for LQL arithmetic operations and expressions
/// </summary>
public partial class LqlFileBasedTests
{
    [Theory]
    [MemberData(nameof(Arithmetic_FileBasedTest_ShouldTransformCorrectlyInlineCases))]
    public void Arithmetic_FileBasedTest_ShouldTransformCorrectly(
        string testCaseName,
        string dialect
    ) => ExecuteFileBasedTest(testCaseName, dialect);
}
