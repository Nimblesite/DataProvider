// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.Lql.Tests;

public partial class LqlErrorHandlingTests
{
    public static IEnumerable<object?[]> MissingPipeOperator_ShouldReturnError_CasesData()
    {
        yield return
        [
            "MissingPipeOperator_ShouldReturnError",
            """
                users select(users.id, users.name)
                """,
        ];
        yield return
        [
            "InvalidTableName_ShouldReturnError",
            """
                123_invalid_table |> select(id, name)
                """,
        ];
        yield return
        [
            "UnclosedParentheses_ShouldReturnError",
            """
                users |> select(users.id, users.name
                """,
        ];
        yield return
        [
            "InvalidJoinSyntax_ShouldReturnError",
            """
                users |> join(orders, invalid_join_syntax)
                """,
        ];
        yield return
        [
            "MissingOnClauseInJoin_ShouldReturnError",
            """
                users |> join(orders)
                """,
        ];
        yield return
        [
            "InvalidFilterFunction_ShouldReturnError",
            """
                users |> filter(invalid_filter_function)
                """,
        ];
        yield return
        [
            "CircularReference_ShouldReturnError",
            Data_CircularReference_ShouldReturnError_1,
        ];
    }
}
