using System.Data.Common;
using System.Net;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Nimblesite.Sync.Http.Tests;

[Collection(PostgresTestSuite.Name)]
public sealed partial record HttpEndpointPlatformTests
    : IClassFixture<SyncApiWebApplicationFactory>,
        IClassFixture<SqlServerContainerFixture>
{
    private readonly SyncApiWebApplicationFactory _factory;
    private readonly PostgresContainerFixture _postgres;
    private readonly SqlServerContainerFixture _sqlServer;

    public HttpEndpointPlatformTests(
        SyncApiWebApplicationFactory factory,
        PostgresContainerFixture postgres,
        SqlServerContainerFixture sqlServer
    )
    {
        _factory = factory;
        _postgres = postgres;
        _sqlServer = sqlServer;
    }

    [Theory]
    [MemberData(nameof(HttpStateAndPull_ReturnEmptyStateAndChangesInlineCases))]
    // Implements [SYNC-HTTP-PROVIDERS].
    public async Task HttpStateAndPull_ReturnEmptyStateAndChanges(string provider)
    {
        var target = await CreateTargetAsync(provider).ConfigureAwait(true);
        try
        {
            using var client = _factory.CreateClient();
            var query =
                $"dbType={provider}&connectionString={Uri.EscapeDataString(target.ConnectionString)}";
            using var state = await client.GetAsync($"/sync/state?{query}").ConfigureAwait(true);
            Assert.Equal(HttpStatusCode.OK, state.StatusCode);
            Assert.Equal("application/json", state.Content.Headers.ContentType?.MediaType);
            using var stateJson = JsonDocument.Parse(
                await state.Content.ReadAsStringAsync().ConfigureAwait(true)
            );
            Assert.Equal(0L, stateJson.RootElement.GetProperty("maxVersion").GetInt64());

            using var pull = await client
                .GetAsync($"/sync/changes?fromVersion=0&batchSize=10&{query}")
                .ConfigureAwait(true);
            Assert.Equal(HttpStatusCode.OK, pull.StatusCode);
            Assert.Equal("application/json", pull.Content.Headers.ContentType?.MediaType);
            using var pullJson = JsonDocument.Parse(
                await pull.Content.ReadAsStringAsync().ConfigureAwait(true)
            );
            Assert.Empty(pullJson.RootElement.GetProperty("changes").EnumerateArray());
        }
        finally
        {
            if (target.FilePath is not null)
            {
                File.Delete(target.FilePath);
            }
        }
    }

    [Theory]
    [MemberData(nameof(HttpPush_AppliesChangeOnEveryProviderInlineCases))]
    // Implements [SYNC-HTTP-PROVIDERS].
    public async Task HttpPush_AppliesChangeOnEveryProvider(string provider)
    {
        var target = await CreateTargetAsync(provider).ConfigureAwait(true);
        try
        {
            using var client = _factory.CreateClient();
            var id = Guid.Parse("47cb994e-3e7e-492b-9bc1-2e56dc556dcc");
            var change = new
            {
                Version = 1L,
                TableName = "person",
                PkValue = JsonSerializer.Serialize(new { id }),
                Operation = "insert",
                Payload = JsonSerializer.Serialize(
                    new
                    {
                        id,
                        name = "Ada",
                        email = "ada@example.test",
                    }
                ),
                Origin = "remote-origin",
                Timestamp = "2024-01-01T00:00:00Z",
            };
            var request = new { Changes = new[] { change }, OriginId = "server-origin" };
            using var content = new StringContent(
                JsonSerializer.Serialize(request),
                System.Text.Encoding.UTF8,
                "application/json"
            );
            using var response = await client.PostAsync(
                $"/sync/changes?dbType={provider}&connectionString={Uri.EscapeDataString(target.ConnectionString)}",
                content
            );
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            await AssertPersonAsync(provider, target.ConnectionString, id).ConfigureAwait(true);
        }
        finally
        {
            if (target.FilePath is not null)
            {
                File.Delete(target.FilePath);
            }
        }
    }

    private static async Task AssertPersonAsync(
        string provider,
        string connectionString,
        Guid id,
        string expectedName = "Ada"
    )
    {
        using DbConnection connection = provider switch
        {
            "sqlite" => new SqliteConnection(connectionString),
            "postgres" => new NpgsqlConnection(connectionString),
            _ => new SqlConnection(connectionString),
        };
        await connection.OpenAsync().ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, email FROM person";
        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        Assert.True(reader.Read(), $"{provider} did not receive the pushed person");
        Assert.Equal(id, Guid.Parse(reader.GetValue(0).ToString() ?? string.Empty));
        Assert.Equal(expectedName, reader.GetString(1));
        Assert.Equal("ada@example.test", reader.GetString(2));
        Assert.False(reader.Read(), $"{provider} inserted more than one person");
    }

    private Task<(string ConnectionString, string? FilePath)> CreateTargetAsync(
        string provider,
        bool tableChecks = false
    ) =>
        SyncDatabaseTargets.CreateAsync(
            provider: provider,
            postgres: _postgres,
            sqlServer: _sqlServer,
            tableChecks: tableChecks
        );
}
