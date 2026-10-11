// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.DataProvider.Example.Tests;

public partial class DataProviderIntegrationTests
{
    /// <summary>Returns independent typed query inputs and expectations.</summary>
    public static IEnumerable<object?[]> FluentQueryBuilder_WhereWithBoolValue_GeneratesCorrectSQL_ContextCasesData()
    {
        {
            yield return
            [
                "FluentQueryBuilder_WhereWithBoolValue_GeneratesCorrectSQL",
                "o.IsActive",
                true,
                "WHERE o.IsActive = 1",
            ];
        }
        {
            yield return
            [
                "FluentQueryBuilder_WhereWithIntValue_GeneratesCorrectSQL",
                "o.CustomerId",
                42,
                "WHERE o.CustomerId = 42",
            ];
        }
        {
            yield return
            [
                "FluentQueryBuilder_WhereWithStringContainingQuote_EscapesCorrectly",
                "o.CustomerName",
                "O'Brien",
                "WHERE o.CustomerName = 'O''Brien'",
            ];
        }
    }
}
