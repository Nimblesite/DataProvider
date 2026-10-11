using System.Data.Common;
namespace Tests.Shared;

// Implements [SYNC-TEST-PERSON-PARAMETERS].
internal static class PersonCommandParameters
{
    internal static void Execute(DbCommand command, string id, string name, string email)
    {
        DatabaseCommands.Bind(command: command, name: "@id", value: id);
        DatabaseCommands.Bind(command: command, name: "@name", value: name);
        DatabaseCommands.Bind(command: command, name: "@email", value: email);
        command.ExecuteNonQuery();
    }
}
