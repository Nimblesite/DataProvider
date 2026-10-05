using System.Net;
using System.Text.Json;

namespace Nimblesite.Sync.Http.Tests;

/// <summary>
/// Custom WebApplicationFactory that sets the correct content root path.
/// </summary>
public sealed class SyncApiWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Use the output directory where the test assembly runs from
        // This avoids path resolution issues with the source directory structure
        var contentRoot = Path.GetDirectoryName(typeof(Program).Assembly.Location)!;
        builder.UseContentRoot(contentRoot);
    }
}

/// <summary>
/// E2E HTTP integration tests for sync API endpoints.
/// Tests the FULL HTTP stack including rate limiting, validation, and error handling.
/// Uses WebApplicationFactory for real ASP.NET Core hosting.
/// </summary>
public sealed class HttpEndpointTests : IClassFixture<SyncApiWebApplicationFactory>
{
    private readonly SyncApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public HttpEndpointTests(SyncApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    #region Health Endpoint

    [Fact]
    public async Task Health_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", content);
    }

    #endregion

    #region Pull Changes Endpoint

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public async Task PullChanges_WithoutConnectionString_ReturnsBadRequest(string provider)
    {
        // Act
        var response = await _client.GetAsync(
            $"/sync/changes?fromVersion=0&batchSize=100&dbType={provider}"
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PullChanges_WithInvalidDbType_ReturnsProblem()
    {
        // Act
        var response = await _client.GetAsync(
            "/sync/changes?fromVersion=0&batchSize=100&dbType=invalid&connectionString=test"
        );

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    #endregion

    #region Push Changes Endpoint

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public async Task PushChanges_WithoutBody_ReturnsBadRequest(string provider)
    {
        // Arrange
        var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(
            $"/sync/changes?dbType={provider}&connectionString=test",
            content
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PushChanges_WithTooManyChanges_ReturnsBadRequest()
    {
        // Arrange - create 10001 fake changes (over the 10000 limit)
        var changes = Enumerable
            .Range(0, 10001)
            .Select(i => new
            {
                Version = (long)i,
                TableName = "Person",
                PkValue = $"{{\"Id\":\"{i}\"}}",
                Operation = "insert",
                Payload = $"{{\"Id\":\"{i}\",\"Name\":\"Person {i}\"}}",
                Origin = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow.ToString("O"),
            })
            .ToList();

        var request = new { Changes = changes, OriginId = Guid.NewGuid().ToString() };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        // Act - using temp file path (test expects BadRequest before connection is used)
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"temp_test_{Guid.NewGuid()}.db");
        var response = await _client.PostAsync(
            $"/sync/changes?dbType=sqlite&connectionString=Data Source={Uri.EscapeDataString(tempDbPath)}",
            content
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("10000", body);
    }

    #endregion

    #region Register Client Endpoint

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public async Task RegisterClient_WithoutOriginId_ReturnsBadRequest(string provider)
    {
        // Arrange
        var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(
            $"/sync/clients?dbType={provider}&connectionString=test",
            content
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public async Task RegisterClient_WithoutConnectionString_ReturnsBadRequest(string provider)
    {
        // Arrange
        var request = new { OriginId = Guid.NewGuid().ToString(), LastSyncVersion = 0L };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        // Act
        var response = await _client.PostAsync($"/sync/clients?dbType={provider}", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Sync State Endpoint

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public async Task SyncState_WithoutConnectionString_ReturnsBadRequest(string provider)
    {
        // Act
        var response = await _client.GetAsync($"/sync/state?dbType={provider}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion
}

/// <summary>
/// E2E tests with real SQLite database for HTTP endpoints.
/// </summary>
public sealed class HttpEndpointWithDatabaseTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _connection;
    private readonly string _originId = Guid.NewGuid().ToString();

    public HttpEndpointWithDatabaseTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sync_test_{Guid.NewGuid()}.db");
        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();

        // Initialize sync schema
        SyncSchema.CreateSchema(_connection);
        SyncSchema.SetOriginId(_connection, _originId);

        // Create test table with triggers
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Person (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Email TEXT
            );
            """;
        cmd.ExecuteNonQuery();

        TriggerGenerator.CreateTriggers(_connection, "Person", NullLogger.Instance);
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public void Database_InitializesCorrectly()
    {
        // Verify origin ID is set
        var result = SyncSchema.GetOriginId(_connection);
        Assert.IsType<StringSyncOk>(result);
        Assert.Equal(_originId, ((StringSyncOk)result).Value);
    }

    [Fact]
    public void Insert_CreatesChangelog()
    {
        // Act
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            "INSERT INTO Person (Id, Name, Email) VALUES ('p1', 'Alice', 'alice@test.com')";
        cmd.ExecuteNonQuery();

        // Assert
        var changes = SyncLogRepository.FetchChanges(_connection, 0, 100);

        var list = (Assert.IsType<SyncLogListOk>(changes)).Value;
        Assert.Single(list);
        Assert.Equal("Person", list[0].TableName);
        Assert.Equal(SyncOperation.Insert, list[0].Operation);
    }

    [Fact]
    public void Update_CreatesChangelog()
    {
        // Arrange
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO Person (Id, Name, Email) VALUES ('p2', 'Bob', 'bob@test.com')";
            cmd.ExecuteNonQuery();
        }

        // Act
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "UPDATE Person SET Name = 'Bob Updated' WHERE Id = 'p2'";
            cmd.ExecuteNonQuery();
        }

        // Assert
        var changes = SyncLogRepository.FetchChanges(_connection, 0, 100);
        var list = ((SyncLogListOk)changes).Value;
        Assert.Equal(2, list.Count);
        Assert.Equal(SyncOperation.Update, list[1].Operation);
    }

    [Fact]
    public void Delete_CreatesTombstone()
    {
        // Arrange
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO Person (Id, Name, Email) VALUES ('p3', 'Charlie', 'charlie@test.com')";
            cmd.ExecuteNonQuery();
        }

        // Act
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM Person WHERE Id = 'p3'";
            cmd.ExecuteNonQuery();
        }

        // Assert
        var changes = SyncLogRepository.FetchChanges(_connection, 0, 100);
        var list = ((SyncLogListOk)changes).Value;
        Assert.Equal(2, list.Count);
        Assert.Equal(SyncOperation.Delete, list[1].Operation);
    }

    [Fact]
    public void TriggerSuppression_PreventsLogging()
    {
        // Act - insert with suppression enabled
        SyncSessionManager.EnableSuppression(_connection);
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO Person (Id, Name, Email) VALUES ('suppressed', 'Suppressed', 's@test.com')";
            cmd.ExecuteNonQuery();
        }
        SyncSessionManager.DisableSuppression(_connection);

        // Assert - no changes logged
        var changes = SyncLogRepository.FetchChanges(_connection, 0, 100);
        var list = ((SyncLogListOk)changes).Value;
        Assert.Empty(list);
    }

    [Fact]
    public void ApplyChange_WithSuppression_DoesNotEcho()
    {
        // Arrange - simulate incoming change
        var incomingEntry = new SyncLogEntry(
            Version: 1,
            TableName: "Person",
            PkValue: "{\"Id\":\"incoming1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{\"Id\":\"incoming1\",\"Name\":\"Incoming\",\"Email\":\"in@test.com\"}",
            Origin: "remote-origin",
            Timestamp: DateTime.UtcNow.ToString("O")
        );

        // Act - apply with suppression
        SyncSessionManager.EnableSuppression(_connection);
        var result = ChangeApplierSQLite.ApplyChange(_connection, incomingEntry);
        SyncSessionManager.DisableSuppression(_connection);

        // Assert - change applied but not logged
        Assert.IsType<BoolSyncOk>(result);

        var changes = SyncLogRepository.FetchChanges(_connection, 0, 100);
        var list = ((SyncLogListOk)changes).Value;
        Assert.Empty(list);

        // Verify data exists
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Person WHERE Id = 'incoming1'";
        Assert.Equal("Incoming", cmd.ExecuteScalar()?.ToString());
    }
}
