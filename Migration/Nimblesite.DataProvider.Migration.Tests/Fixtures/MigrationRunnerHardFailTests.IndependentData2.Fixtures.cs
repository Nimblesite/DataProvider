// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial record MigrationPlatformRegressionTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.MigrationOptions Dataset_ApplyFailureScenario_1(
        global::System.Boolean continueOnError
    ) => new MigrationOptions { ContinueOnError = continueOnError, UseTransaction = false };
}
