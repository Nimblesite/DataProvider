using System.Diagnostics;
using System.Text;
using Xunit;

#pragma warning disable CA1307
#pragma warning disable CA1707

namespace Nimblesite.Lql.Cli.SQLite.Tests;

/// <summary>
/// End-to-end tests for the LQL CLI tool
/// </summary>
public sealed partial class CliEndToEndTests : IDisposable
{
    private static readonly string[] SubcommandOnly = ["sqlite"];
    private static readonly string[] SubcommandHelp = ["sqlite", "--help"];

    private readonly string _tempDirectory;
    private readonly string _cliPath;

    /// <summary>
    /// Initializes a new instance of the CliEndToEndTests class
    /// </summary>
    public CliEndToEndTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"lql-cli-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);

        // Use the built executable directly from the test's bin directory
        // This avoids the slow and potentially hanging 'dotnet run --project' approach
        _cliPath = Path.Combine(AppContext.BaseDirectory, "Lql");
    }

    /// <summary>
    /// Tests simple LQL to SQLite conversion with console output
    /// </summary>
    [Fact]
    public async Task TranspileSimpleSelect_ToConsole_ReturnsCorrectSQL()
    {
        // Arrange
        var inputFile = CreateTempFile("users |> select(users.id, users.name, users.email)");

        // Act
        var result = await RunCliAsync("--input", inputFile);

        // Assert
        Assert.Equal(0, result.ExitCode);

        Assert.Contains("SELECT users.id, users.name, users.email FROM users", result.Output);

        Assert.Contains("Generated SQLite SQL:", result.Output);
    }

    /// <summary>
    /// Tests LQL to SQLite conversion with file output
    /// </summary>
    [Fact]
    public async Task TranspileSimpleSelect_ToFile_CreatesCorrectSQLFile()
    {
        // Arrange
        var inputFile = CreateTempFile("users |> select(users.id, users.name, users.email)");
        var outputFile = Path.Combine(_tempDirectory, "output.sql");

        // Act
        var result = await RunCliAsync("--input", inputFile, "--output", outputFile);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains($"SQLite SQL written to: {outputFile}", result.Output);
        Assert.True(File.Exists(outputFile));

        var sqlContent = await File.ReadAllTextAsync(outputFile);
        Assert.Equal("SELECT users.id, users.name, users.email FROM users", sqlContent);
    }

    /// <summary>
    /// Tests LQL validation mode
    /// </summary>
    [Fact]
    public async Task ValidateMode_WithValidLQL_ReturnsSuccess()
    {
        // Arrange
        var inputFile = CreateTempFile("users |> select(users.id, users.name)");

        // Act
        var result = await RunCliAsync("--input", inputFile, "--validate");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("LQL syntax is valid", result.Output);
        Assert.DoesNotContain("Generated SQLite SQL:", result.Output);
    }

    /// <summary>
    /// Tests complex LQL with filtering
    /// </summary>
    [Fact]
    public async Task TranspileWithFilter_ToConsole_ReturnsCorrectSQL()
    {
        // Arrange
        var lql = Data_TranspileWithFilter_ToConsole_ReturnsCorrectSQL_1;
        var inputFile = CreateTempFile(lql);

        // Act
        var result = await RunCliAsync("--input", inputFile);

        // Assert
        Assert.Equal(0, result.ExitCode);
        var expectedSQL =
            "SELECT employees.id, employees.name, employees.salary FROM employees WHERE employees.salary > 50000 AND employees.salary < 100000";
        Assert.Contains(expectedSQL, result.Output);
    }

    /// <summary>
    /// Tests error handling for non-existent input file
    /// </summary>
    [Fact]
    public async Task NonExistentInputFile_ReturnsError()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_tempDirectory, "nonexistent.lql");

        // Act
        var result = await RunCliAsync("--input", nonExistentFile);

        // Assert
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("does not exist", result.Output);
    }

    /// <summary>
    /// Tests error handling for empty input file
    /// </summary>
    [Fact]
    public async Task EmptyInputFile_ReturnsError()
    {
        // Arrange
        var inputFile = CreateTempFile("");

        // Act
        var result = await RunCliAsync("--input", inputFile);

        // Assert
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("is empty", result.Output);
    }

    /// <summary>
    /// Tests help display
    /// </summary>
    [Fact]
    public async Task HelpOption_DisplaysUsageInformation()
    {
        // Act
        var result = await RunCliAsync("--help");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("LQL to SQLite SQL transpiler", result.Output);
        Assert.Contains("--input", result.Output);
        Assert.Contains("--output", result.Output);
        Assert.Contains("--validate", result.Output);
    }

    /// <summary>
    /// Tests required input parameter validation
    /// </summary>
    [Fact]
    public async Task NoInputParameter_ReturnsError()
    {
        // Act
        var result = await RunCliAsync();

        // Assert
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Option '--input' is required", result.Output);
    }

    /// <summary>
    /// Tests aggregation query
    /// </summary>
    [Fact]
    public async Task TranspileAggregation_ToConsole_ReturnsCorrectSQL()
    {
        // Arrange - This is based on the test data files
        var lql = Data_TranspileAggregation_ToConsole_ReturnsCorrectSQL_1;
        var inputFile = CreateTempFile(lql);

        // Act
        var result = await RunCliAsync("--input", inputFile);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("SELECT", result.Output);
        Assert.Contains("FROM orders", result.Output);
        Assert.Contains("GROUP BY", result.Output);
    }

    /// <summary>
    /// Tests that output file is created in correct directory
    /// </summary>
    [Fact]
    public async Task OutputFileInSubdirectory_CreatesDirectoryAndFile()
    {
        // Arrange
        var inputFile = CreateTempFile("users |> select(users.id)");
        var subDir = Path.Combine(_tempDirectory, "subdir");
        var outputFile = Path.Combine(subDir, "output.sql");

        // Act
        var result = await RunCliAsync("--input", inputFile, "--output", outputFile);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(outputFile));

        var sqlContent = await File.ReadAllTextAsync(outputFile);
        Assert.Equal("SELECT users.id FROM users", sqlContent);
    }

    /// <summary>
    /// Creates a temporary file with the given content
    /// </summary>
    /// <param name="content">File content</param>
    /// <returns>Path to the created file</returns>
    private string CreateTempFile(string content)
    {
        var fileName = Path.Combine(_tempDirectory, $"test-{Guid.NewGuid():N}.lql");
        File.WriteAllText(fileName, content);
        return fileName;
    }

    /// <summary>
    /// Runs the CLI with the given arguments
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Process result with exit code and output</returns>
    private async Task<ProcessResult> RunCliAsync(params string[] args)
    {
        using var process = new Process();
        process.StartInfo.FileName = _cliPath;
        // All tests target the unified Lql tool's `sqlite` subcommand.
        // --help and the no-args invocation are intentionally exercised
        // against the subcommand, not the root, to preserve prior behavior.
        var prefixed =
            args.Length == 0 ? SubcommandOnly
            : args is ["--help"] ? SubcommandHelp
            : SubcommandOnly.Concat(args).ToArray();
        process.StartInfo.Arguments = string.Join(
            " ",
            prefixed.Select(arg => arg.Contains(' ') ? $"\"{arg}\"" : arg)
        );

        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
                outputBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync().ConfigureAwait(false);

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();

        // Combine stdout and stderr for easier testing
        var combinedOutput = string.IsNullOrEmpty(error) ? output : $"{output}\n{error}";

        return new ProcessResult(process.ExitCode, combinedOutput.Trim());
    }

    /// <summary>
    /// Cleans up temporary files
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}

/// <summary>
/// Represents the result of running a process
/// </summary>
/// <param name="ExitCode">The exit code</param>
/// <param name="Output">The combined output</param>
internal sealed record ProcessResult(int ExitCode, string Output);
