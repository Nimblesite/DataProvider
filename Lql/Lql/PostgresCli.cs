using System.CommandLine;
using Nimblesite.Lql.Postgres;

namespace Lql;

/// <summary>
/// <c>postgres</c> subcommand of the unified <c>Lql</c> tool. Transpiles
/// LQL to PostgreSQL SQL. Built via <see cref="BuildCommand"/> and
/// attached to the root command by <see cref="Program"/>.
/// </summary>
internal static class PostgresCli
{
    /// <summary>
    /// Builds the <c>postgres</c> subcommand. Exposed so <see cref="Program"/>
    /// can wire it onto the root command.
    /// </summary>
    /// <returns>Configured <see cref="Command"/>.</returns>
    public static Command BuildCommand() =>
        TranspileRunner.BuildCommand(
            name: "postgres",
            platformLabel: "PostgreSQL",
            transpile: statement => statement.ToPostgreSql()
        );
}
