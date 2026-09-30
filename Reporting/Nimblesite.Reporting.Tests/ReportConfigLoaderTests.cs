using Microsoft.Extensions.Logging;
using Nimblesite.Reporting.Engine;
using Xunit;
using LoadError = Outcome.Result<
    Nimblesite.Reporting.Engine.ReportDefinition,
    Nimblesite.Sql.Model.SqlError
>.Error<Nimblesite.Reporting.Engine.ReportDefinition, Nimblesite.Sql.Model.SqlError>;
using LoadOk = Outcome.Result<
    Nimblesite.Reporting.Engine.ReportDefinition,
    Nimblesite.Sql.Model.SqlError
>.Ok<Nimblesite.Reporting.Engine.ReportDefinition, Nimblesite.Sql.Model.SqlError>;

namespace Nimblesite.Reporting.Tests;

#pragma warning disable CS1591

public sealed class ReportConfigLoaderTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    public ReportConfigLoaderTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = _loggerFactory.CreateLogger<ReportConfigLoaderTests>();
    }

    [Fact]
    public void LoadFromJson_WithValidJson_ReturnsReportDefinition()
    {
        // Arrange
        var json = """
            {
                "id": "test-report",
                "title": "Test Report",
                "parameters": [
                    { "name": "startDate", "type": "Date", "label": "Start", "required": true, "default": null }
                ],
                "dataSources": [
                    {
                        "id": "ds1",
                        "type": "Sql",
                        "connectionRef": "my-db",
                        "query": "SELECT * FROM products",
                        "parameters": ["startDate"]
                    }
                ],
                "layout": {
                    "columns": 12,
                    "rows": [
                        {
                            "cells": [
                                {
                                    "colSpan": 12,
                                    "component": {
                                        "type": "Table",
                                        "dataSource": "ds1",
                                        "title": "Products"
                                    }
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        // Act
        var result = ReportConfigLoader.LoadFromJson(json: json, logger: _logger);

        // Assert
        var report = Assert.IsType<LoadOk>(result).Value;
        Assert.Equal("test-report", report.Id);
        Assert.Equal("Test Report", report.Title);
        Assert.Single(report.Parameters);
        Assert.Equal("startDate", report.Parameters[0].Name);
        Assert.Equal(ParameterType.Date, report.Parameters[0].Type);
        Assert.Single(report.DataSources);
        Assert.Equal("ds1", report.DataSources[0].Id);
        Assert.Equal(DataSourceType.Sql, report.DataSources[0].Type);
        Assert.Equal("my-db", report.DataSources[0].ConnectionRef);
        Assert.Equal("SELECT * FROM products", report.DataSources[0].Query);
        Assert.Single(report.DataSources[0].Parameters);
        Assert.Equal("startDate", report.DataSources[0].Parameters[0]);
        Assert.True(report.Parameters[0].Required);
        Assert.Null(report.Parameters[0].Default);
        Assert.Equal("Start", report.Parameters[0].Label);
        Assert.Single(report.Layout.Rows);
        Assert.Equal(12, report.Layout.Columns);
        Assert.Single(report.Layout.Rows[0].Cells);
        Assert.Equal(12, report.Layout.Rows[0].Cells[0].ColSpan);
        Assert.Equal(ComponentType.Table, report.Layout.Rows[0].Cells[0].Component.Type);
        Assert.Equal("ds1", report.Layout.Rows[0].Cells[0].Component.DataSource);
        Assert.Equal("Products", report.Layout.Rows[0].Cells[0].Component.Title);
        Assert.Null(report.CustomCss);
    }

    [Fact]
    public void LoadFromJson_WithInvalidJson_ReturnsError()
    {
        // Arrange
        var invalidJson = "{ not valid json }}}";

        // Act
        var result = ReportConfigLoader.LoadFromJson(json: invalidJson, logger: _logger);

        // Assert
        var err = Assert.IsType<LoadError>(result);
        Assert.NotNull(err.Value.Message);
        Assert.True(
            err.Value.Message.Length > 0,
            "Error message should describe the parse failure"
        );
    }

    [Fact]
    public void LoadFromJson_WithMultipleDataSources_ParsesAll()
    {
        // Arrange
        var json = """
            {
                "id": "multi-ds",
                "title": "Multi DS",
                "parameters": [],
                "dataSources": [
                    { "id": "sql-ds", "type": "Sql", "connectionRef": "db", "query": "SELECT 1", "parameters": [] },
                    { "id": "lql-ds", "type": "Lql", "connectionRef": "db", "query": "products |> select(Name)", "parameters": [] }
                ],
                "layout": { "columns": 12, "rows": [] }
            }
            """;

        // Act
        var result = ReportConfigLoader.LoadFromJson(json: json, logger: _logger);

        // Assert
        var report = Assert.IsType<LoadOk>(result).Value;
        Assert.Equal("multi-ds", report.Id);
        Assert.Equal("Multi DS", report.Title);
        Assert.Empty(report.Parameters);
        Assert.Equal(2, report.DataSources.Length);
        Assert.Equal("sql-ds", report.DataSources[0].Id);
        Assert.Equal(DataSourceType.Sql, report.DataSources[0].Type);
        Assert.Equal("db", report.DataSources[0].ConnectionRef);
        Assert.Equal("SELECT 1", report.DataSources[0].Query);
        Assert.Equal("lql-ds", report.DataSources[1].Id);
        Assert.Equal(DataSourceType.Lql, report.DataSources[1].Type);
        Assert.Equal("products |> select(Name)", report.DataSources[1].Query);
        Assert.Equal(12, report.Layout.Columns);
        Assert.Empty(report.Layout.Rows);
    }

    [Fact]
    public void LoadFromJson_WithLayoutComponents_ParsesCorrectly()
    {
        // Arrange
        var json = """
            {
                "id": "layout-test",
                "title": "Layout Test",
                "parameters": [],
                "dataSources": [],
                "layout": {
                    "columns": 12,
                    "rows": [
                        {
                            "cells": [
                                {
                                    "colSpan": 4,
                                    "component": {
                                        "type": "Metric",
                                        "dataSource": "ds1",
                                        "title": "Total",
                                        "value": "count",
                                        "format": "number"
                                    }
                                },
                                {
                                    "colSpan": 8,
                                    "component": {
                                        "type": "Chart",
                                        "chartType": "Bar",
                                        "dataSource": "ds1",
                                        "title": "Chart",
                                        "xAxis": { "field": "category" },
                                        "yAxis": { "field": "count", "label": "Count" }
                                    }
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        // Act
        var result = ReportConfigLoader.LoadFromJson(json: json, logger: _logger);

        // Assert
        var report = Assert.IsType<LoadOk>(result).Value;
        Assert.Single(report.Layout.Rows);
        Assert.Equal(2, report.Layout.Rows[0].Cells.Length);

        var metricCell = report.Layout.Rows[0].Cells[0];
        Assert.Equal(4, metricCell.ColSpan);
        var metric = metricCell.Component;
        Assert.Equal(ComponentType.Metric, metric.Type);
        Assert.Equal("ds1", metric.DataSource);
        Assert.Equal("Total", metric.Title);
        Assert.Equal("count", metric.Value);
        Assert.Equal("number", metric.Format);
        Assert.Null(metric.CssClass);
        Assert.Null(metric.CssStyle);

        var chartCell = report.Layout.Rows[0].Cells[1];
        Assert.Equal(8, chartCell.ColSpan);
        var chart = chartCell.Component;
        Assert.Equal(ComponentType.Chart, chart.Type);
        Assert.Equal(ChartType.Bar, chart.ChartType);
        Assert.Equal("ds1", chart.DataSource);
        Assert.Equal("Chart", chart.Title);
        Assert.Equal("category", chart.XAxis?.Field);
        Assert.Null(chart.XAxis?.Label);
        Assert.Equal("count", chart.YAxis?.Field);
        Assert.Equal("Count", chart.YAxis?.Label);
    }

    [Fact]
    public void LoadFromJson_WithCustomCssAndCssClass_ParsesCorrectly()
    {
        // Arrange
        var json = """
            {
                "id": "styled-report",
                "title": "Styled Report",
                "parameters": [],
                "customCss": ".my-highlight { background: red; } .dark-chart { background: #1a2332; }",
                "dataSources": [],
                "layout": {
                    "columns": 12,
                    "rows": [
                        {
                            "cells": [
                                {
                                    "colSpan": 6,
                                    "cssClass": "custom-cell",
                                    "component": {
                                        "type": "Metric",
                                        "dataSource": "ds1",
                                        "title": "Styled Metric",
                                        "value": "total",
                                        "format": "number",
                                        "cssClass": "my-highlight",
                                        "cssStyle": { "fontWeight": "bold", "color": "#ff0000" }
                                    }
                                },
                                {
                                    "colSpan": 6,
                                    "component": {
                                        "type": "Chart",
                                        "chartType": "Bar",
                                        "dataSource": "ds1",
                                        "title": "Dark Chart",
                                        "xAxis": { "field": "x" },
                                        "yAxis": { "field": "y" },
                                        "cssClass": "dark-chart"
                                    }
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        // Act
        var result = ReportConfigLoader.LoadFromJson(json: json, logger: _logger);

        // Assert
        var report = Assert.IsType<LoadOk>(result).Value;

        // Report-level customCss
        Assert.Equal(
            ".my-highlight { background: red; } .dark-chart { background: #1a2332; }",
            report.CustomCss
        );

        // Cell-level cssClass
        Assert.Equal("custom-cell", report.Layout.Rows[0].Cells[0].CssClass);
        Assert.Null(report.Layout.Rows[0].Cells[1].CssClass);

        // Component-level cssClass
        var metric = report.Layout.Rows[0].Cells[0].Component;
        Assert.Equal("my-highlight", metric.CssClass);
        Assert.NotNull(metric.CssStyle);
        Assert.Equal("bold", metric.CssStyle!["fontWeight"]);
        Assert.Equal("#ff0000", metric.CssStyle!["color"]);

        // Component cssClass without cssStyle
        var chart = report.Layout.Rows[0].Cells[1].Component;
        Assert.Equal("dark-chart", chart.CssClass);
        Assert.Null(chart.CssStyle);
    }

    [Fact]
    public void LoadFromFile_WithNonexistentFile_ReturnsError()
    {
        // Act
        var result = ReportConfigLoader.LoadFromFile(
            filePath: "/tmp/nonexistent-report.json",
            logger: _logger
        );

        // Assert
        var err = Assert.IsType<LoadError>(result);
        Assert.NotNull(err.Value.Message);
        Assert.True(err.Value.Message.Length > 0, "Error message should describe file not found");
    }

    public void Dispose()
    {
        _loggerFactory?.Dispose();
    }
}
