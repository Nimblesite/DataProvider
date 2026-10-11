// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Tests;

public partial class SqlQueryableTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> PredicateBuilder_True_GeneratesAlwaysTrueExpression_ContextCasesData()
    {
        {
            yield return
            [
                "PredicateBuilder_True_GeneratesAlwaysTrueExpression",
                new global::System.Func<global::System.Linq.Expressions.Expression<global::System.Func<
                    global::Nimblesite.DataProvider.Tests.SqlQueryableTests.User,
                    global::System.Boolean
                >>>(PredicateBuilder.True<User>),
            ];
        }
        {
            yield return
            [
                "PredicateBuilder_False_GeneratesAlwaysFalseExpression",
                new global::System.Func<global::System.Linq.Expressions.Expression<global::System.Func<
                    global::Nimblesite.DataProvider.Tests.SqlQueryableTests.User,
                    global::System.Boolean
                >>>(PredicateBuilder.False<User>),
            ];
        }
    }
}
