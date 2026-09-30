using ICD10AM.Api.Tests;

namespace ICD10AM.Cli.Tests;

/// <summary>
/// Test fixture that spins up a real API with seeded test data.
/// CLI tests run against this real API.
/// </summary>
public sealed class CliTestFixture : IDisposable
{
    private readonly ICD10AMApiFactory _factory;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Gets the API base URL.
    /// </summary>
    public string ApiUrl { get; }

    /// <summary>
    /// Gets the HTTP client configured for the test API.
    /// </summary>
    public HttpClient HttpClient => _httpClient;

    /// <summary>
    /// Creates a new test fixture with a real API server.
    /// </summary>
    public CliTestFixture()
    {
        _factory = new ICD10AMApiFactory();
        _httpClient = _factory.CreateClient();
        ApiUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "http://localhost";
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _httpClient.Dispose();
        _factory.Dispose();
    }
}
