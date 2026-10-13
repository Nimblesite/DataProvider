// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Sync.Tests;

public partial class ConflictResolverTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> Resolve_LastWriteWins_NewerTimestampWins_ContextCasesData()
    {
        {
            yield return
            [
                "Resolve_LastWriteWins_NewerTimestampWins",
                "2025-01-01T00:00:00.000Z",
                "2025-01-01T00:00:01.000Z",
                ConflictStrategy.LastWriteWins,
                ConflictStrategy.LastWriteWins,
            ];
        }
        {
            yield return
            [
                "Resolve_ServerWins_AlwaysReturnsRemote",
                "2025-01-01T00:00:01.000Z",
                "2025-01-01T00:00:00.000Z",
                ConflictStrategy.ServerWins,
                ConflictStrategy.ServerWins,
            ];
        }
    }
}
