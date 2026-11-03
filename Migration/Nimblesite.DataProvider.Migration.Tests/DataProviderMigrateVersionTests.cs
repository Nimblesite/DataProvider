using System.Reflection;

namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// Implements [MIG-CLI-COMMANDS]: <c>--version</c> prints the stamped release version
/// without build metadata. The Homebrew formula test runs it against the native binary.
/// </summary>
public sealed class DataProviderMigrateVersionTests
{
    private const string ReleaseVersionPattern = @"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$";

    [Theory]
    [InlineData("--version")]
    [InlineData("-V")]
    public void Main_VersionFlag_PrintsReleaseVersion(string flag)
    {
        var informational = typeof(DataProviderMigrate.Program)
            .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        var (exitCode, output) = MigrationCliConsole.Run(flag);
        var printed = output.Trim();

        Assert.Equal(expected: 0, actual: exitCode);
        Assert.Matches(ReleaseVersionPattern, printed);
        Assert.NotNull(informational);
        Assert.StartsWith(printed, informational, StringComparison.Ordinal);
    }
}
