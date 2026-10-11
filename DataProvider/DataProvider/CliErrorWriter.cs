// Implements [DP-CLI-SHARED-ERROR-REPORT].
namespace DataProvider;

/// <summary>
/// Shared helpers for surfacing code generation failures to MSBuild. Both the
/// Postgres and SQLite pipelines report the diagnostic on the console and emit
/// an <c>#error</c> source file so the compile fails loudly.
/// </summary>
internal static class CliErrorWriter
{
    internal static async Task ReportAsync(
        DirectoryInfo outDir,
        string baseName,
        string sqlPath,
        string code,
        string reason,
        string message
    )
    {
        Console.WriteLine($"❌ {message}");
        Console.Error.WriteLine($"{sqlPath}(1,1): error {code}: {message}");
        await WriteCompileTimeErrorFileAsync(outDir, baseName, sqlPath, reason, message)
            .ConfigureAwait(false);
    }

    internal static async Task ReportUnexpectedAsync(
        DirectoryInfo outDir,
        string sqlPath,
        string code,
        Exception exception
    )
    {
        Console.WriteLine($"❌ Error processing {sqlPath}: {exception.Message}");
        Console.Error.WriteLine($"{sqlPath}(1,1): error {code}: {exception.Message}");
        var baseName = Path.GetFileNameWithoutExtension(sqlPath);
        if (baseName.EndsWith(".generated", StringComparison.OrdinalIgnoreCase))
        {
            baseName = baseName[..^".generated".Length];
        }
        await WriteCompileTimeErrorFileAsync(
                outDir,
                baseName,
                sqlPath,
                reason: "unexpected error",
                message: exception.Message
            )
            .ConfigureAwait(false);
    }

    internal static async Task WriteCompileTimeErrorFileAsync(
        DirectoryInfo outDir,
        string baseName,
        string sqlPath,
        string reason,
        string message
    )
    {
        var errorFile = Path.Combine(outDir.FullName, baseName + ".g.cs");
        var content =
            $"// Auto-generated due to {reason} in {sqlPath}\n#error {EscapeForPreprocessor(message)}\n";
        await File.WriteAllTextAsync(errorFile, content).ConfigureAwait(false);
    }

    internal static string EscapeForPreprocessor(string message)
    {
        if (string.IsNullOrEmpty(message))
            return string.Empty;
        // Preprocessor directives cannot span multiple lines; replace newlines and quotes
        var oneLine = message.Replace('\r', ' ').Replace('\n', ' ');
        return oneLine.Replace('"', '\'');
    }
}
