using System.Data.Common;
using Nimblesite.DataProvider.Core.CodeGeneration;
using Nimblesite.Sql.Model;
using ColumnMetadataError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.DataProvider.Core.DatabaseColumn>,
    Nimblesite.Sql.Model.SqlError
>.Error<
    System.Collections.Generic.IReadOnlyList<Nimblesite.DataProvider.Core.DatabaseColumn>,
    Nimblesite.Sql.Model.SqlError
>;
using ColumnMetadataOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.DataProvider.Core.DatabaseColumn>,
    Nimblesite.Sql.Model.SqlError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.DataProvider.Core.DatabaseColumn>,
    Nimblesite.Sql.Model.SqlError
>;

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial record MigrationPlatformRegressionTests
{
    // Implements [CON-SHARED-CORE] and [MIG-TEST-CROSS-PLATFORM].
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public async Task AdoNetEffects_InspectsLiveQueriesAndReportsInvalidSql(string provider)
    {
        await WithAsyncTargetAsync(provider, AssertAdoNetEffectsAsync).ConfigureAwait(true);
    }

    private static async Task AssertAdoNetEffectsAsync(MigrationTarget target)
    {
        var effects = CreateEffects(target.Provider);
        var connectionString =
            target.Provider == "sqlite"
                ? new SqliteConnectionStringBuilder { DataSource = target.Output }.ConnectionString
                : target.Output;
        var first = await effects
            .GetColumnMetadataFromSqlAsync(
                connectionString,
                "SELECT 1 AS first_value, 'x' AS second_value",
                Array.Empty<ParameterInfo>()
            )
            .ConfigureAwait(false);
        var columns = Assert.IsType<ColumnMetadataOk>(first).Value;
        Assert.Equal(2, columns.Count);
        Assert.Equal("first_value", columns[0].Name);
        Assert.Equal("second_value", columns[1].Name);
        Assert.NotEqual(columns[0].Name, columns[1].Name);

        var again = await effects
            .GetColumnMetadataFromSqlAsync(
                connectionString,
                "SELECT 1 AS first_value, 'x' AS second_value",
                Array.Empty<ParameterInfo>()
            )
            .ConfigureAwait(false);
        Assert.Equal(
            columns.Select(column => column.Name),
            Assert.IsType<ColumnMetadataOk>(again).Value.Select(column => column.Name)
        );

        var parameterized = await effects
            .GetColumnMetadataFromSqlAsync(
                connectionString,
                "SELECT @id AS selected_id, @limit AS selected_limit",
                [new ParameterInfo("id"), new ParameterInfo("limit")]
            )
            .ConfigureAwait(false);
        var parameterColumns = Assert.IsType<ColumnMetadataOk>(parameterized).Value;
        Assert.Equal(2, parameterColumns.Count);
        Assert.Equal("selected_id", parameterColumns[0].Name);
        Assert.Equal("selected_limit", parameterColumns[1].Name);

        var invalid = await effects
            .GetColumnMetadataFromSqlAsync(
                connectionString,
                "SELECT * FROM metadata_table_that_does_not_exist",
                Array.Empty<ParameterInfo>()
            )
            .ConfigureAwait(false);
        var error = Assert.IsType<ColumnMetadataError>(invalid);
        Assert.False(string.IsNullOrWhiteSpace(error.Value.Message));
        Assert.Contains(
            "metadata_table_that_does_not_exist",
            error.Value.Message,
            StringComparison.OrdinalIgnoreCase
        );

        var blankSql = await effects
            .GetColumnMetadataFromSqlAsync(connectionString, "   ", Array.Empty<ParameterInfo>())
            .ConfigureAwait(false);
        Assert.False(
            string.IsNullOrWhiteSpace(Assert.IsType<ColumnMetadataError>(blankSql).Value.Message)
        );
        var blankConnection = await effects
            .GetColumnMetadataFromSqlAsync("", "SELECT 1 AS id", Array.Empty<ParameterInfo>())
            .ConfigureAwait(false);
        Assert.False(
            string.IsNullOrWhiteSpace(
                Assert.IsType<ColumnMetadataError>(blankConnection).Value.Message
            )
        );
    }

    private static AdoNetDatabaseEffects CreateEffects(string provider)
    {
        Assert.True(provider is "sqlite" or "postgres" or "sqlserver");
        Func<string, DbConnection> connectionFactory = provider switch
        {
            "sqlite" => connectionString => new SqliteConnection(connectionString),
            "postgres" => connectionString => new NpgsqlConnection(connectionString),
            "sqlserver" => connectionString => new Microsoft.Data.SqlClient.SqlConnection(
                connectionString
            ),
            _ => connectionString => new SqliteConnection(connectionString),
        };
        return new AdoNetDatabaseEffects(
            connectionFactory,
            (fieldType, _, isNullable) => isNullable ? $"{fieldType.Name}?" : fieldType.Name
        );
    }
}
