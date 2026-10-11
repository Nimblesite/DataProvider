using System.Collections.Immutable;
using System.Data.Common;

namespace Tests.Shared;

// Implements [CI-TEST-SHARED-COMMAND].
internal static class DatabaseCommands
{
    // Implements [CI-TEST-SHARED-COMMAND-PARAMETERS].
    internal static void Execute(
        DbConnection connection,
        Action<DbCommand> configure,
        Func<ImmutableArray<(string Name, object? Value)>>? parameters = null
    )
    {
        using var command = connection.CreateCommand();
        configure(obj: command);
        foreach (var (name, value) in parameters?.Invoke() ?? [])
        {
            Bind(command: command, name: name, value: value);
        }
        command.ExecuteNonQuery();
    }

    internal static void Bind(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
