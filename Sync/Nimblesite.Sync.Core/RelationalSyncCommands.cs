using System.Collections.Immutable;
using System.Data.Common;
using Microsoft.Extensions.Logging;
using Outcome;

namespace Nimblesite.Sync.Core;

// Implements [SYNC-HTTP-PROVIDERS]. Driver-independent binding and error handling.
internal static class RelationalSyncCommands
{
    internal static Result<T, SyncError> Run<T>(DbConnection connection, Action<DbCommand> configure,
        ImmutableArray<(string Name, object? Value)> parameters, Func<DbCommand, T> execute, ILogger? logger = null)
    {
        try
        {
            using var command = connection.CreateCommand();
            configure(obj: command);
            foreach (var (name, value) in parameters)
                Bind(command: command, name: name, value: value);
            return new Result<T, SyncError>.Ok<T, SyncError>(execute(arg: command));
        }
        catch (Exception exception)
        {
            logger?.LogError(exception, "Sync database operation failed");
            return new Result<T, SyncError>.Error<T, SyncError>(new SyncErrorDatabase(exception.Message));
        }
    }

    internal static void Bind(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(value: parameter);
    }
}
