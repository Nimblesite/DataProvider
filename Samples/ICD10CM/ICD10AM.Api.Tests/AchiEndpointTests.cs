namespace ICD10AM.Api.Tests;

/// <summary>
/// E2E tests for ACHI procedure endpoints - REAL database, NO mocks.
/// </summary>
public sealed class AchiEndpointTests : IClassFixture<ICD10AMApiFactory>
{
    private readonly HttpClient _client;

    public AchiEndpointTests(ICD10AMApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAchiBlocks_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/achi/blocks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAchiBlocks_ReturnsSeededBlocks()
    {
        var response = await _client.GetAsync("/api/achi/blocks");
        var blocks = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(blocks);
        Assert.NotEmpty(blocks);
        Assert.Contains(blocks, b => b.GetProperty("BlockNumber").GetString() == "1820");
    }

    [Fact]
    public async Task GetAchiCodesByBlock_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/achi/blocks/achi-blk-1/codes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAchiCodesByBlock_ReturnsCodes()
    {
        var response = await _client.GetAsync("/api/achi/blocks/achi-blk-1/codes");
        var codes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(codes);
        Assert.NotEmpty(codes);
        Assert.Contains(codes, c => c.GetProperty("Code").GetString() == "38497-00");
    }

    [Fact]
    public async Task GetAchiCodeByCode_ReturnsOk_WhenCodeExists()
    {
        var response = await _client.GetAsync("/api/achi/codes/38497-00");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAchiCodeByCode_ReturnsCodeDetails()
    {
        var response = await _client.GetAsync("/api/achi/codes/38497-00");
        var code = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("38497-00", code.GetProperty("Code").GetString());
        Assert.Contains(
            "angiography",
            code.GetProperty("ShortDescription").GetString()!,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public async Task GetAchiCodeByCode_ReturnsNotFound_WhenCodeNotExists()
    {
        var response = await _client.GetAsync("/api/achi/codes/99999-99");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAchiCodeByCode_ReturnsFhirFormat_WhenRequested()
    {
        var response = await _client.GetAsync("/api/achi/codes/38497-00?format=fhir");
        var fhir = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("CodeSystem", fhir.GetProperty("ResourceType").GetString());
        Assert.Equal("http://hl7.org/fhir/sid/achi", fhir.GetProperty("Url").GetString());
    }

    [Fact]
    public async Task SearchAchiCodes_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/achi/codes?q=coronary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SearchAchiCodes_FindsMatchingCodes()
    {
        var response = await _client.GetAsync("/api/achi/codes?q=coronary");
        var codes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(codes);
        Assert.NotEmpty(codes);
        Assert.Contains(codes, c => c.GetProperty("Code").GetString() == "38497-00");
    }

    [Fact]
    public async Task SearchAchiCodes_RespectsLimit()
    {
        var response = await _client.GetAsync("/api/achi/codes?q=a&limit=1");
        var codes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(codes);
        Assert.True(codes.Length <= 1, "Expected at most 1 result with limit=1");
    }
}
