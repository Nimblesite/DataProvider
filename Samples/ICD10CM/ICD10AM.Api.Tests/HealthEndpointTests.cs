namespace ICD10AM.Api.Tests;

/// <summary>
/// E2E tests for health check endpoint.
/// </summary>
public sealed class HealthEndpointTests : IClassFixture<ICD10AMApiFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(ICD10AMApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_ReturnsHealthyStatus()
    {
        var response = await _client.GetAsync("/health");
        var health = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("healthy", health.GetProperty("Status").GetString());
        Assert.Equal("ICD10AM.Api", health.GetProperty("Service").GetString());
    }
}
