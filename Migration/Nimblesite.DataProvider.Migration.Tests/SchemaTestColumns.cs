namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [MIG-TEST-ID-NAME-COLUMNS].
internal static class SchemaTestColumns
{
    internal static TableBuilder IdAndName(
        TableBuilder table,
        PortableType idType,
        PortableType nameType
    ) =>
        table
            .Column(name: "Id", type: idType, configure: column => column.PrimaryKey())
            .Column(name: "Name", type: nameType, configure: column => column.NotNull());
}
