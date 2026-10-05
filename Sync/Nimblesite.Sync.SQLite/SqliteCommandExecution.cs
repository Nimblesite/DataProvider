using Microsoft.Data.Sqlite;

namespace Nimblesite.Sync.SQLite;

// Implements [SYNC-SCALAR-READ-SHARED].
internal static class SqliteCommandExecution
{
    /// <summary>
    /// Creates, configures, and executes a command, converting SqliteException
    /// into the supplied error result. Shared by all repository operations.
    /// </summary>
    internal static TResult Execute<TResult>(
        SqliteConnection connection,
        Action<SqliteCommand> setSql,
        (string Name, object Value)[] parameters,
        Func<SqliteCommand, TResult> execute,
        Func<string, TResult> onError
    )
    {
        try
        {
            using var cmd = connection.CreateCommand();
            setSql(cmd);
            foreach (var (name, value) in parameters)
            {
                cmd.Parameters.AddWithValue(parameterName: name, value: value);
            }

            return execute(cmd);
        }
        catch (SqliteException ex)
        {
            return onError(ex.Message);
        }
    }

    internal static LongSyncResult ReadInt64OrZero(
        SqliteConnection connection,
        Action<SqliteCommand> setSql,
        string failurePrefix
    ) =>
        Execute<LongSyncResult>(
            connection: connection,
            setSql: setSql,
            parameters: [],
            execute: command => new LongSyncOk(command.ExecuteScalar() is long value ? value : 0),
            onError: message => new LongSyncError(
                new SyncErrorDatabase($"{failurePrefix}: {message}")
            )
        );
}
