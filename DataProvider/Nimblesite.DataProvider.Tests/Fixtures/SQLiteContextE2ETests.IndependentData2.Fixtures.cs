// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Tests;

public partial class SQLiteContextE2ETests
{
    private static global::Nimblesite.Sql.Model.JoinRelationship Dataset_PipelineProcessor_JoinStep_GeneratesJoinSql_1() =>
        new JoinRelationship(
            LeftTable: "users",
            RightTable: "orders",
            Condition: "users.id = orders.user_id",
            JoinType: "INNER"
        );
}
