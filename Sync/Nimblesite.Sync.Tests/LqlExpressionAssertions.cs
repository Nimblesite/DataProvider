using System.Text.Json;

namespace Nimblesite.Sync.Tests;

// Implements [SYNC-EXPRESSION-TEST-CASES].
internal static class LqlExpressionAssertions
{
    internal static void EvaluatesTo(string json, string expression, string expected)
    {
        using var source = JsonDocument.Parse(json: json);
        var result = LqlExpressionEvaluator.Evaluate(
            expression: expression,
            source: source.RootElement
        );
        Assert.Equal(expected: expected, actual: result);
    }
}
