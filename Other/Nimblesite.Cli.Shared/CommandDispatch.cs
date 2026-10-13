using System.CommandLine;

namespace Nimblesite.Cli.Shared;

/// <summary>
/// Shared entry-point plumbing for the platform CLIs. Builds a root command from
/// an ordered set of platform subcommand factories and dispatches the arguments,
/// preserving the original collection-initializer ordering.
/// </summary>
// Implements [CI-CLI-SHARED-DISPATCH].
internal static class CommandDispatch
{
    /// <summary>
    /// Builds a root command with the supplied description and ordered subcommands,
    /// then invokes it with the supplied arguments.
    /// </summary>
    /// <param name="description">Root command description.</param>
    /// <param name="args">Command line arguments.</param>
    /// <param name="commandFactories">Ordered subcommand factories.</param>
    /// <returns>The process exit code.</returns>
    internal static Task<int> Run(
        string description,
        string[] args,
        params Func<Command>[] commandFactories
    )
    {
        var root = new RootCommand(description);
        foreach (var commandFactory in commandFactories)
        {
            root.Add(commandFactory());
        }

        return root.InvokeAsync(args);
    }
}
