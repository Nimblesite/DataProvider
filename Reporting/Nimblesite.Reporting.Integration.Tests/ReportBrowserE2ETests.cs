using Microsoft.Playwright;

namespace Nimblesite.Reporting.Integration.Tests;

/// <summary>
/// Full-stack browser E2E tests: real SQLite DB -> real Reporting.Api -> React renderer -> Playwright Chromium.
/// No mocks. No fakes. No shortcuts. Data flows from disk to DOM.
/// </summary>
[Collection("Reporting E2E")]
[Trait("Category", "E2E")]
public sealed class ReportBrowserE2ETests
{
    private readonly ReportingE2EFixture _fixture;

    public ReportBrowserE2ETests(ReportingE2EFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Navigate to report viewer. React loads, fetches report list from real API.
    /// Verifies the report list is rendered in the DOM.
    /// </summary>
    [Fact]
    public async Task ReportViewer_LoadsAndShowsReportList()
    {
        var page = await _fixture.CreateReportPageAsync();

        // Wait for React to render the report list
        await page.WaitForSelectorAsync(
            ".report-viewer-list, .report-list-item, .report-container",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        // Should show available reports or auto-load
        var pageContent = await page.ContentAsync();
        Assert.True(
            pageContent.Contains("E2E Product Report", StringComparison.Ordinal)
                || pageContent.Contains("Available Reports", StringComparison.Ordinal)
                || pageContent.Contains("report", StringComparison.Ordinal),
            "Page should display report content or report list"
        );

        await page.CloseAsync();
    }

    /// <summary>
    /// Navigate directly to a specific report. Verifies the report title renders.
    /// Full path: SQLite query -> API response -> React component -> DOM text.
    /// </summary>
    [Fact]
    public async Task Report_DirectLoad_ShowsReportTitle()
    {
        var page = await _fixture.CreateReportPageAsync(reportId: "e2e-products");

        // Wait for report to render (title appears when data is loaded)
        await page.WaitForSelectorAsync(
            ".report-title, .report-container",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        var titleElement = await page.QuerySelectorAsync(".report-title");
        Assert.NotNull(titleElement);
        var titleText = await titleElement.TextContentAsync();
        Assert.Equal("E2E Product Report", titleText?.Trim());

        // Verify report container exists
        var container = await page.QuerySelectorAsync(".report-container");
        Assert.NotNull(container);

        await page.CloseAsync();
    }

    /// <summary>
    /// Verify metric cards are rendered with real data from the database.
    /// The totals data source queries: COUNT(*), SUM(Stock), SUM(Price * Stock).
    /// These values come from the 6 seeded products in SQLite.
    /// </summary>
    [Fact]
    public async Task Report_MetricCards_ShowRealDatabaseValues()
    {
        var page = await _fixture.CreateReportPageAsync(reportId: "e2e-products");

        await page.WaitForSelectorAsync(
            ".report-metric",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        var metrics = await page.QuerySelectorAllAsync(".report-metric");
        Assert.True(metrics.Count >= 3, $"Expected at least 3 metric cards, got {metrics.Count}");

        // Extract metric values - they should contain real numbers from the DB
        var metricValues = new List<string>();
        foreach (var metric in metrics)
        {
            var valueEl = await metric.QuerySelectorAsync(".report-metric-value");
            if (valueEl is not null)
            {
                var text = await valueEl.TextContentAsync();
                metricValues.Add(text ?? "");
            }
        }

        // At least one metric should have a non-empty, non-dash value
        Assert.True(
            metricValues.Any(v => v != "—" && v.Length > 0),
            $"Metric values should contain real data. Got: [{string.Join(", ", metricValues)}]"
        );

        // Verify metric titles are present
        var titles = await page.QuerySelectorAllAsync(".report-metric-title");
        Assert.True(titles.Count >= 3, "Should have metric titles");

        var titleTexts = new List<string>();
        foreach (var title in titles)
        {
            titleTexts.Add(await title.TextContentAsync() ?? "");
        }

        Assert.Contains(titleTexts, t => t.Contains("Total Products", StringComparison.Ordinal));
        Assert.Contains(titleTexts, t => t.Contains("Total Stock", StringComparison.Ordinal));
        Assert.Contains(titleTexts, t => t.Contains("Total Value", StringComparison.Ordinal));

        await page.CloseAsync();
    }

    /// <summary>
    /// Verify bar charts are rendered as SVG with real data.
    /// The categorySummary data source groups products by category.
    /// Bars should be present in the SVG for each category.
    /// </summary>
    [Fact]
    public async Task Report_BarCharts_RenderSvgWithRealData()
    {
        var page = await _fixture.CreateReportPageAsync(reportId: "e2e-products");

        await page.WaitForSelectorAsync(
            ".report-chart",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        var charts = await page.QuerySelectorAllAsync(".report-chart");
        Assert.True(
            charts.Count >= 4,
            $"Expected at least 4 charts (LQL-powered), got {charts.Count}"
        );

        // Verify SVG elements exist (real bar chart rendering)
        var svgs = await page.QuerySelectorAllAsync(".report-bar-chart");
        Assert.True(svgs.Count >= 1, "Should have SVG chart elements");

        // Verify bars exist inside the SVG (rect elements are the bars)
        var bars = await page.QuerySelectorAllAsync(".report-bar-chart rect");
        Assert.True(
            bars.Count >= 3,
            $"Should have at least 3 bars (one per category: Widgets, Gadgets, Doohickeys), got {bars.Count}"
        );

        // Verify chart titles
        var chartTitles = await page.QuerySelectorAllAsync(".report-chart .report-component-title");
        Assert.True(chartTitles.Count >= 4, "Should have chart titles for all 4 LQL charts");

        var chartTitleTexts = new List<string>();
        foreach (var title in chartTitles)
        {
            chartTitleTexts.Add(await title.TextContentAsync() ?? "");
        }

        Assert.Contains(
            chartTitleTexts,
            t => t.Contains("Products by Category", StringComparison.Ordinal)
        );
        Assert.Contains(
            chartTitleTexts,
            t => t.Contains("Avg Price by Category", StringComparison.Ordinal)
        );
        Assert.Contains(
            chartTitleTexts,
            t => t.Contains("Stock Distribution by Category", StringComparison.Ordinal)
        );
        Assert.Contains(
            chartTitleTexts,
            t => t.Contains("High-Value Product Inventory", StringComparison.Ordinal)
        );

        // Verify axis labels exist in SVG
        var svgTexts = await page.QuerySelectorAllAsync(".report-bar-chart text");
        Assert.True(svgTexts.Count > 0, "SVG should contain axis labels and value labels");

        // Verify SVG contains actual category labels in text elements
        var allSvgText = "";
        foreach (var svgText in svgTexts)
        {
            allSvgText += await svgText.TextContentAsync() + " ";
        }

        Assert.Contains("Widgets", allSvgText, StringComparison.Ordinal);

        await page.CloseAsync();
    }

    /// <summary>
    /// Verify the data table renders with real product data from SQLite.
    /// Checks column headers, row count, and actual cell values.
    /// </summary>
    [Fact]
    public async Task Report_DataTable_ShowsRealProductData()
    {
        var page = await _fixture.CreateReportPageAsync(reportId: "e2e-products");

        await page.WaitForSelectorAsync(
            ".report-table",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        // Verify table header
        var headerCells = await page.QuerySelectorAllAsync(".report-table-th");
        Assert.True(headerCells.Count >= 4, $"Expected 4 table headers, got {headerCells.Count}");

        var headers = new List<string>();
        foreach (var th in headerCells)
        {
            headers.Add(await th.TextContentAsync() ?? "");
        }

        Assert.Contains("Product", headers);
        Assert.Contains("Category", headers);
        Assert.Contains("Price", headers);
        Assert.Contains("Stock", headers);

        // Verify data rows (6 products seeded)
        var dataRows = await page.QuerySelectorAllAsync(".report-table-row");
        Assert.True(
            dataRows.Count >= 6,
            $"Expected at least 6 data rows (6 seeded products), got {dataRows.Count}"
        );

        // Verify actual product names appear in the table
        var tableContent = await page.InnerTextAsync(".report-table");
        Assert.Contains("Alpha Widget", tableContent, StringComparison.Ordinal);
        Assert.Contains("Beta Gadget", tableContent, StringComparison.Ordinal);
        Assert.Contains("Gamma Widget", tableContent, StringComparison.Ordinal);
        Assert.Contains("Delta Gadget", tableContent, StringComparison.Ordinal);
        Assert.Contains("Epsilon Doohickey", tableContent, StringComparison.Ordinal);
        Assert.Contains("Zeta Widget", tableContent, StringComparison.Ordinal);

        // Verify table title
        var tableTitle = await page.QuerySelectorAsync(
            ".report-table-container .report-component-title"
        );
        Assert.NotNull(tableTitle);
        var titleText = await tableTitle.TextContentAsync();
        Assert.Contains("All Products", titleText ?? "", StringComparison.Ordinal);

        await page.CloseAsync();
    }

    /// <summary>
    /// Verify text components render with their content.
    /// </summary>
    [Fact]
    public async Task Report_TextComponent_RendersCaption()
    {
        var page = await _fixture.CreateReportPageAsync(reportId: "e2e-products");

        await page.WaitForSelectorAsync(
            ".report-text-caption",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        var caption = await page.QuerySelectorAsync(".report-text-caption");
        Assert.NotNull(caption);
        var text = await caption.TextContentAsync();
        Assert.Contains("LQL", text ?? "", StringComparison.Ordinal);
        Assert.Contains("validates full stack rendering", text ?? "", StringComparison.Ordinal);
        Assert.Contains("custom CSS styling", text ?? "", StringComparison.Ordinal);
        Assert.Contains("cssClass", text ?? "", StringComparison.Ordinal);

        // Caption should also have the text-banner custom class applied
        var captionClass = await caption.GetAttributeAsync("class");
        Assert.Contains("text-banner", captionClass ?? "", StringComparison.Ordinal);
        Assert.Contains("report-text-caption", captionClass ?? "", StringComparison.Ordinal);

        await page.CloseAsync();
    }

    /// <summary>
    /// Verify the grid layout renders cells with correct structure.
    /// Report has 6 rows of varying cell configurations (all LQL-powered).
    /// </summary>
    [Fact]
    public async Task Report_GridLayout_RendersRowsAndCells()
    {
        var page = await _fixture.CreateReportPageAsync(reportId: "e2e-products");

        await page.WaitForSelectorAsync(
            ".report-row",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        var rows = await page.QuerySelectorAllAsync(".report-row");
        Assert.True(
            rows.Count >= 6,
            $"Expected at least 6 layout rows (LQL report), got {rows.Count}"
        );

        // First row should have 3 metric cells (colSpan 4 each)
        var firstRowCells = await rows[0].QuerySelectorAllAsync(".report-cell");
        Assert.True(
            firstRowCells.Count >= 3,
            $"First row should have 3 cells, got {firstRowCells.Count}"
        );

        // Second row should have 2 chart cells (colSpan 6 each)
        var secondRowCells = await rows[1].QuerySelectorAllAsync(".report-cell");
        Assert.True(
            secondRowCells.Count >= 2,
            $"Second row should have 2 cells, got {secondRowCells.Count}"
        );

        // Third row should have 2 chart cells (colSpan 6 each)
        var thirdRowCells = await rows[2].QuerySelectorAllAsync(".report-cell");
        Assert.True(
            thirdRowCells.Count >= 2,
            $"Third row should have 2 chart cells, got {thirdRowCells.Count}"
        );

        // Verify colSpan classes are applied: first row has 3 x report-cell-4
        var cell4s = await rows[0].QuerySelectorAllAsync("[class*='report-cell-4']");
        Assert.Equal(3, cell4s.Count);

        // Second row has 2 x report-cell-6
        var cell6s = await rows[1].QuerySelectorAllAsync("[class*='report-cell-6']");
        Assert.Equal(2, cell6s.Count);

        // Fourth row is full-width table (report-cell-12)
        var fourthRowCells = await rows[3].QuerySelectorAllAsync("[class*='report-cell-12']");
        Assert.True(fourthRowCells.Count >= 1, "Fourth row should have a full-width cell");

        await page.CloseAsync();
    }

    /// <summary>
    /// Verify the full rendering pipeline by checking that ALL component types
    /// appear on a single rendered report page. This is the ultimate E2E test:
    /// database -> API -> React -> metrics + charts + table + text all visible.
    /// </summary>
    [Fact]
    public async Task Report_FullPipeline_AllComponentTypesRendered()
    {
        var page = await _fixture.CreateReportPageAsync(reportId: "e2e-products");

        // Wait for the slowest component (table with data)
        await page.WaitForSelectorAsync(
            ".report-table-row",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Metrics rendered
        var metrics = await page.QuerySelectorAllAsync(".report-metric");
        Assert.True(metrics.Count > 0, "Metrics should be rendered");

        // Charts rendered with SVG
        var svgCharts = await page.QuerySelectorAllAsync(".report-bar-chart");
        Assert.True(svgCharts.Count > 0, "SVG bar charts should be rendered");

        // Table rendered with data
        var tableRows = await page.QuerySelectorAllAsync(".report-table-row");
        Assert.True(tableRows.Count > 0, "Table rows should be rendered");

        // Text rendered
        var text = await page.QuerySelectorAllAsync(".report-text-caption");
        Assert.True(text.Count > 0, "Text caption should be rendered");

        // Report container with title
        var container = await page.QuerySelectorAsync(".report-container");
        Assert.NotNull(container);
        var title = await page.QuerySelectorAsync(".report-title");
        Assert.NotNull(title);
        var titleText = await title.TextContentAsync();
        Assert.Equal("E2E Product Report", titleText?.Trim());

        // Verify no error states visible
        var errors = await page.QuerySelectorAllAsync(".report-error, .report-viewer-error");
        Assert.True(
            errors.Count == 0,
            "No error elements should be visible on a successful render"
        );

        // Verify no unknown component types
        var unknowns = await page.QuerySelectorAllAsync(".report-unknown-component");
        Assert.True(unknowns.Count == 0, "No unknown component elements should be rendered");

        // Verify custom CSS is also applied in the full pipeline
        var styledMetrics = await page.QuerySelectorAllAsync(".report-metric.metric-highlight");
        Assert.True(
            styledMetrics.Count >= 2,
            "Custom CSS classes should be applied in full pipeline"
        );

        await page.CloseAsync();
    }

    /// <summary>
    /// Verify that report-level customCss is injected as a style tag in the DOM.
    /// The e2e report defines custom classes (metric-highlight, chart-dark-theme, etc.)
    /// and these must be present in an injected style element.
    /// </summary>
    [Fact]
    public async Task Report_CustomCss_InjectsStyleTag()
    {
        var page = await _fixture.CreateReportPageAsync(reportId: "e2e-products");

        await page.WaitForSelectorAsync(
            ".report-container",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        // Verify the injected <style> tag contains our custom CSS classes
        var styleContent = await page.EvaluateAsync<string>(
            "() => { const s = document.querySelector('.report-container style'); return s ? s.innerHTML : ''; }"
        );

        Assert.Contains("metric-highlight", styleContent, StringComparison.Ordinal);
        Assert.Contains("chart-dark-theme", styleContent, StringComparison.Ordinal);
        Assert.Contains("table-striped-accent", styleContent, StringComparison.Ordinal);
        Assert.Contains("text-banner", styleContent, StringComparison.Ordinal);
        Assert.Contains("table-compact", styleContent, StringComparison.Ordinal);
        Assert.Contains("metric-currency", styleContent, StringComparison.Ordinal);

        await page.CloseAsync();
    }

    /// <summary>
    /// Verify that component-level cssClass is applied to rendered DOM elements.
    /// Metrics should have metric-highlight/metric-currency classes,
    /// charts should have chart-dark-theme, tables should have table-striped-accent/table-compact,
    /// and the text caption should have text-banner.
    /// </summary>
    [Fact]
    public async Task Report_CustomCssClass_AppliedToComponents()
    {
        var page = await _fixture.CreateReportPageAsync(reportId: "e2e-products");

        await page.WaitForSelectorAsync(
            ".report-metric",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        // Metric components with custom cssClass
        var highlightMetrics = await page.QuerySelectorAllAsync(".report-metric.metric-highlight");
        Assert.True(
            highlightMetrics.Count >= 2,
            $"Expected at least 2 metric-highlight metrics, got {highlightMetrics.Count}"
        );

        var currencyMetric = await page.QuerySelectorAllAsync(".report-metric.metric-currency");
        Assert.True(
            currencyMetric.Count >= 1,
            $"Expected at least 1 metric-currency metric, got {currencyMetric.Count}"
        );

        // Chart components with custom cssClass
        var darkCharts = await page.QuerySelectorAllAsync(".report-chart.chart-dark-theme");
        Assert.True(
            darkCharts.Count >= 2,
            $"Expected at least 2 chart-dark-theme charts, got {darkCharts.Count}"
        );

        // Table components with custom cssClass
        var accentTables = await page.QuerySelectorAllAsync(
            ".report-table-container.table-striped-accent"
        );
        Assert.True(
            accentTables.Count >= 1,
            $"Expected at least 1 table-striped-accent table, got {accentTables.Count}"
        );

        var compactTables = await page.QuerySelectorAllAsync(
            ".report-table-container.table-compact"
        );
        Assert.True(
            compactTables.Count >= 1,
            $"Expected at least 1 table-compact table, got {compactTables.Count}"
        );

        // Text component with custom cssClass
        var bannerTexts = await page.QuerySelectorAllAsync(".report-text-caption.text-banner");
        Assert.True(
            bannerTexts.Count >= 1,
            $"Expected at least 1 text-banner caption, got {bannerTexts.Count}"
        );

        await page.CloseAsync();
    }

    /// <summary>
    /// Verify that component-level cssStyle (inline styles) is applied to rendered elements.
    /// The Total Value metric has cssStyle: fontWeight=bold, and a chart has borderTop.
    /// </summary>
    [Fact]
    public async Task Report_CustomCssStyle_AppliedAsInlineStyle()
    {
        var page = await _fixture.CreateReportPageAsync(reportId: "e2e-products");

        await page.WaitForSelectorAsync(
            ".report-metric.metric-currency",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        // The currency metric should have inline font-weight: bold
        var fontWeight = await page.EvaluateAsync<string>(
            "() => { const el = document.querySelector('.report-metric.metric-currency'); return el ? getComputedStyle(el).fontWeight : ''; }"
        );
        Assert.True(
            fontWeight == "bold" || fontWeight == "700",
            $"Expected font-weight bold/700 on currency metric, got '{fontWeight}'"
        );

        // Verify the compact table has inline border style
        var tableBorder = await page.EvaluateAsync<string>(
            "() => { const el = document.querySelector('.report-table-container.table-compact'); return el ? el.style.border : ''; }"
        );
        Assert.Contains("2px", tableBorder, StringComparison.Ordinal);
        Assert.Contains("2E4450", tableBorder, StringComparison.OrdinalIgnoreCase);

        // Verify chart with inline borderTop
        var chartBorder = await page.EvaluateAsync<string>(
            "() => { const el = document.querySelector('.report-chart:not(.chart-dark-theme)'); return el ? el.style.borderTop : ''; }"
        );
        Assert.Contains("FF6B6B", chartBorder, StringComparison.OrdinalIgnoreCase);

        await page.CloseAsync();
    }

    /// <summary>
    /// Verify that cell-level cssClass is applied to the grid cell wrapper.
    /// The stock distribution chart cell has cssClass: custom-cell-padded.
    /// </summary>
    [Fact]
    public async Task Report_CellCssClass_AppliedToGridCell()
    {
        var page = await _fixture.CreateReportPageAsync(reportId: "e2e-products");

        await page.WaitForSelectorAsync(
            ".report-cell.custom-cell-padded",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        var paddedCells = await page.QuerySelectorAllAsync(".report-cell.custom-cell-padded");
        Assert.True(
            paddedCells.Count >= 1,
            $"Expected at least 1 custom-cell-padded cell, got {paddedCells.Count}"
        );

        await page.CloseAsync();
    }

    /// <summary>
    /// Verify that the browser console has no uncaught JavaScript errors
    /// during report rendering. Catches transpilation issues in H5 output.
    /// </summary>
    [Fact]
    public async Task Report_NoJavaScriptErrors_DuringRender()
    {
        var page = await _fixture.CreatePageAsync();
        var jsErrors = new List<string>();

        page.PageError += (_, err) => jsErrors.Add(err);

        await page.AddInitScriptAsync(
            $@"window.reportConfig = {{
                apiBaseUrl: '{_fixture.ApiUrl}',
                reportId: 'e2e-products'
            }};"
        );

        await page.GotoAsync(_fixture.FrontendUrl);

        // Give it time to fully render
        await page.WaitForSelectorAsync(
            ".report-container, .report-viewer-loading, .report-viewer-error",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        // Allow a brief moment for any async errors
        await Task.Delay(1000);

        Assert.True(
            jsErrors.Count == 0,
            $"Expected no JS errors, got {jsErrors.Count}: [{string.Join("; ", jsErrors)}]"
        );

        await page.CloseAsync();
    }
}
