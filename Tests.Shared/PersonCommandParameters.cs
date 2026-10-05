using System.Data.Common;

namespace Tests.Shared;

// Implements [SYNC-TEST-PERSON-PARAMETERS].
internal static class PersonCommandParameters
{
    internal static void Execute(DbCommand command, string id, string name, string email)
    {
        Bind(command: command, name: "@id", value: id);
        Bind(command: command, name: "@name", value: name);
        Bind(command: command, name: "@email", value: email);
        command.ExecuteNonQuery();
    }

    private static void Bind(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
