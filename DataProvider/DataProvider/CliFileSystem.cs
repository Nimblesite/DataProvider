// Implements [DP-CLI-SHARED-FILE-GUARDS].
namespace DataProvider;

/// <summary>
/// Shared input/output guards for the code generation subcommands: report a
/// missing configuration file the same way, and materialise the output
/// directory before anything is written into it.
/// </summary>
internal static class CliFileSystem
{
    internal static bool EnsureConfigExists(FileInfo configFile)
    {
        if (configFile.Exists)
        {
            return true;
        }

        Console.WriteLine($"❌ Config not found: {configFile.FullName}");
        return false;
    }

    internal static void EnsureDirectory(DirectoryInfo directory)
    {
        if (!directory.Exists)
        {
            directory.Create();
        }
    }
}
