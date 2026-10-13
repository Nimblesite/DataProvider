using System.Collections.Immutable;
using System.Data.Common;

namespace Nimblesite.Sync.Core;

// Implements [SYNC-REPOSITORY-SHARED-WRITES].
internal static class SyncCommandExecution
{
    internal static IReadOnlyList<T> ReadRows<T>(DbCommand command, Func<DbDataReader, T> read)
    {
        var rows = ImmutableArray.CreateBuilder<T>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(read(arg: reader));
        }
        return rows.ToImmutable();
    }

    internal static BoolSyncResult Write<TException>(
        DbConnection connection,
        Action<DbCommand> setSql,
        Func<ImmutableArray<(string Name, object Value)>> parameters,
        string failurePrefix
    )
        where TException : Exception =>
        Read<TException, BoolSyncResult>(
            connection: connection,
            setSql: setSql,
            parameters: parameters,
            read: command =>
            {
                command.ExecuteNonQuery();
                return new BoolSyncOk(true);
            },
            failure: exception => new BoolSyncError(
                new SyncErrorDatabase($"{failurePrefix}: {exception.Message}")
            )
        );

    internal static IntSyncResult WriteCount<TException>(
        DbConnection connection,
        Action<DbCommand> setSql,
        Func<ImmutableArray<(string Name, object Value)>> parameters,
        string failurePrefix
    )
        where TException : Exception =>
        Read<TException, IntSyncResult>(
            connection: connection,
            setSql: setSql,
            parameters: parameters,
            read: command => new IntSyncOk(command.ExecuteNonQuery()),
            failure: exception => new IntSyncError(
                new SyncErrorDatabase($"{failurePrefix}: {exception.Message}")
            )
        );

    // Implements [SYNC-REPOSITORY-SHARED-READS].
    internal static SyncLogListResult FetchSyncLogChanges<TConnection, TException>(
        TConnection connection,
        Action<DbCommand> setSql,
        string batchParameterName,
        long fromVersion,
        int batchSize,
        SyncOperation fallback,
        string failurePrefix
    )
        where TConnection : DbConnection
        where TException : Exception =>
        Read<TException, SyncLogListResult>(
            connection: connection,
            setSql: setSql,
            parameters: () => [("@fromVersion", fromVersion), (batchParameterName, batchSize)],
            read: command => new SyncLogListOk(
                ReadRows<SyncLogEntry>(
                    command: command,
                    read: row => SyncLogEntryFactory.Read(reader: row, fallback: fallback)
                )
            ),
            failure: exception => new SyncLogListError(
                new SyncErrorDatabase($"{failurePrefix}: {exception.Message}")
            )
        );

    internal static TResult Read<TException, TResult>(
        DbConnection connection,
        Action<DbCommand> setSql,
        Func<ImmutableArray<(string Name, object Value)>> parameters,
        Func<DbCommand, TResult> read,
        Func<TException, TResult> failure
    )
        where TException : Exception
    {
        try
        {
            using var command = connection.CreateCommand();
            setSql(command);
            foreach (var (name, value) in parameters())
            {
                SyncClientStorage.Bind(command: command, name: name, value: value);
            }
            return read(command);
        }
        catch (TException exception)
        {
            return failure(exception);
        }
    }
}
