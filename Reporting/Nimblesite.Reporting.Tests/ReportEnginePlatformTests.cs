using System.Collections.Immutable;
using System.Data;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Nimblesite.DataProvider.Migration.Core;
using Nimblesite.DataProvider.Migration.Tests;
using Nimblesite.Reporting.Engine;
using Nimblesite.Sql.Model;
using Npgsql;
using Xunit;
using ConnError = Outcome.Result<System.Data.IDbConnection, Nimblesite.Sql.Model.SqlError>.Error<
    System.Data.IDbConnection,
    Nimblesite.Sql.Model.SqlError
>;
using ConnOk = Outcome.Result<System.Data.IDbConnection, Nimblesite.Sql.Model.SqlError>.Ok<
    System.Data.IDbConnection,
    Nimblesite.Sql.Model.SqlError
>;
using ConnResult = Outcome.Result<System.Data.IDbConnection, Nimblesite.Sql.Model.SqlError>;
using EngineOk = Outcome.Result<
    Nimblesite.Reporting.Engine.ReportExecutionResult,
    Nimblesite.Sql.Model.SqlError
>.Ok<Nimblesite.Reporting.Engine.ReportExecutionResult, Nimblesite.Sql.Model.SqlError>;
using TranspileError = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>.Error<
    string,
    Nimblesite.Sql.Model.SqlError
>;

namespace Nimblesite.Reporting.Tests;

/// <summary>Checks report execution against real SQLite, PostgreSQL, and SQL Server databases.</summary>
[Collection(ReportEnginePlatformSuite.Name)]
public sealed record ReportEnginePlatformTests
{
    private readonly MigrationPostgresContainerFixture _postgres;
    private readonly SqlServerContainerFixture _sqlServer;

    /// <summary>Uses the shared container fixtures for database isolation.</summary>
    public ReportEnginePlatformTests(
        MigrationPostgresContainerFixture postgres,
        SqlServerContainerFixture sqlServer
    )
    {
        _postgres = postgres;
        _sqlServer = sqlServer;
    }

    // Implements [MIG-TEST-CROSS-PLATFORM]: real SQL execution on every backend.
    /// <summary>Runs the same SQL report twice against each supported database.</summary>
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public async Task Execute_LiveSqlSourceReturnsSameExactRowOnRepeatedCalls(string provider)
    {
        if (provider == "postgres")
        {
            await AssertPostgresExecutionAsync().ConfigureAwait(true);
            return;
        }
        if (provider == "sqlserver")
        {
            await AssertSqlServerExecutionAsync().ConfigureAwait(true);
            return;
        }
        Assert.Equal("sqlite", provider);
        AssertSqliteExecution();
    }

    private async Task AssertPostgresExecutionAsync()
    {
        using var connection = await _postgres
            .CreateDatabaseAsync("report_engine_platform")
            .ConfigureAwait(false);
        AssertRepeatedExecution("postgres", connection.ConnectionString);
    }

    private async Task AssertSqlServerExecutionAsync()
    {
        var connectionString = await _sqlServer
            .CreateDatabaseConnectionStringAsync()
            .ConfigureAwait(false);
        AssertRepeatedExecution("sqlserver", connectionString);
    }

    private static void AssertSqliteExecution()
    {
        var path = Path.Combine(Path.GetTempPath(), $"report_engine_{Guid.NewGuid():N}.db");
        try
        {
            AssertRepeatedExecution("sqlite", $"Data Source={path}");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void AssertRepeatedExecution(string provider, string connectionString)
    {
        MigratePlatformDatabase(provider, connectionString);
        var report = PlatformReport();
        var opened = 0;
        ConnResult CreateConnection(string reference) =>
            OpenConnection(provider, connectionString, reference, () => opened++);

        var first = ExecuteReport(report, CreateConnection);
        AssertExactReport(first);
        var second = ExecuteReport(report, CreateConnection);
        AssertExactReport(second);
        Assert.Equal(2, opened);
    }

    private static ReportDefinition PlatformReport() =>
        new(
            Id: "platform-report",
            Title: "Platform Report",
            Parameters: [],
            DataSources: [PlatformSource()],
            Layout: new LayoutDefinition(Columns: 12, Rows: [])
        );

    private static DataSourceDefinition PlatformSource() =>
        new(
            Id: "probe",
            Type: DataSourceType.Sql,
            ConnectionRef: "platform-db",
            Query: "SELECT 7 AS metric_value, 'ready' AS state",
            Url: null,
            Method: null,
            Headers: null,
            Parameters: []
        );

    private static ConnResult OpenConnection(
        string provider,
        string connectionString,
        string connectionRef,
        Action onOpened
    )
    {
        Assert.Equal("platform-db", connectionRef);
        try
        {
            IDbConnection connection = provider switch
            {
                "sqlite" => new SqliteConnection(connectionString),
                "postgres" => new NpgsqlConnection(connectionString),
                "sqlserver" => new Microsoft.Data.SqlClient.SqlConnection(connectionString),
                _ => new SqliteConnection(connectionString),
            };
            connection.Open();
            onOpened();
            return new ConnOk(connection);
        }
        catch (Exception error)
        {
            return new ConnError(SqlError.FromException(error));
        }
    }

    private static EngineOk ExecuteReport(
        ReportDefinition report,
        Func<string, ConnResult> connectionFactory
    ) =>
        Assert.IsType<EngineOk>(
            ReportEngine.Execute(
                report: report,
                parameters: ImmutableDictionary<string, string>.Empty,
                connectionFactory: connectionFactory,
                lqlTranspiler: _ => new TranspileError(SqlError.Create("unexpected LQL source")),
                logger: NullLogger.Instance
            )
        );

    private static void AssertExactReport(EngineOk result)
    {
        Assert.Equal("platform-report", result.Value.ReportId);
        Assert.NotEqual(default, result.Value.ExecutedAt);
        var source = Assert.Single(result.Value.DataSources);
        Assert.Equal("probe", source.Key);
        Assert.Equal(["metric_value", "state"], source.Value.ColumnNames);
        Assert.Equal(1, source.Value.TotalRows);
        var row = Assert.Single(source.Value.Rows);
        Assert.Equal(2, row.Length);
        Assert.Equal(7L, Convert.ToInt64(row[0], CultureInfo.InvariantCulture));
        Assert.Equal("ready", row[1]?.ToString());
    }

    private static void MigratePlatformDatabase(string provider, string connectionString)
    {
        var output =
            provider == "sqlite"
                ? new SqliteConnectionStringBuilder(connectionString).DataSource
                : connectionString;
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, SchemaYamlSerializer.ToYaml(PlatformSchema(provider)));
            var exitCode = DataProviderMigrate.Program.Main([
                "migrate",
                "--schema",
                path,
                "--provider",
                provider,
                "--output",
                output,
            ]);
            Assert.Equal(0, exitCode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static SchemaDefinition PlatformSchema(string provider) =>
        new()
        {
            Name = "report_engine_platform",
            Tables =
            [
                new TableDefinition
                {
                    Schema = provider switch
                    {
                        "sqlite" => "main",
                        "postgres" => "public",
                        _ => "dbo",
                    },
                    Name = "platform_probe",
                    Columns =
                    [
                        new Nimblesite.DataProvider.Migration.Core.ColumnDefinition
                        {
                            Name = "id",
                            Type = PortableTypes.Uuid,
                            IsNullable = false,
                        },
                    ],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                },
            ],
        };
}
