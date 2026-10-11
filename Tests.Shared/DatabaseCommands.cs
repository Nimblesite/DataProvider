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

    // Implements [CI-TEST-SHARED-READ-COLUMN]. Reads every value of one result
    // column into a list, preserving the caller's original iteration order.
    internal static List<string> ReadStrings(DbCommand command, int ordinal = 0) =>
        ReadAll(command: command, read: reader => reader.GetString(ordinal));

    // Implements [CI-TEST-SHARED-READ-COLUMN]. Projects every row through a
    // reader callback so callers keep their original per-row conversions.
    internal static List<T> ReadAll<T>(DbCommand command, Func<DbDataReader, T> read)
    {
        var values = new List<T>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            values.Add(read(reader));
        }
        return values;
    }
}
