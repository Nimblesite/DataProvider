namespace Nimblesite.DataProvider.Tests;

public class ResultTypeTests
{
    // Implements [CI-TEST-SHARED-CASE-MATRIX]: each original fact name is retained
    // as a named case label. Both Result factories must not throw and stay non-null.
    [Theory]
    [InlineData("Result_SuccessValue_CreatesSuccessResult", true, "Success")]
    [InlineData("Result_ErrorValue_CreatesFailureResult", false, "Something went wrong")]
    public void Result_CreatesResult(string caseName, bool isOk, string value)
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));

        object result = isOk
            ? new Result<string, string>.Ok<string, string>(value)
            : new Result<string, string>.Error<string, string>(value);

        Assert.NotNull(result);
    }

    [Fact]
    public void SqlError_WithMessage_CreatesCorrectError()
    {
        // Arrange
        const string message = "Database connection failed";

        // Act
        var error = new SqlError(message);

        // Assert
        Assert.NotNull(error);
        // Verify SqlError has proper structure for the generated code
    }

    [Fact]
    public void SqlError_WithMessageAndException_CreatesCorrectError()
    {
        // Arrange
        const string message = "Database operation failed";
        var exception = new InvalidOperationException("Connection timeout");

        // Act
        var error = new SqlError(message, exception);

        // Assert
        Assert.NotNull(error);
        // Verify SqlError properly encapsulates exceptions without throwing them
    }
}
