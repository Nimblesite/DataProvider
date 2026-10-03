using Nimblesite.Reporting.Api;
using Nimblesite.Reporting.Engine;

namespace Nimblesite.Reporting.Integration.Tests;

public sealed record ReportingSecurityContractTests
{
    // Implements [REPORT-AUTH-BEARER]: malformed signed claims must fail closed.
    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
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
