// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresEdgeCaseTests
{
    private const string Data_ForeignKey_AllCascadeTypes_Success_1 =
        @"
            INSERT INTO parent_table (id) VALUES (1);
            INSERT INTO child_cascade (id, parent_id) VALUES (1, 1);
            INSERT INTO child_setnull (id, parent_id) VALUES (1, 1);
            DELETE FROM parent_table WHERE id = 1;
        ";
}
