using System.Collections.Immutable;
using System.Data.Common;
using BoolSyncError = Outcome.Result<bool, Nimblesite.Sync.Core.SyncError>.Error<
    bool,
    Nimblesite.Sync.Core.SyncError
>;
using BoolSyncOk = Outcome.Result<bool, Nimblesite.Sync.Core.SyncError>.Ok<
    bool,
    Nimblesite.Sync.Core.SyncError
>;
using BoolSyncResult = Outcome.Result<bool, Nimblesite.Sync.Core.SyncError>;
using IntSyncError = Outcome.Result<int, Nimblesite.Sync.Core.SyncError>.Error<
    int,
    Nimblesite.Sync.Core.SyncError
>;
using IntSyncOk = Outcome.Result<int, Nimblesite.Sync.Core.SyncError>.Ok<
    int,
    Nimblesite.Sync.Core.SyncError
>;
using IntSyncResult = Outcome.Result<int, Nimblesite.Sync.Core.SyncError>;

namespace Nimblesite.Sync.Core;

// Implements [SYNC-REPOSITORY-SHARED-WRITES].
internal static class SyncCommandExecution
{
    internal static BoolSyncResult Write<TException>(
        DbConnection connection,
        Action<DbCommand> setSql,
        Func<ImmutableArray<(string Name, object Value)>> parameters,
        string failurePrefix
    )
        where TException : Exception =>
        Execute<TException, BoolSyncResult>(
            connection: connection,
            setSql: setSql,
            parameters: parameters,
            success: _ => new BoolSyncOk(true),
            failure: message => new BoolSyncError(
                new SyncErrorDatabase($"{failurePrefix}: {message}")
            )
        );

    internal static IntSyncResult WriteCount<TException>(
        DbConnection connection,
        Action<DbCommand> setSql,
        Func<ImmutableArray<(string Name, object Value)>> parameters,
        string failurePrefix
    )
        where TException : Exception =>
        Execute<TException, IntSyncResult>(
            connection: connection,
            setSql: setSql,
            parameters: parameters,
            success: count => new IntSyncOk(count),
            failure: message => new IntSyncError(
                new SyncErrorDatabase($"{failurePrefix}: {message}")
            )
        );

    private static TResult Execute<TException, TResult>(
        DbConnection connection,
        Action<DbCommand> setSql,
        Func<ImmutableArray<(string Name, object Value)>> parameters,
        Func<int, TResult> success,
        Func<string, TResult> failure
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
            return success(command.ExecuteNonQuery());
        }
        catch (TException exception)
        {
            return failure(exception.Message);
        }
    }
}
