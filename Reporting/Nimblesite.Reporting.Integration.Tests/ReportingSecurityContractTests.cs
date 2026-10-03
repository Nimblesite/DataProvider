using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nimblesite.Reporting.Api;
using Nimblesite.Reporting.Engine;

namespace Nimblesite.Reporting.Integration.Tests;

public sealed record ReportingSecurityContractTests
{
    // Implements [REPORT-AUTH-BEARER]: rejected paths may contain private data.
    [Fact]
    public async Task RejectedRequest_DoesNotLogUserControlledPath()
    {
        using var output = new StringWriter(formatProvider: CultureInfo.InvariantCulture);
        var original = Console.Out;
        Console.SetOut(newOut: output);
        try
        {
            await AssertRejectedRequestAsync().ConfigureAwait(true);
        }
        finally
        {
            Console.SetOut(newOut: original);
        }
        Assert.DoesNotContain("private-report-marker", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Rejected unauthenticated", output.ToString(), StringComparison.Ordinal);
    }

    private static async Task AssertRejectedRequestAsync()
    {
        using var services = new ServiceCollection()
            .AddLogging(builder => builder.AddSimpleConsole())
            .BuildServiceProvider();
        var app = new ApplicationBuilder(serviceProvider: services);
        app.UseReportingBearerAuth(
            configuration: new ConfigurationBuilder().Build(),
            logger: services.GetRequiredService<ILogger<ReportingSecurityContractTests>>()
        );
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/reports/private-report-marker";
        await app.Build()(context).ConfigureAwait(true);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    // Implements [REPORT-AUTH-BEARER]: malformed signed claims must fail closed.
    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{]")]
    [InlineData("{\"exp\":{}}")]
    [InlineData("{\"exp\":\"4102444800\"}")]
    [InlineData("{\"exp\":4102444800,\"nbf\":4102444800}")]
    [InlineData("{\"exp\":4102444800,\"nbf\":{}}")]
    public void SignedMalformedOrFutureClaims_AreRejected(string claims) =>
        Assert.False(
            GatekeeperBearerAuth.IsAuthorized(
                authorizationHeader: $"Bearer {GatekeeperTestTokens.Create(claims: claims)}",
                signingKey: GatekeeperBearerAuth.ParseSigningKey(GatekeeperTestTokens.SigningKey),
                now: DateTimeOffset.FromUnixTimeSeconds(2_000_000_000)
            )
        );

    // Implements [REPORT-AUTH-BEARER]: only the configured HS256 algorithm is accepted.
    [Theory]
    [InlineData("{\"alg\":\"none\"}", false)]
    [InlineData("[]", false)]
    [InlineData("{\"alg\":\"HS256\"}", true)]
    public void SignedHeaders_RequireHs256(string header, bool expected) =>
        Assert.Equal(
            expected,
            GatekeeperBearerAuth.IsAuthorized(
                authorizationHeader: $"Bearer {GatekeeperTestTokens.Create(claims: """{"exp":4102444800}""", headerJson: header)}",
                signingKey: GatekeeperBearerAuth.ParseSigningKey(GatekeeperTestTokens.SigningKey),
                now: DateTimeOffset.FromUnixTimeSeconds(2_000_000_000)
            )
        );

    // Implements [REPORT-CONNECTIONS]: valid Npgsql aliases must choose PostgreSQL.
    [Theory]
    [InlineData("Server=localhost;Username=test;Database=test")]
    [InlineData("Server=localhost;Port=5432;User ID=test;Database=test")]
    public void PostgresServerAliases_AreDetectedAsPostgres(string connectionString)
    {
        Assert.Equal("localhost", new NpgsqlConnectionStringBuilder(connectionString).Host);
        Assert.Equal(
            DatabaseProvider.Postgres,
            ReportingConnections.DetectProvider(connectionString)
        );
    }
}
