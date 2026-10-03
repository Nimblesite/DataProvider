using System.Security.Cryptography;
using System.Text;

namespace Nimblesite.Reporting.Integration.Tests;

/// <summary>
/// Mints Gatekeeper-format HS256 bearer tokens for the E2E hosts ([REPORT-AUTH-BEARER]).
/// Deliberately independent of the API's validator so the tests do not validate the
/// validator with itself.
/// </summary>
public static class GatekeeperTestTokens
{
    /// <summary>
    /// Base64 signing key configured on every test API host.
    /// </summary>
    public static readonly string SigningKey = Convert.ToBase64String(
        Encoding.UTF8.GetBytes("reporting-e2e-gatekeeper-signing-key-32b")
    );

    /// <summary>
    /// Creates a token signed with <see cref="SigningKey"/> that expires in one hour.
    /// </summary>
    public static string Create()
    {
        var expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        return Create(
            $$"""{"sub":"reporting-e2e","jti":"{{Guid.NewGuid():N}}","roles":[],"exp":{{expires}}}"""
        );
    }

    /// <summary>Signs explicit JSON for authentication boundary tests.</summary>
    /// <param name="claims">The JWT claims, including deliberately malformed test values.</param>
    /// <param name="headerJson">The JOSE header to sign.</param>
    /// <returns>A token independently signed with the test host key.</returns>
    public static string Create(
        string claims,
        string headerJson = """{"alg":"HS256","typ":"JWT"}"""
    )
    {
        var header = Encode(headerJson);
        var payload = Encode(claims);
        using var hmac = new HMACSHA256(Convert.FromBase64String(SigningKey));
        var signature = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{header}.{payload}"));
        return $"{header}.{payload}.{Base64Url(signature)}";
    }

    private static string Encode(string json) => Base64Url(Encoding.UTF8.GetBytes(json));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
