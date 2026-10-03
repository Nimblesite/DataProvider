using Nimblesite.DataProvider.Migration.Tests;
using Xunit;

namespace Nimblesite.Reporting.Tests;

// Implements [MIG-TEST-CROSS-PLATFORM]: each database container starts on demand.
/// <summary>Shares lazy database fixtures across report engine platform cases.</summary>
[CollectionDefinition(Name)]
public sealed record ReportEnginePlatformSuite
    : ICollectionFixture<MigrationPostgresContainerFixture>,
        ICollectionFixture<SqlServerContainerFixture>
{
    /// <summary>The xUnit collection name.</summary>
    public const string Name = "Report engine platforms";
}
