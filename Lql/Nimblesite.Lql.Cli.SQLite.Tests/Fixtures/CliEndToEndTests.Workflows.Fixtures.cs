// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Lql.Cli.SQLite.Tests;

/// <summary>
/// Data-only scenario rows for the CLI end-to-end workflow theories. Each row
/// preserves the original test name, inputs, expected exit code, and output
/// expectations; assertions live in the analysed test file.
/// </summary>
public partial class CliEndToEndTests
{
    /// <summary>
    /// Input-file workflow scenarios: console transpilation, validation, and errors.
    /// </summary>
    public static IEnumerable<object?[]> InputFileWorkflowCases()
    {
        yield return
        [
            "TranspileSimpleSelect_ToConsole_ReturnsCorrectSQL",
            "users |> select(users.id, users.name, users.email)",
            true,
            Array.Empty<string>(),
            0,
            new[]
            {
                "SELECT users.id, users.name, users.email FROM users",
                "Generated SQLite SQL:",
            },
            Array.Empty<string>(),
        ];
        yield return
        [
            "ValidateMode_WithValidLQL_ReturnsSuccess",
            "users |> select(users.id, users.name)",
            true,
            new[] { "--validate" },
            0,
            new[] { "LQL syntax is valid" },
            new[] { "Generated SQLite SQL:" },
        ];
        yield return
        [
            "TranspileWithFilter_ToConsole_ReturnsCorrectSQL",
            Data_TranspileWithFilter_ToConsole_ReturnsCorrectSQL_1,
            true,
            Array.Empty<string>(),
            0,
            new[]
            {
                "SELECT employees.id, employees.name, employees.salary FROM employees WHERE employees.salary > 50000 AND employees.salary < 100000",
            },
            Array.Empty<string>(),
        ];
        yield return
        [
            "NonExistentInputFile_ReturnsError",
            "",
            false,
            Array.Empty<string>(),
            1,
            new[] { "does not exist" },
            Array.Empty<string>(),
        ];
        yield return
        [
            "EmptyInputFile_ReturnsError",
            "",
            true,
            Array.Empty<string>(),
            1,
            new[] { "is empty" },
            Array.Empty<string>(),
        ];
        yield return
        [
            "TranspileAggregation_ToConsole_ReturnsCorrectSQL",
            Data_TranspileAggregation_ToConsole_ReturnsCorrectSQL_1,
            true,
            Array.Empty<string>(),
            0,
            new[] { "SELECT", "FROM orders", "GROUP BY" },
            Array.Empty<string>(),
        ];
    }

    /// <summary>
    /// File-output workflow scenarios, including a nested output directory.
    /// </summary>
    public static IEnumerable<object?[]> FileOutputWorkflowCases()
    {
        yield return
        [
            "TranspileSimpleSelect_ToFile_CreatesCorrectSQLFile",
            "users |> select(users.id, users.name, users.email)",
            false,
            "",
            true,
            "SELECT users.id, users.name, users.email FROM users",
        ];
        yield return
        [
            "OutputFileInSubdirectory_CreatesDirectoryAndFile",
            "users |> select(users.id)",
            true,
            "subdir",
            false,
            "SELECT users.id FROM users",
        ];
    }
}
