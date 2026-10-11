// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Tests;

public partial class BulkOperationsTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> GenerateBulkInsertMethod_WithValidTable_ReturnsSuccess_CasesInlineCases()
    {
        yield return
        [
            "BulkInsertProductsAsync",
            "IEnumerable<(",
            "INSERT INTO Products",
            "batchSize",
        ];
        yield return
        [
            "try",
            "catch (Exception ex)",
            "Result<int, SqlError>.Error",
            "Bulk insert failed",
        ];
        yield return ["Guid Id", "string Name", "decimal Price", "string Category"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> GenerateBulkInsertMethod_GeneratesBatchHelper_CasesInlineCases()
    {
        yield return ["ExecuteBulkInsertProductsBatchAsync", "StringBuilder", "parameters.Add"];
        yield return ["@p", "AddWithValue", "DBNull.Value"];
        yield return ["async Task<Result<int, SqlError>>", "await", "ConfigureAwait(false)"];
    }
}
