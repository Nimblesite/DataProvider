using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nimblesite.DataProvider.Migration.Core;
using Nimblesite.DataProvider.Migration.Tests;

namespace Nimblesite.Reporting.Integration.Tests;

[Collection(ReportingPlatformCollection.Name)]
[Trait("Category", "E2E")]
public sealed record ReportingPlatformApiTests
{
    private readonly MigrationPostgresContainerFixture _postgres;
    private readonly SqlServerContainerFixture _sqlServer;

    public ReportingPlatformApiTests(
        MigrationPostgresContainerFixture postgres,
        SqlServerContainerFixture sqlServer
    )
    {
        _postgres = postgres;
        _sqlServer = sqlServer;
    }

    // Implements [MIG-TEST-CROSS-PLATFORM]: one HTTP report journey on real databases.
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public Task ReportApi_ListsGetsExecutesAndRejectsMissingReports(string provider) =>
        WithProviderAsync(provider, RunJourneyAsync);

    // Implements [REPORT-AUTH-BEARER] and [MIG-TEST-CROSS-PLATFORM].
    [Theory]
    [InlineData("sqlite", null)]
    [InlineData("postgres", null)]
    [InlineData("sqlserver", null)]
    [InlineData("sqlite", "invalid-token")]
    [InlineData("postgres", "invalid-token")]
    [InlineData("sqlserver", "invalid-token")]
    public Task ReportApi_RejectsMissingAndInvalidBearerTokens(string provider, string? token) =>
        WithProviderAsync(
            provider,
            (selectedProvider, connectionString) =>
                AssertAuthenticationAsync(selectedProvider, connectionString, token)
        );

    private async Task WithProviderAsync(string provider, Func<string, string, Task> run)
    {
        if (provider == "postgres")
        {
            using var connection = await _postgres
                .CreateDatabaseAsync("reporting_platform")
                .ConfigureAwait(true);
            await run(provider, connection.ConnectionString).ConfigureAwait(true);
            return;
        }
        if (provider == "sqlserver")
        {
            var connectionString = await _sqlServer
                .CreateDatabaseConnectionStringAsync()
                .ConfigureAwait(true);
            await run(provider, connectionString).ConfigureAwait(true);
            return;
        }
        Assert.Equal("sqlite", provider);
        var path = Path.Combine(Path.GetTempPath(), $"reporting_platform_{Guid.NewGuid():N}.db");
        try
        {
            await run(provider, $"Data Source={path}").ConfigureAwait(true);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task AssertAuthenticationAsync(
        string provider,
        string connectionString,
        string? token
    )
    {
        MigratePlatformDatabase(provider, connectionString);
        using var host = CreateHost(connectionString);
        await host.StartAsync().ConfigureAwait(false);
        try
        {
            var server = host.Services.GetRequiredService<IServer>();
            var addresses = Assert.IsAssignableFrom<IServerAddressesFeature>(
                server.Features.Get<IServerAddressesFeature>()
            );
            var apiUrl = Assert.Single(addresses.Addresses);
            using var client = new HttpClient();
            if (token is not null)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    token
                );
            }
            await AssertUnauthorizedEndpointsAsync(client, apiUrl).ConfigureAwait(false);
        }
        finally
        {
            await host.StopAsync().ConfigureAwait(false);
        }
    }

