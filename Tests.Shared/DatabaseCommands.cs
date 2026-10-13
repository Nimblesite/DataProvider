using System.Collections.Immutable;

namespace Tests.Shared;

// Implements [CI-TEST-SHARED-COMMAND].
internal static class DatabaseCommands
{
    // Implements [CI-TEST-SHARED-COMMAND-PARAMETERS].
    internal static void Execute(
        global::System.Data.Common.DbConnection connection,
        Action<global::System.Data.Common.DbCommand> configure,
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

    internal static void Bind(
        global::System.Data.Common.DbCommand command,
        string name,
        object? value
    )
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    // Implements [CI-TEST-SHARED-SCALAR-QUERY].
    internal static object? Scalar(
        global::System.Data.Common.DbConnection connection,
        Action<global::System.Data.Common.DbCommand> configure,
        Func<ImmutableArray<(string Name, object? Value)>>? parameters = null
    )
    {
        using var command = connection.CreateCommand();
        configure(obj: command);
        foreach (var (name, value) in parameters?.Invoke() ?? [])
        {
            Bind(command: command, name: name, value: value);
        }
        return command.ExecuteScalar();
    }

    // Implements [CI-TEST-SHARED-READ-FIRST-ROW]. Executes the configured query
    // and advances to the first row, preserving the original read assertion.
    internal static global::System.Data.Common.DbDataReader ReadFirstRow(
        global::System.Data.Common.DbCommand command,
        Action<global::System.Data.Common.DbCommand> configure
    )
    {
        configure(obj: command);
        var reader = command.ExecuteReader();
        Xunit.Assert.True(reader.Read());
        return reader;
    }

    // Implements [CI-TEST-SHARED-READ-COLUMN]. Reads every value of one result
    // column into a list, preserving the caller's original iteration order.
    internal static List<string> ReadStrings(
        global::System.Data.Common.DbCommand command,
        int ordinal = 0
    ) => ReadAll(command: command, read: reader => reader.GetString(ordinal));

    // Implements [CI-TEST-SHARED-READ-COLUMN]. Projects every row through a
    // reader callback so callers keep their original per-row conversions.
    internal static List<T> ReadAll<T>(
        global::System.Data.Common.DbCommand command,
        Func<global::System.Data.Common.DbDataReader, T> read
    )
    {
        var values = new List<T>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            values.Add(read(reader));
        }
        return values;
    }

    // Implements [CI-TEST-SHARED-SCALAR-QUERY].
    internal static int ScalarInt32(
        global::System.Data.Common.DbConnection connection,
        Action<global::System.Data.Common.DbCommand> configure,
        Func<ImmutableArray<(string Name, object? Value)>>? parameters = null
    ) =>
        Convert.ToInt32(
            value: Scalar(connection: connection, configure: configure, parameters: parameters),
            provider: global::System.Globalization.CultureInfo.InvariantCulture
        );

    // Implements [CI-TEST-SHARED-SCALAR-QUERY].
    internal static long ScalarInt64(
        global::System.Data.Common.DbConnection connection,
        Action<global::System.Data.Common.DbCommand> configure,
        Func<ImmutableArray<(string Name, object? Value)>>? parameters = null
    ) =>
        Convert.ToInt64(
            value: Scalar(connection: connection, configure: configure, parameters: parameters),
            provider: global::System.Globalization.CultureInfo.InvariantCulture
        );

    // Implements [CI-TEST-SHARED-SCALAR-QUERY].
    internal static double ScalarDouble(
        global::System.Data.Common.DbConnection connection,
        Action<global::System.Data.Common.DbCommand> configure,
        Func<ImmutableArray<(string Name, object? Value)>>? parameters = null
    ) =>
        Convert.ToDouble(
            value: Scalar(connection: connection, configure: configure, parameters: parameters),
            provider: global::System.Globalization.CultureInfo.InvariantCulture
        );
}
