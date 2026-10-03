using System.Text.Json;

namespace Nimblesite.Reporting.Engine;

/// <summary>
/// Serializes report execution results to various output formats.
/// </summary>
public static class FormatAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary>
    /// Serializes a report execution result to JSON.
    /// </summary>
    /// <param name="result">The execution result to serialize.</param>
    /// <returns>JSON string representation of the result.</returns>
    public static string ToJson(ReportExecutionResult result) =>
        JsonSerializer.Serialize(value: result, options: JsonOptions);

    /// <summary>
    /// Serializes a report execution result to CSV for a specific data source.
    /// </summary>
    /// <param name="result">The data source result to serialize.</param>
    /// <returns>CSV string representation.</returns>
    public static string ToCsv(DataSourceResult result)
    {
        var lines = new List<string>(capacity: result.TotalRows + 1)
        {
            string.Join(",", result.ColumnNames.Select(EscapeCsvValue)),
        };

        foreach (var row in result.Rows)
        {
            lines.Add(string.Join(",", row.Select(value => EscapeCsvValue(value?.ToString()))));
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Escapes a single CSV field per RFC 4180: fields containing commas,
    /// quotes, or newlines are wrapped in double quotes and embedded
    /// quotes are doubled.
    /// </summary>
    /// <param name="value">The raw field value.</param>
    /// <returns>The escaped CSV field.</returns>
    private static string EscapeCsvValue(string? value)
    {
        var str = value ?? "";
        if (
            str.Contains(',', StringComparison.Ordinal)
            || str.Contains('"', StringComparison.Ordinal)
            || str.Contains('\n', StringComparison.Ordinal)
        )
        {
            return $"\"{str.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return str;
    }
}
