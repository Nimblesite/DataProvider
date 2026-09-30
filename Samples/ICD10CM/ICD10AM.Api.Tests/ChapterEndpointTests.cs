namespace ICD10AM.Api.Tests;

/// <summary>
/// E2E tests for ICD-10-AM chapter endpoints - REAL database, NO mocks.
/// </summary>
public sealed class ChapterEndpointTests : IClassFixture<ICD10AMApiFactory>
{
    private readonly HttpClient _client;

    public ChapterEndpointTests(ICD10AMApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetChapters_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/icd10am/chapters");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetChapters_ReturnsSeededChapters()
    {
        var response = await _client.GetAsync("/api/icd10am/chapters");
        var chapters = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(chapters);
        Assert.True(chapters.Length >= 2, "Expected at least 2 seeded chapters");
        Assert.Contains(chapters, c => c.GetProperty("ChapterNumber").GetString() == "I");
        Assert.Contains(chapters, c => c.GetProperty("ChapterNumber").GetString() == "XVIII");
    }

    [Fact]
    public async Task GetChapters_ChaptersHaveRequiredFields()
    {
        var response = await _client.GetAsync("/api/icd10am/chapters");
        var chapters = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(chapters);
        Assert.NotEmpty(chapters);

        var chapter = chapters[0];
        Assert.True(chapter.TryGetProperty("Id", out _), "Missing Id");
        Assert.True(chapter.TryGetProperty("ChapterNumber", out _), "Missing ChapterNumber");
        Assert.True(chapter.TryGetProperty("Title", out _), "Missing Title");
        Assert.True(chapter.TryGetProperty("CodeRangeStart", out _), "Missing CodeRangeStart");
        Assert.True(chapter.TryGetProperty("CodeRangeEnd", out _), "Missing CodeRangeEnd");
    }

    [Fact]
    public async Task GetBlocksByChapter_ReturnsOk_WhenChapterExists()
    {
        var response = await _client.GetAsync("/api/icd10am/chapters/ch-01/blocks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetBlocksByChapter_ReturnsBlocks()
    {
        var response = await _client.GetAsync("/api/icd10am/chapters/ch-01/blocks");
        var blocks = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(blocks);
        Assert.NotEmpty(blocks);
        Assert.Contains(blocks, b => b.GetProperty("BlockCode").GetString() == "A00-A09");
    }

    [Fact]
    public async Task GetBlocksByChapter_ReturnsEmptyArray_WhenChapterNotExists()
    {
        var response = await _client.GetAsync("/api/icd10am/chapters/nonexistent/blocks");
        var blocks = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(blocks);
        Assert.Empty(blocks);
    }
}
