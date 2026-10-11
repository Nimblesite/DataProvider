// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SqliteRlsMigrationTests
{
    private const string Data_Sqlite_TriggerPredicate_IgnoresKeywordsInsideComments_1 = """
            CREATE TRIGGER [rls_insert_owner_Documents] -- BEGIN
            BEFORE INSERT ON [Documents]
            BEGIN
              SELECT RAISE(ABORT, 'RLS') WHERE NOT (NEW.[OwnerId] = 'allowed');
            END -- END
            """;

    private const string Data_GroupMembershipPolicy_1 = """
            exists(
              UserGroupMemberships
              |> filter(fn(m) => m.UserId = current_user_id())
            )
            """;
}
