using System.CommandLine;
using Nimblesite.Lql.SQLite;

namespace Lql;

/// <summary>
/// <c>sqlite</c> subcommand of the unified <c>Lql</c> tool. Transpiles
/// LQL to SQLite SQL. Built via <see cref="BuildCommand"/> and attached
/// to the root command by <see cref="Program"/>.
/// </summary>
internal static class SqliteCli
{
    /// <summary>
    /// Builds the <c>sqlite</c> subcommand. Exposed so <see cref="Program"/>
    /// can wire it onto the root command.
    /// </summary>
    /// <returns>Configured <see cref="Command"/>.</returns>
    public static Command BuildCommand() =>
        TranspileRunner.BuildCommand(
            name: "sqlite",
            platformLabel: "SQLite",
            transpile: statement => statement.ToSQLite()
        );
}
