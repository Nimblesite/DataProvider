namespace Tests.Shared;

// Implements [CI-TEST-SHARED-COMMAND]. Applies an ordered sequence of schema
// and seed statements to a live SQLite connection. Statement text stays with
// each caller's data fixture; this helper only holds the execution loop so the
// per-class setup methods keep their original ordering.
internal static class SqliteSchemaSetup
{
    internal static void Apply(
        DbConnection connection,
        params Action<DbCommand>[] configureStatements
    )
    {
        foreach (var configure in configureStatements)
        {
            DatabaseCommands.Execute(connection: connection, configure: configure);
        }
    }
}
