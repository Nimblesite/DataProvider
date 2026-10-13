// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Tests;

public partial class CodeGenerationExtendedE2ETests
{
    private static global::Nimblesite.DataProvider.Core.CodeGeneration.CodeGenerationConfig Dataset_DefaultConfig_SourceFile_RejectsInvalidInput_1() =>
        new CodeGenerationConfig(
            getColumnMetadata: Nimblesite
                .DataProvider
                .SQLite
                .SqliteCodeGenerator
                .GetColumnMetadataFromSqlAsync
        );
}
