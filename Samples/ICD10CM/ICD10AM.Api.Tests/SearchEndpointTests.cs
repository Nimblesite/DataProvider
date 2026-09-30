namespace ICD10AM.Api.Tests;

/// <summary>
/// E2E tests for RAG semantic search endpoint - REAL embedding service, NO mocks.
/// These tests require the Docker embedding service running at localhost:8000.
/// Start it with: ./scripts/Dependencies/start.sh
/// </summary>
public sealed class SearchEndpointTests : IClassFixture<ICD10AMApiFactory>
{
    private readonly HttpClient _client;
    private readonly ICD10AMApiFactory _factory;

    public SearchEndpointTests(ICD10AMApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Search_ReturnsServiceUnavailable_WhenEmbeddingServiceDown()
    {
        // This test verifies the error handling when embedding service is unavailable
        // Skip if service IS available - we want to test the failure case separately
        if (_factory.EmbeddingServiceAvailable)
        {
            // Service is up, so we can't test the failure case here
            // This is expected in normal E2E testing
            return;
        }

        var response = await _client.PostAsJsonAsync(
            "/api/search",
            new { Query = "chest pain", Limit = 5 }
        );

        // Should return 500 with problem details when embedding service is down
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Search_ReturnsOk_WhenEmbeddingServiceAvailable()
    {
        SkipIfEmbeddingServiceUnavailable();

        var response = await _client.PostAsJsonAsync(
            "/api/search",
            new { Query = "chest pain", Limit = 5 }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Search_ReturnsResults_ForChestPainQuery()
    {
        SkipIfEmbeddingServiceUnavailable();

        var response = await _client.PostAsJsonAsync(
            "/api/search",
            new { Query = "chest pain", Limit = 10 }
        );

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);

        Assert.True(
            result.TryGetProperty("Results", out var results),
            "Response should have Results property"
        );
        Assert.True(results.GetArrayLength() > 0, "Should return at least one result");
    }

    [Fact]
    public async Task Search_ReturnsChestPainCodes_ForChestPainQuery()
    {
        SkipIfEmbeddingServiceUnavailable();

        var response = await _client.PostAsJsonAsync(
            "/api/search",
            new { Query = "chest pain with shortness of breath", Limit = 10 }
        );

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        var results = result.GetProperty("Results");

        // Semantic search should rank chest pain (R07.x) and dyspnea (R06.x) codes highly
        var codes = new List<string>();
        foreach (var item in results.EnumerateArray())
        {
            codes.Add(item.GetProperty("Code").GetString()!);
        }

        // At least one chest pain or shortness of breath code should be in top results
        var hasRelevantCode = codes.Any(c =>
            c.StartsWith("R07", StringComparison.Ordinal)
            || // Chest pain
            c.StartsWith("R06", StringComparison.Ordinal)
            || // Dyspnea/breathing problems
            c.StartsWith("I21", StringComparison.Ordinal) // Heart attack (also chest pain related)
        );

        Assert.True(
            hasRelevantCode,
            $"Expected chest pain/dyspnea codes in top results. Got: {string.Join(", ", codes)}"
        );
    }

    [Fact]
    public async Task Search_ReturnsResultsWithConfidenceScores()
    {
        SkipIfEmbeddingServiceUnavailable();

        var response = await _client.PostAsJsonAsync(
            "/api/search",
            new { Query = "diabetes", Limit = 5 }
        );

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        var results = result.GetProperty("Results");

        foreach (var item in results.EnumerateArray())
        {
            Assert.True(
                item.TryGetProperty("Confidence", out var confidence),
                "Each result should have Confidence score"
            );
            var score = confidence.GetDouble();
            Assert.True(
                score >= -1 && score <= 1,
                $"Confidence should be valid cosine similarity: {score}"
            );
        }
    }

    [Fact]
    public async Task Search_ResultsAreRankedByConfidence()
    {
        SkipIfEmbeddingServiceUnavailable();

        var response = await _client.PostAsJsonAsync(
            "/api/search",
            new { Query = "heart attack myocardial infarction", Limit = 10 }
        );

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        var results = result.GetProperty("Results");

        var confidences = new List<double>();
        foreach (var item in results.EnumerateArray())
        {
            confidences.Add(item.GetProperty("Confidence").GetDouble());
        }

        // Verify results are sorted in descending order by confidence
        for (var i = 0; i < confidences.Count - 1; i++)
        {
            Assert.True(
                confidences[i] >= confidences[i + 1],
                $"Results should be sorted by confidence descending. Got {confidences[i]} followed by {confidences[i + 1]}"
            );
        }
    }

    [Fact]
    public async Task Search_RespectsLimitParameter()
    {
        SkipIfEmbeddingServiceUnavailable();

        var response = await _client.PostAsJsonAsync(
            "/api/search",
            new { Query = "pain", Limit = 3 }
        );

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        var results = result.GetProperty("Results");

        Assert.True(
            results.GetArrayLength() <= 3,
            $"Should respect limit=3, got {results.GetArrayLength()} results"
        );
    }

    [Fact]
    public async Task Search_ReturnsFhirFormat_WhenRequested()
    {
        SkipIfEmbeddingServiceUnavailable();

        var response = await _client.PostAsJsonAsync(
            "/api/search",
            new
            {
                Query = "pneumonia lung infection",
                Limit = 5,
                Format = "fhir",
            }
        );

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);

        Assert.Equal("Bundle", result.GetProperty("ResourceType").GetString());
        Assert.Equal("searchset", result.GetProperty("Type").GetString());
        Assert.True(result.TryGetProperty("Total", out _), "FHIR response should have Total");
        Assert.True(
            result.TryGetProperty("Entry", out var entries),
            "FHIR response should have Entry array"
        );

        if (entries.GetArrayLength() > 0)
        {
            var firstEntry = entries[0];
            Assert.True(
                firstEntry.TryGetProperty("Resource", out var resource),
                "Entry should have Resource"
            );
            Assert.Equal("CodeSystem", resource.GetProperty("ResourceType").GetString());
            Assert.True(
                firstEntry.TryGetProperty("Search", out var search),
                "Entry should have Search"
            );
            Assert.True(search.TryGetProperty("Score", out _), "Search should have Score");
        }
    }

    [Fact]
    public async Task Search_IncludesModelInfo_InResponse()
    {
        SkipIfEmbeddingServiceUnavailable();

        var response = await _client.PostAsJsonAsync(
            "/api/search",
            new { Query = "hypertension high blood pressure", Limit = 5 }
        );

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);

        Assert.True(result.TryGetProperty("Model", out var model), "Response should include Model");
        Assert.Equal("MedEmbed-Small-v0.1", model.GetString());
        Assert.True(result.TryGetProperty("Query", out var query), "Response should echo Query");
        Assert.Equal("hypertension high blood pressure", query.GetString());
    }

    [Fact]
    public async Task Search_SemanticallySimilarQueries_ReturnSimilarResults()
    {
        SkipIfEmbeddingServiceUnavailable();

        // Two semantically similar queries should return overlapping results
        var response1 = await _client.PostAsJsonAsync(
            "/api/search",
            new { Query = "difficulty breathing", Limit = 5 }
        );
        var response2 = await _client.PostAsJsonAsync(
            "/api/search",
            new { Query = "shortness of breath dyspnea", Limit = 5 }
        );

        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();
        var result1 = JsonSerializer.Deserialize<JsonElement>(content1);
        var result2 = JsonSerializer.Deserialize<JsonElement>(content2);

        var codes1 = GetCodesFromResults(result1.GetProperty("Results"));
        var codes2 = GetCodesFromResults(result2.GetProperty("Results"));

        // There should be overlap in results for semantically similar queries
        var overlap = codes1.Intersect(codes2).ToList();
        Assert.True(
            overlap.Count > 0,
            $"Semantically similar queries should return overlapping results. Query1: [{string.Join(", ", codes1)}], Query2: [{string.Join(", ", codes2)}]"
        );
    }

    [Fact]
    public async Task Search_ReturnsDescriptions_ForAllResults()
    {
        SkipIfEmbeddingServiceUnavailable();

        var response = await _client.PostAsJsonAsync(
            "/api/search",
            new { Query = "headache migraine", Limit = 5 }
        );

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        var results = result.GetProperty("Results");

        foreach (var item in results.EnumerateArray())
        {
            Assert.True(item.TryGetProperty("Code", out var code), "Result should have Code");
            Assert.False(string.IsNullOrEmpty(code.GetString()), "Code should not be empty");

            Assert.True(
                item.TryGetProperty("Description", out var desc),
                "Result should have Description"
            );
            Assert.False(string.IsNullOrEmpty(desc.GetString()), "Description should not be empty");
        }
    }

    [Fact]
    public async Task Search_TopResult_IsSemanticallySimilar_ForSpecificQuery()
    {
        SkipIfEmbeddingServiceUnavailable();

        // Search for a very specific medical term
        var response = await _client.PostAsJsonAsync(
            "/api/search",
            new { Query = "acute myocardial infarction heart attack", Limit = 1 }
        );

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        var results = result.GetProperty("Results");

        Assert.True(results.GetArrayLength() >= 1, "Should return at least one result");

        var topCode = results[0].GetProperty("Code").GetString()!;
        var topDesc = results[0].GetProperty("Description").GetString()!.ToLowerInvariant();

        // Top result for heart attack query should be heart-related
        var isHeartRelated =
            topCode.StartsWith("I21", StringComparison.Ordinal)
            || // MI codes
            topCode.StartsWith("I22", StringComparison.Ordinal)
            || // Subsequent MI
            topDesc.Contains("myocardial")
            || topDesc.Contains("infarction")
            || topDesc.Contains("heart")
            || topDesc.Contains("coronary");

        Assert.True(
            isHeartRelated,
            $"Top result for 'heart attack' should be heart-related. Got: {topCode} - {topDesc}"
        );
    }

    private void SkipIfEmbeddingServiceUnavailable()
    {
        if (!_factory.EmbeddingServiceAvailable)
        {
            Assert.Fail(
                "EMBEDDING SERVICE NOT RUNNING! "
                    + "Start it with: ./scripts/Dependencies/start.sh "
                    + "(localhost:8000 must be available for RAG E2E tests)"
            );
        }
    }

    private static List<string> GetCodesFromResults(JsonElement results)
    {
        var codes = new List<string>();
        foreach (var item in results.EnumerateArray())
        {
            codes.Add(item.GetProperty("Code").GetString()!);
        }
        return codes;
    }
}
