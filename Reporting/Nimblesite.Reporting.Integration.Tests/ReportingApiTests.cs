using System.Net;
using System.Text;
using System.Text.Json;

namespace Nimblesite.Reporting.Integration.Tests;

/// <summary>
/// API integration tests: real HTTP requests -> real Reporting.Api -> real SQLite database.
/// No mocks. No fakes. The full API stack running end to end.
/// </summary>
[Collection("Reporting E2E")]
[Trait("Category", "E2E")]
public sealed class ReportingApiTests
{
    private readonly ReportingE2EFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public ReportingApiTests(ReportingE2EFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ListReports_ReturnsReportList_FromRealApi()
    {
        // Arrange
        using var client = ReportingE2EFixture.CreateApiClient();

        // Act
        var response = await client.GetAsync($"{_fixture.ApiUrl}/api/reports");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var reports = JsonSerializer.Deserialize<JsonElement[]>(json, JsonOptions);
        Assert.NotNull(reports);
        Assert.True(reports.Length > 0, "API should return at least one report");

        var firstReport = reports[0];
        Assert.True(firstReport.TryGetProperty("id", out var id));
        Assert.Equal("e2e-products", id.GetString());
        Assert.True(firstReport.TryGetProperty("title", out var title));
        Assert.Equal("E2E Product Report", title.GetString());

        // Verify metadata structure includes expected fields
        Assert.True(firstReport.TryGetProperty("dataSourceIds", out var dsIds));
        Assert.Equal(6, dsIds.GetArrayLength());
        Assert.True(firstReport.TryGetProperty("layout", out var layout));
        Assert.Equal(12, layout.GetProperty("columns").GetInt32());
        Assert.True(firstReport.TryGetProperty("parameters", out var parameters));
        Assert.Equal(0, parameters.GetArrayLength());
    }

    [Fact]
    public async Task GetReport_ReturnsReportMetadata_WithoutConnectionStrings()
    {
        // Arrange
        using var client = ReportingE2EFixture.CreateApiClient();

        // Act
        var response = await client.GetAsync($"{_fixture.ApiUrl}/api/reports/e2e-products");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var report = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);

        // Verify metadata fields are present
        Assert.Equal("e2e-products", report.GetProperty("id").GetString());
        Assert.Equal("E2E Product Report", report.GetProperty("title").GetString());

        // Verify data source IDs are present (6 LQL data sources)
        var dsIds = report.GetProperty("dataSourceIds");
        Assert.Equal(6, dsIds.GetArrayLength());

        // Verify layout is present
        var layout = report.GetProperty("layout");
        Assert.Equal(12, layout.GetProperty("columns").GetInt32());

        // CRITICAL: connection strings must NOT be exposed
        Assert.DoesNotContain("connectionRef", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Data Source", json, StringComparison.Ordinal);
        Assert.DoesNotContain("reporting-db", json, StringComparison.Ordinal);

        // Verify customCss is passed through to metadata
        Assert.True(report.TryGetProperty("customCss", out var customCss));
        var cssText = customCss.GetString() ?? "";
        Assert.Contains("metric-highlight", cssText, StringComparison.Ordinal);
        Assert.Contains("chart-dark-theme", cssText, StringComparison.Ordinal);
        Assert.Contains("text-banner", cssText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetReport_NonexistentId_Returns404()
    {
        // Arrange
        using var client = ReportingE2EFixture.CreateApiClient();

        // Act
        var response = await client.GetAsync($"{_fixture.ApiUrl}/api/reports/nonexistent");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ExecuteReport_ReturnsRealDataFromSQLite()
    {
        // Arrange
        using var client = ReportingE2EFixture.CreateApiClient();
        var request = new StringContent(
            """{"parameters": {}, "format": "json"}""",
            Encoding.UTF8,
            "application/json"
        );

        // Act
        var response = await client.PostAsync(
            $"{_fixture.ApiUrl}/api/reports/e2e-products/execute",
            request
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);

        Assert.Equal("e2e-products", result.GetProperty("reportId").GetString());

        var dataSources = result.GetProperty("dataSources");

        // Verify allProducts data source returned real data
        var allProducts = dataSources.GetProperty("allProducts");
        var productRows = allProducts.GetProperty("rows");
        Assert.Equal(6, allProducts.GetProperty("totalRows").GetInt32());
        Assert.Equal(6, productRows.GetArrayLength());

        // Verify column names from LQL-transpiled query
        var columns = allProducts.GetProperty("columnNames");
        Assert.Equal(5, columns.GetArrayLength());
        Assert.Contains("Id", columns.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("Name", columns.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("Category", columns.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("Price", columns.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("Stock", columns.EnumerateArray().Select(c => c.GetString()));

        // Verify actual product names appear in ordered rows (LQL order_by Name asc)
        var firstRow = productRows[0];
        var nameIdx = columns
            .EnumerateArray()
            .Select((c, i) => new { Name = c.GetString(), Index = i })
            .First(c => c.Name == "Name")
            .Index;
        Assert.Equal("Alpha Widget", firstRow[nameIdx].GetString());

        // Verify executedAt timestamp is present
        Assert.True(result.TryGetProperty("executedAt", out var executedAt));
        Assert.True(executedAt.GetString()?.Length > 0, "executedAt should be non-empty");
    }

    [Fact]
    public async Task ExecuteReport_LqlHighValueFilter_ReturnsFilteredProducts()
    {
        // Arrange
        using var client = ReportingE2EFixture.CreateApiClient();
        var request = new StringContent(
            """{"parameters": {}, "format": "json"}""",
            Encoding.UTF8,
            "application/json"
        );

        // Act
        var response = await client.PostAsync(
            $"{_fixture.ApiUrl}/api/reports/e2e-products/execute",
            request
        );

        // Assert - highValueProducts: filter(Price > 30) with arithmetic InventoryValue
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
        var highValue = result.GetProperty("dataSources").GetProperty("highValueProducts");

        // Products with Price > 30: Beta Gadget (49.99), Gamma Widget (NO - 19.99),
        // Delta Gadget (79.99), Zeta Widget (39.99) = 3 products
        Assert.True(
            highValue.GetProperty("totalRows").GetInt32() >= 3,
            "Should have at least 3 products with price > 30"
        );

        var hvColumns = highValue.GetProperty("columnNames");
        Assert.Equal(4, hvColumns.GetArrayLength());
        Assert.Contains("Name", hvColumns.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("Price", hvColumns.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("Stock", hvColumns.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("InventoryValue", hvColumns.EnumerateArray().Select(c => c.GetString()));

        // Verify all prices in result are > 30
        var priceIdx = hvColumns
            .EnumerateArray()
            .Select((c, i) => new { Name = c.GetString(), Index = i })
            .First(c => c.Name == "Price")
            .Index;
        foreach (var row in highValue.GetProperty("rows").EnumerateArray())
        {
            Assert.True(
                row[priceIdx].GetDouble() > 30,
                $"All high-value products should have price > 30, got {row[priceIdx]}"
            );
        }
    }

    [Fact]
    public async Task ExecuteReport_LqlCaseExpression_ReturnsPriceTiers()
    {
        // Arrange
        using var client = ReportingE2EFixture.CreateApiClient();
        var request = new StringContent(
            """{"parameters": {}, "format": "json"}""",
            Encoding.UTF8,
            "application/json"
        );

        // Act
        var response = await client.PostAsync(
            $"{_fixture.ApiUrl}/api/reports/e2e-products/execute",
            request
        );

        // Assert - priceAnalysis: CASE expression assigning PriceTier
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
        var priceAnalysis = result.GetProperty("dataSources").GetProperty("priceAnalysis");

        Assert.Equal(6, priceAnalysis.GetProperty("totalRows").GetInt32());

        var paColumns = priceAnalysis.GetProperty("columnNames");
        Assert.Equal(4, paColumns.GetArrayLength());
        Assert.Contains("Name", paColumns.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("Price", paColumns.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("PriceTier", paColumns.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("InventoryValue", paColumns.EnumerateArray().Select(c => c.GetString()));

        // Verify CASE expression produced valid tiers
        var tierIdx = paColumns
            .EnumerateArray()
            .Select((c, i) => new { Name = c.GetString(), Index = i })
            .First(c => c.Name == "PriceTier")
            .Index;
        var tiers = priceAnalysis
            .GetProperty("rows")
            .EnumerateArray()
            .Select(r => r[tierIdx].GetString())
            .ToArray();
        Assert.Contains("Premium", tiers);
        Assert.Contains("Standard", tiers);
        Assert.Contains("Budget", tiers);
    }

    [Fact]
    public async Task ExecuteReport_LqlGroupByHaving_ReturnsFilteredAggregates()
    {
        // Arrange
        using var client = ReportingE2EFixture.CreateApiClient();
        var request = new StringContent(
            """{"parameters": {}, "format": "json"}""",
            Encoding.UTF8,
            "application/json"
        );

        // Act
        var response = await client.PostAsync(
            $"{_fixture.ApiUrl}/api/reports/e2e-products/execute",
            request
        );

        // Assert - stockAnalysis: group_by + having(count > 1) + min/max aggregates
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
        var stockAnalysis = result.GetProperty("dataSources").GetProperty("stockAnalysis");

        // Widgets (3 items) and Gadgets (2 items) pass having(count > 1),
        // Doohickeys (1 item) filtered out
        Assert.Equal(2, stockAnalysis.GetProperty("totalRows").GetInt32());

        var saColumns = stockAnalysis.GetProperty("columnNames");
        Assert.Contains("Category", saColumns.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("Items", saColumns.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("TotalStock", saColumns.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("CheapestPrice", saColumns.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("MostExpensive", saColumns.EnumerateArray().Select(c => c.GetString()));
    }

    [Fact]
    public async Task ExecuteReport_CategorySummary_ReturnsAggregatedData()
    {
        // Arrange
        using var client = ReportingE2EFixture.CreateApiClient();
        var request = new StringContent(
            """{"parameters": {}, "format": "json"}""",
            Encoding.UTF8,
            "application/json"
        );

        // Act
        var response = await client.PostAsync(
            $"{_fixture.ApiUrl}/api/reports/e2e-products/execute",
            request
        );

        // Assert
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
        var categorySummary = result.GetProperty("dataSources").GetProperty("categorySummary");

        // We seeded 3 Widgets, 2 Gadgets, 1 Doohickey = 3 categories
        Assert.Equal(3, categorySummary.GetProperty("totalRows").GetInt32());

        var columns = categorySummary.GetProperty("columnNames");
        Assert.Equal(4, columns.GetArrayLength());
        Assert.Contains("Category", columns.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("ProductCount", columns.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("TotalStock", columns.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("AvgPrice", columns.EnumerateArray().Select(c => c.GetString()));

        // Verify actual categories present
        var catIdx = columns
            .EnumerateArray()
            .Select((c, i) => new { Name = c.GetString(), Index = i })
            .First(c => c.Name == "Category")
            .Index;
        var cats = categorySummary
            .GetProperty("rows")
            .EnumerateArray()
            .Select(r => r[catIdx].GetString())
            .ToArray();
        Assert.Contains("Widgets", cats);
        Assert.Contains("Gadgets", cats);
        Assert.Contains("Doohickeys", cats);
    }

    [Fact]
    public async Task ExecuteReport_Totals_ReturnsCorrectAggregates()
    {
        // Arrange
        using var client = ReportingE2EFixture.CreateApiClient();
        var request = new StringContent(
            """{"parameters": {}, "format": "json"}""",
            Encoding.UTF8,
            "application/json"
        );

        // Act
        var response = await client.PostAsync(
            $"{_fixture.ApiUrl}/api/reports/e2e-products/execute",
            request
        );

        // Assert
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
        var totals = result.GetProperty("dataSources").GetProperty("totals");

        Assert.Equal(1, totals.GetProperty("totalRows").GetInt32());

        // Verify column names
        var totCols = totals.GetProperty("columnNames");
        Assert.Equal(3, totCols.GetArrayLength());
        Assert.Contains("TotalProducts", totCols.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("TotalStock", totCols.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("TotalValue", totCols.EnumerateArray().Select(c => c.GetString()));

        // Verify the totals row contains correct aggregate values
        var row = totals.GetProperty("rows")[0];
        // 6 products total
        Assert.Equal(6, row[0].GetInt64());
        // Total stock > 0
        Assert.True(row[1].GetInt64() > 0, "TotalStock should be positive");
        // Total value > 0
        Assert.True(row[2].GetDouble() > 0, "TotalValue should be positive");
    }

    [Fact]
    public async Task ExecuteReport_NonexistentId_Returns404()
    {
        // Arrange
        using var client = ReportingE2EFixture.CreateApiClient();
        var request = new StringContent(
            """{"parameters": {}, "format": "json"}""",
            Encoding.UTF8,
            "application/json"
        );

        // Act
        var response = await client.PostAsync(
            $"{_fixture.ApiUrl}/api/reports/does-not-exist/execute",
            request
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ExecuteReport_AllDataSourcesPresent_InResponse()
    {
        // Arrange
        using var client = ReportingE2EFixture.CreateApiClient();
        var request = new StringContent(
            """{"parameters": {}, "format": "json"}""",
            Encoding.UTF8,
            "application/json"
        );

        // Act
        var response = await client.PostAsync(
            $"{_fixture.ApiUrl}/api/reports/e2e-products/execute",
            request
        );

        // Assert
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
        var dataSources = result.GetProperty("dataSources");

        // All 6 LQL data sources from the report definition should be present
        Assert.True(dataSources.TryGetProperty("allProducts", out _));
        Assert.True(dataSources.TryGetProperty("categorySummary", out _));
        Assert.True(dataSources.TryGetProperty("totals", out _));
        Assert.True(dataSources.TryGetProperty("highValueProducts", out _));
        Assert.True(dataSources.TryGetProperty("priceAnalysis", out _));
        Assert.True(dataSources.TryGetProperty("stockAnalysis", out _));
    }
}
