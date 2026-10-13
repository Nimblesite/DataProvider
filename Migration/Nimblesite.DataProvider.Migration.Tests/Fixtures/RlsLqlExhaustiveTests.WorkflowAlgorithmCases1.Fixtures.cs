// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsLqlExhaustiveTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> StringLiteralWithEscapedQuote_Survives_ContextCasesData()
    {
        {
            global::System.Func<global::System.String, global::System.String> input0 = Pg;
            yield return
            [
                "StringLiteralWithEscapedQuote_Survives",
                input0,
                "name = 'O''Brien'",
                "'O''Brien'",
            ];
        }
        {
            global::System.Func<global::System.String, global::System.String> input0 = Sl;
            yield return
            [
                "MixedCaseIdentifier_Sqlite_BracketsPreserveCase",
                input0,
                "OwnerId = current_user_id()",
                "[OwnerId]",
            ];
        }
        {
            global::System.Func<global::System.String, global::System.String> input0 = Sl;
            yield return
            [
                "Sqlite_NewRowReference_ColumnQuotedAsTriggerExpr",
                input0,
                "NEW.tenant_id = current_user_id()",
                "__rls_context",
            ];
        }
    }
}
