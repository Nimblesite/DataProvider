using System.Collections.Immutable;
using System.Text.Json;
using Nimblesite.Reporting.Engine;
using Xunit;

namespace Nimblesite.Reporting.Tests;

#pragma warning disable CS1591

public sealed class FormatAdapterTests
{
    [Fact]
    public void ToJson_WithValidResult_ReturnsValidJson()
    {
        // Arrange
        var result = new ReportExecutionResult(
            ReportId: "test",
            ExecutedAt: new DateTimeOffset(2025, 3, 3, 10, 0, 0, TimeSpan.Zero),
            DataSources: ImmutableDictionary<string, DataSourceResult>.Empty.Add(
                "ds1",
                new DataSourceResult(
                    ColumnNames: ["Name", "Value"],
                    Rows:
                    [
                        ImmutableArray.Create<object?>("Alpha", (object?)42),
                        ImmutableArray.Create<object?>("Beta", (object?)99),
                    ],
                    TotalRows: 2
                )
            )
        );

        // Act
        var json = FormatAdapter.ToJson(result);

        // Assert
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("test", doc.RootElement.GetProperty("reportId").GetString());

        // Verify executedAt is present and formatted
        Assert.True(doc.RootElement.TryGetProperty("executedAt", out var executedAt));
        Assert.Contains("2025", executedAt.GetString() ?? "", StringComparison.Ordinal);

        Assert.True(doc.RootElement.TryGetProperty("dataSources", out var ds));
        Assert.True(ds.TryGetProperty("ds1", out var ds1));
        Assert.Equal(2, ds1.GetProperty("totalRows").GetInt32());

        // Verify column names in JSON
        var cols = ds1.GetProperty("columnNames");
        Assert.Equal(2, cols.GetArrayLength());
        Assert.Equal("Name", cols[0].GetString());
        Assert.Equal("Value", cols[1].GetString());

        // Verify row data
        var rows = ds1.GetProperty("rows");
        Assert.Equal(2, rows.GetArrayLength());
        Assert.Equal("Alpha", rows[0][0].GetString());
        Assert.Equal(42, rows[0][1].GetInt32());
        Assert.Equal("Beta", rows[1][0].GetString());
        Assert.Equal(99, rows[1][1].GetInt32());
        Assert.Single(ds.EnumerateObject());
        Assert.Equal(3, doc.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public void ToCsv_WithValidResult_ReturnsCorrectCsv()
    {
        // Arrange
        var dsResult = new DataSourceResult(
            ColumnNames: ["Name", "Category", "Price"],
            Rows:
            [
                ImmutableArray.Create<object?>("Widget", (object?)"Tools", (object?)29.99),
                ImmutableArray.Create<object?>("Gadget", (object?)"Tech", (object?)49.99),
            ],
            TotalRows: 2
        );

        // Act
        var csv = FormatAdapter.ToCsv(dsResult);

        // Assert
        var lines = csv.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Equal("Name,Category,Price", lines[0]);
        Assert.Equal("Widget,Tools,29.99", lines[1]);
        Assert.Equal("Gadget,Tech,49.99", lines[2]);
    }

    [Fact]
    public void ToCsv_WithNullValues_HandlesGracefully()
    {
        // Arrange
        var dsResult = new DataSourceResult(
            ColumnNames: ["Name", "Notes"],
            Rows: [ImmutableArray.Create<object?>("Widget", null)],
            TotalRows: 1
        );

        // Act
        var csv = FormatAdapter.ToCsv(dsResult);

        // Assert
        var lines = csv.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Equal("Name,Notes", lines[0]);
        Assert.Equal("Widget,", lines[1]);
    }

    [Fact]
    public void ToCsv_WithCommasInValues_EscapesCorrectly()
    {
        // Arrange
        var dsResult = new DataSourceResult(
            ColumnNames: ["Name", "Description"],
            Rows: [ImmutableArray.Create<object?>("Widget", (object?)"Small, portable device")],
            TotalRows: 1
        );

        // Act
        var csv = FormatAdapter.ToCsv(dsResult);

        // Assert
        var lines = csv.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Equal("Name,Description", lines[0]);
        Assert.Contains("Widget", lines[1], StringComparison.Ordinal);
        Assert.Contains("\"Small, portable device\"", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void ToCsv_WithEmptyResult_ReturnsHeaderOnly()
    {
        // Arrange
        var dsResult = new DataSourceResult(ColumnNames: ["Name", "Price"], Rows: [], TotalRows: 0);

        // Act
        var csv = FormatAdapter.ToCsv(dsResult);

        // Assert
        Assert.Equal("Name,Price", csv);
    }

    [Theory]
    [InlineData("Comma, separated", "\"Comma, separated\"")]
    [InlineData("Said \"yes\"", "\"Said \"\"yes\"\"\"")]
    [InlineData("First line\nSecond line", "\"First line\nSecond line\"")]
    public void ToCsv_WithSpecialCharacters_EscapesEachField(string value, string encoded)
    {
        var source = new DataSourceResult(
            ColumnNames: ["Value"],
            Rows: [ImmutableArray.Create<object?>(value)],
            TotalRows: 1
        );

        Assert.Equal($"Value\n{encoded}", FormatAdapter.ToCsv(source));
    }

    [Fact]
    public void ToCsv_WithSpecialCharactersInHeadersAndRows_PreservesColumnBoundaries()
    {
        var source = new DataSourceResult(
            ColumnNames: ["Patient, Name", "Note \"text\"", "Amount"],
            Rows:
            [
                ImmutableArray.Create<object?>("Doe, Jane", "Said \"hello\"", 10.5),
                ImmutableArray.Create<object?>("Smith", null, 0.0),
            ],
            TotalRows: 2
        );

        Assert.Equal(
            "\"Patient, Name\",\"Note \"\"text\"\"\",Amount\n\"Doe, Jane\",\"Said \"\"hello\"\"\",10.5\nSmith,,0",
            FormatAdapter.ToCsv(source)
        );
    }

    [Fact]
    public void ToJson_WithMultipleSourcesAndNullCell_PreservesStructureAndTypes()
    {
        var executedAt = new DateTimeOffset(2025, 3, 3, 10, 0, 0, TimeSpan.Zero);
        var report = new ReportExecutionResult(
            ReportId: "mixed-report",
            ExecutedAt: executedAt,
            DataSources: ImmutableDictionary<string, DataSourceResult>
                .Empty.Add(
                    "patients",
                    new DataSourceResult(
                        ColumnNames: ["id", "note", "count"],
                        Rows: [ImmutableArray.Create<object?>("patient-1", null, 0)],
                        TotalRows: 1
                    )
                )
                .Add("empty", new DataSourceResult(ColumnNames: ["id"], Rows: [], TotalRows: 0))
        );

        using var document = JsonDocument.Parse(FormatAdapter.ToJson(report));
        var root = document.RootElement;
        Assert.Equal("mixed-report", root.GetProperty("reportId").GetString());
        Assert.Equal(executedAt, root.GetProperty("executedAt").GetDateTimeOffset());
        var sources = root.GetProperty("dataSources");
        Assert.Equal(2, sources.EnumerateObject().Count());
        var patients = sources.GetProperty("patients");
        Assert.Equal(1, patients.GetProperty("totalRows").GetInt32());
        Assert.Equal(3, patients.GetProperty("columnNames").GetArrayLength());
        Assert.Equal("patient-1", patients.GetProperty("rows")[0][0].GetString());
        Assert.Equal(JsonValueKind.Null, patients.GetProperty("rows")[0][1].ValueKind);
        Assert.Equal(0, patients.GetProperty("rows")[0][2].GetInt32());
        var empty = sources.GetProperty("empty");
        Assert.Equal(0, empty.GetProperty("totalRows").GetInt32());
        Assert.Empty(empty.GetProperty("rows").EnumerateArray());
    }
}
