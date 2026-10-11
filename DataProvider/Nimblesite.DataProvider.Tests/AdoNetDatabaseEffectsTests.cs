using Microsoft.Data.Sqlite;
using Nimblesite.Sql.Model;
using Outcome;

#pragma warning disable CA2007 // No ConfigureAwait in tests
#pragma warning disable CA1849 // Tests can use sync over async

namespace Nimblesite.DataProvider.Tests;

// Implements [CON-SHARED-CORE]. Drives the dialect-agnostic
// AdoNetDatabaseEffects through a real in-memory SQLite connection so the
// happy path, validation branches, and DbException / InvalidOperationException
// catch arms are all exercised.
public sealed partial class AdoNetDatabaseEffectsTests
{
    private static string MapType(Type fieldType, string dataTypeName, bool isNullable) =>
        fieldType == typeof(long) ? (isNullable ? "long?" : "long")
        : fieldType == typeof(string) ? "string"
        : "object";

    private static AdoNetDatabaseEffects CreateSqliteEffects() =>
        new(cs => new SqliteConnection(cs), MapType);

    [Theory]
    // Rejects_null_connection_string
    [MemberData(nameof(Rejects_null_connection_string_CasesInlineCases))]
    // Rejects_whitespace_sql

    // Returns_error_for_invalid_sql

    public async Task Rejects_null_connection_string_Cases(string caseValue0, string caseValue1)
    {
        var effects = CreateSqliteEffects();
        var result = await effects.GetColumnMetadataFromSqlAsync(
            caseValue0,
            caseValue1,
            Array.Empty<ParameterInfo>()
        );
        Assert.True(
            result
                is Result<IReadOnlyList<DatabaseColumn>, SqlError>.Error<
                    IReadOnlyList<DatabaseColumn>,
                    SqlError
                >
        );
    }

    [Fact]
    public async Task Rejects_null_parameters()
    {
        var effects = CreateSqliteEffects();
        var result = await effects.GetColumnMetadataFromSqlAsync(
            "Data Source=:memory:",
            "SELECT 1",
            null!
        );
        Assert.True(
            result
                is Result<IReadOnlyList<DatabaseColumn>, SqlError>.Error<
                    IReadOnlyList<DatabaseColumn>,
                    SqlError
                >
        );
    }

    [Fact]
    public async Task Reads_columns_from_simple_select()
    {
        var effects = CreateSqliteEffects();
        var result = await effects.GetColumnMetadataFromSqlAsync(
            "Data Source=:memory:",
            "SELECT 1 AS id, 'x' AS name",
            Array.Empty<ParameterInfo>()
        );
        Assert.True(
            result
                is Result<IReadOnlyList<DatabaseColumn>, SqlError>.Ok<
                    IReadOnlyList<DatabaseColumn>,
                    SqlError
                >
        );
        var ok = (Result<IReadOnlyList<DatabaseColumn>, SqlError>.Ok<
            IReadOnlyList<DatabaseColumn>,
            SqlError
        >)result;
        Assert.Equal(2, ok.Value.Count);
        Assert.Equal("id", ok.Value[0].Name);
        Assert.Equal("name", ok.Value[1].Name);
    }

    [Fact]
    public async Task Binds_dummy_parameters_and_reads_back()
    {
        var effects = CreateSqliteEffects();
        var parameters = new[] { new ParameterInfo("id"), new ParameterInfo("limit") };
        var result = await effects.GetColumnMetadataFromSqlAsync(
            "Data Source=:memory:",
            "SELECT @id AS matched_id, @limit AS matched_limit",
            parameters
        );
        Assert.True(
            result
                is Result<IReadOnlyList<DatabaseColumn>, SqlError>.Ok<
                    IReadOnlyList<DatabaseColumn>,
                    SqlError
                >
        );
        var ok = (Result<IReadOnlyList<DatabaseColumn>, SqlError>.Ok<
            IReadOnlyList<DatabaseColumn>,
            SqlError
        >)result;
        Assert.Equal(2, ok.Value.Count);
        Assert.Contains(ok.Value, c => c.Name == "matched_id");
        Assert.Contains(ok.Value, c => c.Name == "matched_limit");
    }

    [Fact]
    public async Task Returns_error_when_open_throws_invalid_operation()
    {
        // Read-only mode against a non-existent file path causes a failure
        // through the InvalidOperationException catch arm.
        var effects = new AdoNetDatabaseEffects(cs => new SqliteConnection(cs), MapType);
        var result = await effects.GetColumnMetadataFromSqlAsync(
            "Data Source=/nonexistent/path/that/does/not/exist.db;Mode=ReadOnly",
            "SELECT 1",
            Array.Empty<ParameterInfo>()
        );
        Assert.True(
            result
                is Result<IReadOnlyList<DatabaseColumn>, SqlError>.Error<
                    IReadOnlyList<DatabaseColumn>,
                    SqlError
                >
        );
    }
}
