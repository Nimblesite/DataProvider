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
    /// Tests every input-file workflow: console transpilation, validation mode,
    /// and error handling for missing or empty files. Each case carries its own
    /// original test name, input, expected exit code, and output expectations.
    /// </summary>
    /// <param name="caseName">Original test name for the scenario.</param>
    /// <param name="inputContent">Content written to the temporary input file.</param>
    /// <param name="createInputFile">Whether the input file exists at all.</param>
    /// <param name="extraArgs">Additional CLI arguments appended after the input option.</param>
    /// <param name="expectedExitCode">Expected process exit code.</param>
    /// <param name="expectedFragments">Fragments that must appear in the CLI output.</param>
    /// <param name="forbiddenFragments">Fragments that must not appear in the CLI output.</param>
    [Theory]
    [MemberData(nameof(InputFileWorkflowCases))]
    public async Task InputFileWorkflow_ReportsExpectedExitCodeAndOutput(
        string caseName,
        string inputContent,
        bool createInputFile,
        string[] extraArgs,
        int expectedExitCode,
        string[] expectedFragments,
        string[] forbiddenFragments
    )
    {
        Assert.False(string.IsNullOrEmpty(caseName));

        // Arrange
        var inputFile = createInputFile
            ? CreateTempFile(inputContent)
            : Path.Combine(_tempDirectory, "nonexistent.lql");

        // Act
        var result = await RunCliAsync(["--input", inputFile, .. extraArgs]);

        // Assert
        Assert.Equal(expectedExitCode, result.ExitCode);

        foreach (var fragment in expectedFragments)
        {
            Assert.Contains(fragment, result.Output);
        }

        foreach (var forbidden in forbiddenFragments)
        {
            Assert.DoesNotContain(forbidden, result.Output);
        }
    }

    /// <summary>
    /// Tests file-output workflows, including creating missing output
    /// directories, and verifies the exact SQL that is written.
    /// </summary>
    /// <param name="caseName">Original test name for the scenario.</param>
    /// <param name="lql">LQL input written to the temporary input file.</param>
    /// <param name="createOutputSubdirectory">Whether the output directory is nested.</param>
    /// <param name="outputSubdirectory">Name of the nested output directory.</param>
    /// <param name="assertWrittenMessage">Whether the CLI must report the written file.</param>
    /// <param name="expectedFileContent">Exact SQL expected in the written file.</param>
    [Theory]
    [MemberData(nameof(FileOutputWorkflowCases))]
    public async Task FileOutputWorkflow_WritesExpectedSQLFile(
        string caseName,
        string lql,
        bool createOutputSubdirectory,
        string outputSubdirectory,
        bool assertWrittenMessage,
        string expectedFileContent
    )
    {
        Assert.False(string.IsNullOrEmpty(caseName));

        // Arrange
        var inputFile = CreateTempFile(lql);
        var outputDirectory = createOutputSubdirectory
            ? Path.Combine(_tempDirectory, outputSubdirectory)
            : _tempDirectory;
        var outputFile = Path.Combine(outputDirectory, "output.sql");

        // Act
        var result = await RunCliAsync("--input", inputFile, "--output", outputFile);

        // Assert
        Assert.Equal(0, result.ExitCode);

        if (assertWrittenMessage)
        {
            Assert.Contains($"SQLite SQL written to: {outputFile}", result.Output);
        }

        Assert.True(File.Exists(outputFile));

        var sqlContent = await File.ReadAllTextAsync(outputFile);
        Assert.Equal(expectedFileContent, sqlContent);
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

        process.OutputDataReceived += AppendLineTo(outputBuilder);
        process.ErrorDataReceived += AppendLineTo(errorBuilder);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync().ConfigureAwait(false);

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();

        // Combine stdout and stderr for easier testing
        var combinedOutput = string.IsNullOrEmpty(error) ? output : $"{output}\n{error}";

        return new ProcessResult(process.ExitCode, combinedOutput.Trim());

        static DataReceivedEventHandler AppendLineTo(StringBuilder builder) =>
            (_, e) =>
            {
                if (e.Data != null)
                    builder.AppendLine(e.Data);
            };
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
