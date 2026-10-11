using System.CommandLine;

// Implements [DP-CLI-SHARED-OPTIONS].
namespace DataProvider;

/// <summary>
/// Shared construction of the required CLI options that every codegen
/// subcommand accepts.
/// </summary>
internal static class CliOptions
{
    internal static Option<T> Required<T>(string name, string description) =>
        new Option<T>(name, description: description) { IsRequired = true };
}
