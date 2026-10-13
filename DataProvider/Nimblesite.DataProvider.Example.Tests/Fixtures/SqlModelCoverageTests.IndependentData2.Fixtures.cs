// Implements [CI-DESLOP-INDEPENDENT-DATA].

using Nimblesite.Sql.Model;

namespace Nimblesite.DataProvider.Example.Tests;

public partial class SqlModelCoverageTests
{
    private static global::Nimblesite.Sql.Model.SqlError Dataset_SqlError_DetailedMessage_WithExceptionAndSource_IncludesAllDetails_1(
        global::System.InvalidOperationException outerEx,
        global::System.ArgumentException innerEx
    ) =>
        new SqlError("Parse failed", outerEx, new SourcePosition(1, 5), "SELECT * FROM bad_table")
        {
            InnerException = innerEx,
        };
}
