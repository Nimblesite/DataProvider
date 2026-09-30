using System.Data.Common;
using System.Net;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Nimblesite.Sync.Http.Tests;

[Collection(PostgresTestSuite.Name)]
public sealed record HttpEndpointPlatformTests
    : IClassFixture<SyncApiWebApplicationFactory>,
        IClassFixture<HttpSqlServerContainerFixture>
{
    private readonly SyncApiWebApplicationFactory _factory;
    private readonly PostgresContainerFixture _postgres;
    private readonly HttpSqlServerContainerFixture _sqlServer;

    public HttpEndpointPlatformTests(
        SyncApiWebApplicationFactory factory,
        PostgresContainerFixture postgres,
        HttpSqlServerContainerFixture sqlServer
    )
    {
        _factory = factory;
        _postgres = postgres;
        _sqlServer = sqlServer;
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public async Task FreshDatabase_HttpStateAndPullAreEmpty(string provider)
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
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public async Task PushOneChange_InsertsExactlyOnePerson(string provider)
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

    private static async Task AssertPersonAsync(string provider, string connectionString, Guid id)
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
        Assert.Equal("Ada", reader.GetString(1));
        Assert.Equal("ada@example.test", reader.GetString(2));
        Assert.False(reader.Read(), $"{provider} inserted more than one person");
    }

    private async Task<HttpTarget> CreateTargetAsync(string provider)
    {
        if (provider == "postgres")
        {
            using var connection = await _postgres
                .CreateDatabaseAsync("sync_http")
                .ConfigureAwait(false);
            CreatePersonTable("postgres", connection.ConnectionString, "public");
            Assert.IsType<BoolSyncOk>(PostgresSyncSchema.CreateSchema(connection));
            Assert.IsType<BoolSyncOk>(PostgresSyncSchema.SetOriginId(connection, "server-origin"));
            return new HttpTarget(connection.ConnectionString, FilePath: null);
        }
        if (provider == "sqlserver")
        {
            var connectionString = await _sqlServer
                .CreateDatabaseConnectionStringAsync()
                .ConfigureAwait(false);
            return new HttpTarget(connectionString, FilePath: null);
        }
        Assert.Equal("sqlite", provider);
        var path = Path.Combine(Path.GetTempPath(), $"sync_http_{Guid.NewGuid():N}.db");
        var sqliteConnectionString = $"Data Source={path};Pooling=False";
        CreatePersonTable("sqlite", path, "main");
        using var sqlite = new SqliteConnection(sqliteConnectionString);
        await sqlite.OpenAsync().ConfigureAwait(false);
        Assert.IsType<BoolSyncOk>(SyncSchema.CreateSchema(sqlite));
        Assert.IsType<BoolSyncOk>(SyncSchema.SetOriginId(sqlite, "server-origin"));
        return new HttpTarget(sqliteConnectionString, path);
    }

    private static void CreatePersonTable(string provider, string output, string schema)
    {
        var yaml = $$"""
            name: sync_http_person
            tables:
              - name: person
                schema: {{schema}}
                columns:
                  - name: id
                    type: Uuid
                    isNullable: false
                  - name: name
                    type: Text
                    isNullable: false
                  - name: email
                    type: Text
                    isNullable: false
                primaryKey:
                  columns:
                    - id
            """;
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, yaml);
            Assert.Equal(
                0,
                DataProviderMigrate.Program.Main([
                    "migrate",
                    "--schema",
                    path,
                    "--provider",
                    provider,
                    "--output",
                    output,
                ])
            );
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed record HttpTarget(string ConnectionString, string? FilePath);
}
