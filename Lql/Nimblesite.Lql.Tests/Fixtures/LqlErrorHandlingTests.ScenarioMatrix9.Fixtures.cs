// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.Lql.Tests;

public partial class LqlErrorHandlingTests
{
    /// <summary>Independent input and expected-value rows for the named scenarios.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentScenarioData9()
    {
        {
            yield return
            [
                "InvalidSyntax_ShouldReturnError",
                """
                    users |> select(invalid syntax here
                    """,
            ];
        }
        {
            yield return
            [
                "InvalidCharacter_ShouldReturnLexerError",
                """
                    users |> select(users.id) #
                    """,
            ];
        }
    }
}
