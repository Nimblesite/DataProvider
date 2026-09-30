namespace ICD10AM.Api.Tests;

/// <summary>
/// E2E tests for ICD-10-AM code lookup endpoints - REAL database, NO mocks.
/// </summary>
public sealed class CodeLookupTests : IClassFixture<ICD10AMApiFactory>
{
    private readonly HttpClient _client;

    public CodeLookupTests(ICD10AMApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetCodeByCode_ReturnsOk_WhenCodeExists()
    {
        var response = await _client.GetAsync("/api/icd10am/codes/A00.0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCodeByCode_ReturnsCodeDetails()
    {
        var response = await _client.GetAsync("/api/icd10am/codes/A00.0");
        var code = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("A00.0", code.GetProperty("Code").GetString());
        Assert.Contains("Cholera", code.GetProperty("ShortDescription").GetString());
        Assert.Equal("I", code.GetProperty("ChapterNumber").GetString());
    }

    [Fact]
    public async Task GetCodeByCode_ReturnsNotFound_WhenCodeNotExists()
    {
        var response = await _client.GetAsync("/api/icd10am/codes/INVALID99");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCodeByCode_ReturnsFhirFormat_WhenRequested()
    {
        var response = await _client.GetAsync("/api/icd10am/codes/R07.4?format=fhir");
        var content = await response.Content.ReadAsStringAsync();
        var fhir = JsonSerializer.Deserialize<JsonElement>(content);

        Assert.Equal("CodeSystem", fhir.GetProperty("ResourceType").GetString());
        Assert.Equal("http://hl7.org/fhir/sid/icd-10-am", fhir.GetProperty("Url").GetString());
        Assert.Equal("R07.4", fhir.GetProperty("Concept").GetProperty("Code").GetString());
    }

    [Fact]
    public async Task SearchCodes_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/icd10am/codes?q=cholera");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SearchCodes_FindsMatchingCodes()
    {
        var response = await _client.GetAsync("/api/icd10am/codes?q=cholera");
        var codes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(codes);
        Assert.NotEmpty(codes);
        Assert.Contains(codes, c => c.GetProperty("Code").GetString() == "A00.0");
    }

    [Fact]
    public async Task SearchCodes_RespectsLimit()
    {
        var response = await _client.GetAsync("/api/icd10am/codes?q=a&limit=1");
        var codes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(codes);
        Assert.True(codes.Length <= 1, "Expected at most 1 result with limit=1");
    }

    [Fact]
    public async Task SearchCodes_SearchesByCode()
    {
        var response = await _client.GetAsync("/api/icd10am/codes?q=R07");
        var codes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(codes);
        Assert.Contains(
            codes,
            c => c.GetProperty("Code").GetString()!.StartsWith("R07", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task SearchCodes_ReturnsEmptyArray_WhenNoMatch()
    {
        var response = await _client.GetAsync("/api/icd10am/codes?q=zzznomatch");
        var codes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(codes);
        Assert.Empty(codes);
    }

    [Fact]
    public async Task GetCategoriesByBlock_ReturnsCategories()
    {
        var response = await _client.GetAsync("/api/icd10am/blocks/blk-a00/categories");
        var categories = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(categories);
        Assert.NotEmpty(categories);
        Assert.Contains(categories, c => c.GetProperty("CategoryCode").GetString() == "A00");
    }

    [Fact]
    public async Task GetCodesByCategory_ReturnsCodes()
    {
        var response = await _client.GetAsync("/api/icd10am/categories/cat-a00/codes");
        var codes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(codes);
        Assert.NotEmpty(codes);
        Assert.Contains(codes, c => c.GetProperty("Code").GetString() == "A00.0");
    }

    // =========================================================================
    // ICD-10-CM LOOKUP TESTS (codes returned by RAG search)
    // =========================================================================

    [Fact]
    public async Task Icd10Cm_GetCodeByCode_ReturnsOk_WhenCodeExists()
    {
        var response = await _client.GetAsync("/api/icd10cm/codes/I10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Icd10Cm_GetCodeByCode_ReturnsAllDetails()
    {
        var response = await _client.GetAsync("/api/icd10cm/codes/I10");
        var code = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Verify ALL required fields are present and populated
        Assert.Equal("I10", code.GetProperty("Code").GetString());
        Assert.False(
            string.IsNullOrEmpty(code.GetProperty("ShortDescription").GetString()),
            "ShortDescription must not be empty"
        );
        Assert.False(
            string.IsNullOrEmpty(code.GetProperty("LongDescription").GetString()),
            "LongDescription must not be empty"
        );
        Assert.True(code.TryGetProperty("Billable", out _), "Billable property must exist");
        Assert.True(code.TryGetProperty("Id", out _), "Id property must exist");

        // Verify specific content
        Assert.Contains(
            "hypertension",
            code.GetProperty("ShortDescription").GetString(),
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public async Task Icd10Cm_GetCodeByCode_ReturnsNotFound_WhenCodeNotExists()
    {
        var response = await _client.GetAsync("/api/icd10cm/codes/INVALID99");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Icd10Cm_GetCodeByCode_ReturnsFhirFormat_WhenRequested()
    {
        var response = await _client.GetAsync("/api/icd10cm/codes/I10?format=fhir");
        var fhir = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("CodeSystem", fhir.GetProperty("ResourceType").GetString());
        Assert.Equal("http://hl7.org/fhir/sid/icd-10-cm", fhir.GetProperty("Url").GetString());
        Assert.Equal("I10", fhir.GetProperty("Concept").GetProperty("Code").GetString());
    }

    [Fact]
    public async Task Icd10Cm_SearchCodes_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/icd10cm/codes?q=hypertension");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Icd10Cm_SearchCodes_FindsMatchingCodes()
    {
        var response = await _client.GetAsync("/api/icd10cm/codes?q=hypertension");
        var codes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(codes);
        Assert.NotEmpty(codes);
        Assert.Contains(codes, c => c.GetProperty("Code").GetString() == "I10");
    }

    [Fact]
    public async Task Icd10Cm_SearchCodes_RespectsLimit()
    {
        var response = await _client.GetAsync("/api/icd10cm/codes?q=a&limit=1");
        var codes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(codes);
        Assert.True(codes.Length <= 1, "Expected at most 1 result with limit=1");
    }

    [Fact]
    public async Task Icd10Cm_LookupCodeReturnedByRagSearch_Succeeds()
    {
        // This test verifies the critical bug fix:
        // Codes returned by RAG search (from icd10cm_code_embedding) MUST be lookupable
        // via /api/icd10cm/codes/{code}

        // I21.11 is seeded in icd10cm_code (used by RAG search)
        var response = await _client.GetAsync("/api/icd10cm/codes/I21.11");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var code = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("I21.11", code.GetProperty("Code").GetString());
        Assert.Contains(
            "myocardial infarction",
            code.GetProperty("ShortDescription").GetString(),
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public async Task Icd10Cm_LookupAllSeededCodes_AllSucceed()
    {
        // Verify ALL codes seeded for RAG search can be looked up
        // Descriptions match ICD10AMApiFactory.SeedIcd10CmCodesWithEmbeddings
        var seededCodes = new[]
        {
            ("R07.4", "chest pain"),
            ("R07.89", "chest pain"),
            ("R06.00", "dyspnea"),
            ("R06.02", "shortness of breath"),
            ("I21.11", "ST elevation"),
            ("I21.19", "ST elevation"),
            ("J18.9", "pneumonia"),
            ("J20.9", "bronchitis"),
            ("E11.9", "diabetes"),
            ("I10", "hypertension"),
            ("M54.5", "back pain"),
            ("G43.909", "migraine"),
        };

        foreach (var (code, expectedDescription) in seededCodes)
        {
            var response = await _client.GetAsync($"/api/icd10cm/codes/{code}");
            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"Lookup failed for code {code}: {response.StatusCode}"
            );

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(code, result.GetProperty("Code").GetString());
            Assert.Contains(
                expectedDescription,
                result.GetProperty("ShortDescription").GetString(),
                StringComparison.OrdinalIgnoreCase
            );
        }
    }
}
