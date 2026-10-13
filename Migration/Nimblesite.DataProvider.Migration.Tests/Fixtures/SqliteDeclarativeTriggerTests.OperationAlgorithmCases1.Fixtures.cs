// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SqliteDeclarativeTriggerTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> Sqlite_TriggerGuard_AllowsDeleteWhenAnotherOwnerExists_ContextCasesData()
    {
        {
            global::System.Action<
                global::Microsoft.Data.Sqlite.SqliteConnection,
                global::System.String
            > input5 = DeleteMember;
            yield return
            [
                "Sqlite_TriggerGuard_AllowsDeleteWhenAnotherOwnerExists",
                input5,
                new global::System.Func<
                    global::Microsoft.Data.Sqlite.SqliteConnection,
                    global::System.String,
                    global::System.Int32
                >(SqliteTestDb.CountRows),
                "TenantMembers",
            ];
        }
        {
            global::System.Action<
                global::Microsoft.Data.Sqlite.SqliteConnection,
                global::System.String
            > input5 = DemoteMember;
            yield return
            [
                "Sqlite_TriggerGuard_AllowsDemoteWhenAnotherOwnerExists",
                input5,
                new global::System.Func<
                    global::Microsoft.Data.Sqlite.SqliteConnection,
                    global::System.String,
                    global::System.Int32
                >(SqliteTestDb.Count),
                "SELECT COUNT(*) FROM [TenantMembers] WHERE [Role]='owner'",
            ];
        }
    }

    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> Sqlite_TriggerGuard_BlocksLastOwnerAction_ContextCasesData()
    {
        yield return
        [
            "Sqlite_TriggerGuard_BlocksDemoteOfLastOwner",
            (Action<SqliteConnection, string>)DemoteMember,
            MemberC,
            Tenant1,
            "member",
        ];
        yield return
        [
            "Sqlite_TriggerGuard_IsScopedToTenant",
            (Action<SqliteConnection, string>)DeleteMember,
            OwnerB,
            Tenant2,
            "owner",
        ];
    }
}
