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
        var header = Encode("""{"alg":"HS256","typ":"JWT"}""");
        var expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var payload = Encode(
            $$"""{"sub":"reporting-e2e","jti":"{{Guid.NewGuid():N}}","roles":[],"exp":{{expires}}}"""
        );
        using var hmac = new HMACSHA256(Convert.FromBase64String(SigningKey));
        var signature = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{header}.{payload}"));
        return $"{header}.{payload}.{Base64Url(signature)}";
    }

    private static string Encode(string json) => Base64Url(Encoding.UTF8.GetBytes(json));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
