// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Pure immutable fixture definitions; assertions and database workflows remain analysed.

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class PostgresTriggerDdlTests
{
    private static TriggerDefinition Guard() =>
        new()
        {
            Name = "assert_not_last_owner",
            Events = [TriggerEvent.Delete],
            RaiseWhenLql = "old.role = 'owner'",
            ErrorMessage = "cannot remove the last owner of a tenant",
        };
}
