using static global::Tests.Shared.StringAssertions;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// Tests for SqlError record type
/// </summary>
public sealed class SqlErrorTests
{
    [Fact]
    public void SqlError_CanBeCreatedWithMessageOnly()
    {
        // Arrange & Act
        var error = new SqlError("Test error message");

        // Assert
        Assert.Equal("Test error message", error.Message);
        Assert.Null(error.Exception);
    }

    [Fact]
    public void SqlError_CanBeCreatedWithMessageAndException()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");

        // Act
        var error = new SqlError("Test error message", exception);

        // Assert
        Assert.Equal("Test error message", error.Message);
        Assert.Equal(exception, error.Exception);
    }

    [Fact]
    public void SqlError_CanBeCreatedWithMessageAndNullException()
    {
        // Arrange & Act
        var error = new SqlError("Test error message", null);

        // Assert
        Assert.Equal("Test error message", error.Message);
        Assert.Null(error.Exception);
    }

    [Fact]
    public void SqlError_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        var error1 = new SqlError("Test message", exception);
        var error2 = new SqlError("Test message", exception);
        var error3 = new SqlError("Different message", exception);

        // Act & Assert
        Assert.Equal(error1, error2);
        Assert.NotEqual(error1, error3);
    }

    [Fact]
    public void SqlError_ToString_IncludesMessage()
    {
        // Arrange
        var error = new SqlError("Test error message");

        // Act
        var result = error.ToString();

        // Assert
        Assert.Contains("Test error message", result, StringComparison.Ordinal);
    }

    [Fact]
    public void SqlError_ToString_IncludesExceptionWhenPresent()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        var error = new SqlError("Test error message", exception);

        // Act
        var result = error.ToString();

        // Assert
        ContainsAll(
            actual: result,
            expected: ["Test error message", "InvalidOperationException"],
            comparison: StringComparison.Ordinal
        );
    }

    [Fact]
    public void SqlError_WithEmptyMessage_IsValid()
    {
        // Arrange & Act
        var error = new SqlError("");

        // Assert
        Assert.Equal("", error.Message);
        Assert.Null(error.Exception);
    }

    [Fact]
    public void SqlError_Deconstruction_WorksCorrectly()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        var error = new SqlError("Test message", exception);

        // Act
        var (message, ex) = error;

        // Assert
        Assert.Equal("Test message", message);
        Assert.Equal(exception, ex);
    }
}
