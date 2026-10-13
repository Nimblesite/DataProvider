// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaYamlSerializerTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> VectorType_PostgresDdl_EmitsPgVectorType_ContextCasesData()
    {
        {
            global::System.Func<global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition> input0 =
                Fixture_VectorType_PostgresDdl_EmitsPgVectorType_1;
            yield return
            [
                "VectorType_PostgresDdl_EmitsPgVectorType",
                input0,
                new global::System.Func<
                    global::Nimblesite.DataProvider.Migration.Core.SchemaOperation,
                    global::System.String
                >(PostgresDdlGenerator.Generate),
                "\"Embedding\" vector(768) NOT NULL",
            ];
        }
        {
            global::System.Func<global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition> input0 =
                Fixture_VectorType_SqliteDdl_FallsBackToBlob_1;
            yield return
            [
                "VectorType_SqliteDdl_FallsBackToBlob",
                input0,
                new global::System.Func<
                    global::Nimblesite.DataProvider.Migration.Core.SchemaOperation,
                    global::System.String
                >(SqliteDdlGenerator.Generate),
                "[Embedding] BLOB NOT NULL",
            ];
        }
    }
}
