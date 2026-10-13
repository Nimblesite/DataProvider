// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaYamlSerializerTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Data_ToYamlFile_WritesValidYaml_1() =>
        Schema
            .Define("file_write_test")
            .Table("Table1", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();
}
