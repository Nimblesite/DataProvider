// Implements [CI-DESLOP-INDEPENDENT-DATA].
using System.Collections.Immutable;
using Nimblesite.Sql.Model;

namespace Nimblesite.DataProvider.Tests;

public partial class CodeGenerationE2ETests
{
    private global::Nimblesite.DataProvider.Core.DatabaseTable Fixture_GenerateInsertMethod_WithAllColumnTypes_ProducesCorrectCode_1() =>
        new DatabaseTable
        {
            Name = "Patients",
            Schema = "main",
            Columns = PatientColumns,
        };

    private global::Nimblesite.DataProvider.Core.DatabaseTable Fixture_GenerateBulkInsertMethod_ProducesValidBatchCode_1() =>
        new DatabaseTable
        {
            Name = "Patients",
            Schema = "main",
            Columns = PatientColumns,
        };

    private global::Nimblesite.DataProvider.Core.DatabaseTable Fixture_GenerateBulkUpsertMethod_SQLite_ProducesValidUpsertCode_1() =>
        new DatabaseTable
        {
            Name = "Patients",
            Schema = "main",
            Columns = PatientColumns,
        };
}
