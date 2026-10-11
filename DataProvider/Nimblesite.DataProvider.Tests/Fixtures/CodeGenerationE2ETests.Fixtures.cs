// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Tests;

public partial class CodeGenerationE2ETests
{
    private static global::Nimblesite.DataProvider.Core.DatabaseTable Fixture_GenerateInsertMethod_WithAllColumnTypes_ProducesCorrectCode_1() =>
        new DatabaseTable
        {
            Name = "Patients",
            Schema = "main",
            Columns = PatientColumns,
        };

    private static global::Nimblesite.DataProvider.Core.DatabaseTable Fixture_GenerateBulkInsertMethod_ProducesValidBatchCode_1() =>
        new DatabaseTable
        {
            Name = "Patients",
            Schema = "main",
            Columns = PatientColumns,
        };

    private static global::Nimblesite.DataProvider.Core.DatabaseTable Fixture_GenerateBulkUpsertMethod_SQLite_ProducesValidUpsertCode_1() =>
        new DatabaseTable
        {
            Name = "Patients",
            Schema = "main",
            Columns = PatientColumns,
        };
}