    private static async Task AssertUnauthorizedEndpointsAsync(HttpClient client, string apiUrl)
    {
        using var list = await client.GetAsync($"{apiUrl}/api/reports").ConfigureAwait(false);
        using var metadata = await client
            .GetAsync($"{apiUrl}/api/reports/platform-contract")
            .ConfigureAwait(false);
        using var request = new StringContent(
            """{"parameters": {}, "format": "json"}""",
            Encoding.UTF8,
            "application/json"
        );
        using var execution = await client
            .PostAsync($"{apiUrl}/api/reports/platform-contract/execute", request)
            .ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, metadata.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, execution.StatusCode);
    }

    private static async Task RunJourneyAsync(string provider, string connectionString)
    {
        MigratePlatformDatabase(provider, connectionString);
        using var host = CreateHost(connectionString);
        await host.StartAsync().ConfigureAwait(false);
        try
        {
            var server = host.Services.GetRequiredService<IServer>();
            var addresses = Assert.IsAssignableFrom<IServerAddressesFeature>(
                server.Features.Get<IServerAddressesFeature>()
            );
            var apiUrl = Assert.Single(addresses.Addresses);
            using var client = new HttpClient();
            await AssertReportJourneyAsync(client, apiUrl).ConfigureAwait(false);
        }
        finally
        {
            await host.StopAsync().ConfigureAwait(false);
        }
    }

    private static void MigratePlatformDatabase(string provider, string connectionString)
    {
        var output =
            provider == "sqlite"
                ? new SqliteConnectionStringBuilder(connectionString).DataSource
                : connectionString;
        var schema = new SchemaDefinition
        {
            Name = "reporting_platform",
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
                        new ColumnDefinition
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
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, SchemaYamlSerializer.ToYaml(schema));
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

    private static IHost CreateHost(string connectionString)
    {
        var reportsDirectory = Path.Combine(AppContext.BaseDirectory, "PlatformReports");
        Assert.True(Directory.Exists(reportsDirectory));
        return Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls("http://127.0.0.1:0");
                webBuilder.UseSetting("ConnectionStrings:reporting-db", connectionString);
                webBuilder.UseSetting("ReportsDirectory", reportsDirectory);
                webBuilder.UseStartup<ReportingApiStartup>();
            })
            .Build();
    }

    private static async Task AssertReportJourneyAsync(HttpClient client, string apiUrl)
    {
        using var listResponse = await client
            .GetAsync($"{apiUrl}/api/reports")
            .ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal("application/json", listResponse.Content.Headers.ContentType?.MediaType);
        using var list = JsonDocument.Parse(
            await listResponse.Content.ReadAsStringAsync().ConfigureAwait(false)
        );
        var listed = Assert.Single(list.RootElement.EnumerateArray());
        Assert.Equal("platform-contract", listed.GetProperty("id").GetString());
        Assert.Equal("Platform Contract Report", listed.GetProperty("title").GetString());
        Assert.Equal(1, listed.GetProperty("dataSourceIds").GetArrayLength());
        Assert.Equal("platformProbe", listed.GetProperty("dataSourceIds")[0].GetString());
        Assert.Equal(12, listed.GetProperty("layout").GetProperty("columns").GetInt32());
        Assert.Empty(listed.GetProperty("parameters").EnumerateArray());

        using var metadataResponse = await client
            .GetAsync($"{apiUrl}/api/reports/platform-contract")
            .ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);
        var metadataJson = await metadataResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var metadata = JsonDocument.Parse(metadataJson);
        Assert.Equal("platform-contract", metadata.RootElement.GetProperty("id").GetString());
        Assert.Equal(
            "Platform Contract Report",
            metadata.RootElement.GetProperty("title").GetString()
        );
        Assert.Equal(
            12,
            metadata.RootElement.GetProperty("layout").GetProperty("columns").GetInt32()
        );
        Assert.Equal(1, metadata.RootElement.GetProperty("dataSourceIds").GetArrayLength());
        Assert.Empty(metadata.RootElement.GetProperty("parameters").EnumerateArray());
        Assert.DoesNotContain("connectionRef", metadataJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Data Source", metadataJson, StringComparison.Ordinal);

        var firstRows = await ExecuteAndReadRowsAsync(client, apiUrl).ConfigureAwait(false);
        var secondRows = await ExecuteAndReadRowsAsync(client, apiUrl).ConfigureAwait(false);
        Assert.Equal(firstRows, secondRows);
        using var missing = await client
            .GetAsync($"{apiUrl}/api/reports/does-not-exist")
            .ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var missingRequest = new StringContent(
            """{"parameters": {}, "format": "json"}""",
            Encoding.UTF8,
            "application/json"
        );
        using var missingExecution = await client
            .PostAsync($"{apiUrl}/api/reports/does-not-exist/execute", missingRequest)
            .ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.NotFound, missingExecution.StatusCode);
    }

    private static async Task<string> ExecuteAndReadRowsAsync(HttpClient client, string apiUrl)
    {
        using var request = new StringContent(
            """{"parameters": {}, "format": "json"}""",
            Encoding.UTF8,
            "application/json"
        );
        using var response = await client
            .PostAsync($"{apiUrl}/api/reports/platform-contract/execute", request)
            .ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var result = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync().ConfigureAwait(false)
        );
        Assert.Equal("platform-contract", result.RootElement.GetProperty("reportId").GetString());
        Assert.True(
            DateTimeOffset.TryParse(result.RootElement.GetProperty("executedAt").GetString(), out _)
        );
        var dataSources = result.RootElement.GetProperty("dataSources");
        Assert.Single(dataSources.EnumerateObject());
        var source = dataSources.GetProperty("platformProbe");
        Assert.Equal(1, source.GetProperty("totalRows").GetInt32());
        Assert.Equal(2, source.GetProperty("columnNames").GetArrayLength());
        Assert.Equal("metric_value", source.GetProperty("columnNames")[0].GetString());
        Assert.Equal("state", source.GetProperty("columnNames")[1].GetString());
        var row = Assert.Single(source.GetProperty("rows").EnumerateArray());
        Assert.Equal(2, row.GetArrayLength());
        Assert.Equal(7, row[0].GetInt32());
        Assert.Equal("ready", row[1].GetString());
        return source.GetProperty("rows").GetRawText();
    }
}
