// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Tests;

public partial class NonQueryCodeGenerationTests
{
    /// <summary>
    /// Data-only case matrix preserving each original non-query fact name,
    /// its inputs and its expected generated fragments.
    /// </summary>
    public static IEnumerable<object?[]> GenerateNonQueryMethod_CasesData()
    {
        yield return
        [
            "GenerateNonQueryMethod_UpdateStatement_GeneratesCorrectExtension",
            "RevokeSessionExtensions",
            "RevokeSession",
            "UPDATE gk_session SET is_revoked = 1 WHERE id = @jti",
            new List<ParameterInfo> { new("jti", "TEXT") },
            new[]
            {
                "public static partial class RevokeSessionExtensions",
                "public static async Task<Result<int, SqlError>> RevokeSessionAsync",
                "this SqliteConnection connection",
                "object? jti",
                "ExecuteNonQueryAsync",
                "UPDATE gk_session SET is_revoked = 1 WHERE id = @jti",
            },
            false,
        ];

        yield return
        [
            "GenerateNonQueryMethod_DeleteStatement_GeneratesCorrectExtension",
            "PurgeSyncLogExtensions",
            "PurgeSyncLog",
            "DELETE FROM sync_log WHERE created_at < @cutoff",
            new List<ParameterInfo> { new("cutoff", "TEXT") },
            new[] { "PurgeSyncLogAsync", "DELETE FROM sync_log", "@cutoff" },
            false,
        ];

        yield return
        [
            "GenerateNonQueryMethod_MultipleParameters_GeneratesAllParameters",
            "UpdateUserExtensions",
            "UpdateUser",
            "UPDATE users SET name = @name, email = @email WHERE id = @id",
            new List<ParameterInfo>
            {
                new("name", "TEXT"),
                new("email", "TEXT"),
                new("id", "INTEGER"),
            },
            new[]
            {
                "object? name",
                "object? email",
                "object? id",
                "AddWithValue(\"@name\"",
                "AddWithValue(\"@email\"",
                "AddWithValue(\"@id\"",
            },
            false,
        ];

        yield return
        [
            "GenerateNonQueryMethod_NoParameters_GeneratesMethodWithoutParams",
            "ClearTempDataExtensions",
            "ClearTempData",
            "DELETE FROM temp_data",
            new List<ParameterInfo>(),
            new[] { "ClearTempDataAsync(this SqliteConnection connection)" },
            true,
        ];
    }
}
