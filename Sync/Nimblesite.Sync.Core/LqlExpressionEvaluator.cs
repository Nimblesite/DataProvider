using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nimblesite.Sync.Core;

/// <summary>
/// Evaluates simple LQL expressions on JSON data for sync mapping transforms.
/// Supports: upper(col), lower(col), concat(a, b, ...), coalesce(a, b),
/// substring(col, start, len), trim(col), length(col).
/// </summary>
internal static partial class LqlExpressionEvaluator
{
    /// <summary>
    /// Evaluates an LQL expression against a JSON source object.
    /// </summary>
    /// <param name="expression">LQL expression like "upper(Name)" or "concat(First, ' ', Last)".</param>
    /// <param name="source">JSON object containing the source data.</param>
    /// <returns>Evaluated result or null if evaluation fails.</returns>
    public static object? Evaluate(string expression, JsonElement source)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        expression = expression.Trim();

        // Handle pipe syntax: ColName |> function()
        if (expression.Contains("|>", StringComparison.Ordinal))
        {
            return EvaluatePipeExpression(expression, source);
        }

        // Handle function call: function(args)
        var match = FunctionCallRegex().Match(expression);
        if (match.Success)
        {
            var functionName = match.Groups[1].Value.ToLowerInvariant();
            var argsString = match.Groups[2].Value;
            var args = ParseArguments(argsString);
            return EvaluateFunction(functionName, args, source);
        }

