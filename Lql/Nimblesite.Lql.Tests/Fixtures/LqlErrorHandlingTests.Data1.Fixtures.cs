// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Lql.Tests;

public partial class LqlErrorHandlingTests
{
    private const string Data_CircularReference_ShouldReturnError_1 = """
        let a = b |> select(id)
        let b = a |> select(name)
        a
        """;

    private const string Data_ErrorMessage_ShouldIncludeLineAndColumn_1 = """
        users |> select(
            users.id,
            users.name,
            invalid_syntax_here
        """;
}
