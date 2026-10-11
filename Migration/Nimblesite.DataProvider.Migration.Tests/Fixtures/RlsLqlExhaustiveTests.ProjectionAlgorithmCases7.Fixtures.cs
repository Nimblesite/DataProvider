// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsLqlExhaustiveTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> Sentinel_DoesNotLeak_Postgres_ContextCasesData()
    {
        {
            global::System.Func<global::System.String, global::System.String> input0 = Pg;
            yield return ["Sentinel_DoesNotLeak_Postgres", input0];
        }
        {
            global::System.Func<global::System.String, global::System.String> input0 = Sl;
            yield return ["Sentinel_DoesNotLeak_Sqlite", input0];
        }
        {
            global::System.Func<global::System.String, global::System.String> input0 = Mssql;
            yield return ["Sentinel_DoesNotLeak_SqlServer", input0];
        }
    }
}