        // Handle simple column reference
        return GetColumnValue(expression, source);
    }

    /// <summary>
    /// Evaluates pipe syntax: ColumnName |> function('format').
    /// </summary>
    private static object? EvaluatePipeExpression(string expression, JsonElement source)
    {
        var parts = expression.Split("|>", StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        // Get initial value from first part (column name)
        var value = GetColumnValue(parts[0], source);

        // Apply each function in the pipe
        for (var i = 1; i < parts.Length; i++)
        {
            var funcPart = parts[i].Trim();
            var match = FunctionCallRegex().Match(funcPart);
            if (match.Success)
            {
                var functionName = match.Groups[1].Value.ToLowerInvariant();
                var argsString = match.Groups[2].Value;
                var args = ParseArguments(argsString);

                // Prepend the piped value as first argument
                var allArgs = new List<string> { value?.ToString() ?? "" };
                allArgs.AddRange(args);

                value = EvaluateFunctionWithValues(functionName, allArgs);
            }
        }

        return value;
    }

    /// <summary>
    /// Evaluates a function with parsed arguments.
    /// </summary>
    private static object? EvaluateFunction(
        string functionName,
        List<string> args,
        JsonElement source
    )
    {
        // Resolve column references in arguments
        var resolvedArgs = args.Select(arg => ResolveArgument(arg, source)).ToList();
        return EvaluateFunctionWithValues(functionName, resolvedArgs);
    }

    /// <summary>
    /// Evaluates a function with already-resolved values.
    /// </summary>
    private static object? EvaluateFunctionWithValues(string functionName, List<string> values) =>
        functionName switch
        {
            "upper" => values.Count > 0 ? values[0]?.ToUpperInvariant() : null,
            "lower" => values.Count > 0 ? values[0]?.ToLowerInvariant() : null,
            "trim" => values.Count > 0 ? values[0]?.Trim() : null,
            "length" => values.Count > 0 ? values[0]?.Length : null,
            "concat" => string.Concat(values),
            "coalesce" => values.FirstOrDefault(v => !string.IsNullOrEmpty(v)),
            "substring" => EvaluateSubstring(values),
            "dateformat" or "dateFormat" => EvaluateDateFormat(values),
            "replace" => EvaluateReplace(values),
            "left" => EvaluateLeft(values),
            "right" => EvaluateRight(values),
            _ => values.Count > 0 ? values[0] : null, // Unknown function, return first arg
        };

    /// <summary>
    /// Evaluates substring(value, start, length).
    /// </summary>
    private static string? EvaluateSubstring(List<string> values)
    {
        if (values.Count < 2 || string.IsNullOrEmpty(values[0]))
        {
            return null;
        }

        var str = values[0];
        if (!int.TryParse(values[1], out var start))
        {
            return null;
        }

        // Convert from 1-based to 0-based index (SQL convention)
        start = Math.Max(0, start - 1);

        if (values.Count >= 3 && int.TryParse(values[2], out var length))
        {
            return str.Length > start
                ? str.Substring(start, Math.Min(length, str.Length - start))
                : "";
        }

        return str.Length > start ? str[start..] : "";
    }

    /// <summary>
    /// Evaluates dateFormat(value, format).
    /// Uses UTC to avoid timezone conversion issues.
    /// </summary>
    private static string? EvaluateDateFormat(List<string> values)
    {
        if (values.Count < 2 || string.IsNullOrEmpty(values[0]))
        {
            return values.Count > 0 ? values[0] : null;
        }

        // Parse as DateTimeOffset to preserve the original timezone/UTC
        if (
            DateTimeOffset.TryParse(
                values[0],
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var dateOffset
            )
        )
        {
            var format = values[1].Trim('\'', '"');
            // Use UtcDateTime to avoid local timezone shifts
            return dateOffset.UtcDateTime.ToString(format, CultureInfo.InvariantCulture);
        }

        return values[0];
    }

    /// <summary>
    /// Evaluates replace(value, search, replacement).
    /// </summary>
    private static string? EvaluateReplace(List<string> values)
    {
        if (values.Count < 3 || string.IsNullOrEmpty(values[0]))
        {
            return values.Count > 0 ? values[0] : null;
        }

        var search = values[1].Trim('\'', '"');
        var replacement = values[2].Trim('\'', '"');
        return values[0].Replace(search, replacement, StringComparison.Ordinal);
    }

    /// <summary>
    /// Evaluates left(value, length).
    /// </summary>
    private static string? EvaluateLeft(List<string> values)
    {
        if (values.Count < 2 || string.IsNullOrEmpty(values[0]))
        {
            return values.Count > 0 ? values[0] : null;
        }

        if (!int.TryParse(values[1], out var length))
        {
            return values[0];
        }

        return values[0].Length > length ? values[0][..length] : values[0];
    }

    /// <summary>
    /// Evaluates right(value, length).
    /// </summary>
    private static string? EvaluateRight(List<string> values)
    {
        if (values.Count < 2 || string.IsNullOrEmpty(values[0]))
        {
            return values.Count > 0 ? values[0] : null;
        }

        if (!int.TryParse(values[1], out var length))
        {
            return values[0];
        }

        return values[0].Length > length ? values[0][^length..] : values[0];
    }

    /// <summary>
    /// Resolves an argument - either a literal string, numeric literal, or column reference.
    /// </summary>
    private static string ResolveArgument(string arg, JsonElement source)
    {
        arg = arg.Trim();

        // String literal (single or double quoted)
        if (
            (arg.StartsWith('\'') && arg.EndsWith('\''))
            || (arg.StartsWith('"') && arg.EndsWith('"'))
        )
        {
            return arg[1..^1];
        }

        // Numeric literal - pass through as-is
        if (double.TryParse(arg, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
        {
            return arg;
        }

        // Column reference
        var value = GetColumnValue(arg, source);
        return value?.ToString() ?? "";
    }

    /// <summary>
    /// Gets a column value from the JSON source.
    /// </summary>
    private static object? GetColumnValue(string columnName, JsonElement source)
    {
        columnName = columnName.Trim();

        if (source.TryGetProperty(columnName, out var prop))
        {
            return ConvertJsonElement(prop);
        }

        // Try case-insensitive match
        foreach (var property in source.EnumerateObject())
        {
            if (property.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                return ConvertJsonElement(property.Value);
            }
        }

        return null;
    }

    /// <summary>
    /// Converts a JSON element to its CLR value.
    /// </summary>
    private static object? ConvertJsonElement(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText(),
        };

    /// <summary>
    /// Parses function arguments, handling quoted strings and nested calls.
    /// </summary>
    private static List<string> ParseArguments(string argsString)
    {
        var args = new List<string>();
        var current = new System.Text.StringBuilder();
        var depth = 0;
        var inQuote = false;
        var quoteChar = '\0';

        foreach (var c in argsString)
        {
            if (!inQuote && (c == '\'' || c == '"'))
            {
                inQuote = true;
                quoteChar = c;
                current.Append(c);
            }
            else if (inQuote && c == quoteChar)
            {
                inQuote = false;
                current.Append(c);
            }
            else if (!inQuote && c == '(')
            {
                depth++;
                current.Append(c);
            }
            else if (!inQuote && c == ')')
            {
                depth--;
                current.Append(c);
            }
            else if (!inQuote && c == ',' && depth == 0)
            {
                args.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            args.Add(current.ToString().Trim());
        }

        return args;
    }

    [GeneratedRegex(@"^(\w+)\s*\((.*)\)$", RegexOptions.Singleline)]
    private static partial Regex FunctionCallRegex();
}
