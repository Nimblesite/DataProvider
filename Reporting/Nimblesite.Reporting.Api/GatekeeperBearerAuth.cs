using System.Buffers.Text;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Nimblesite.Reporting.Api;

/// <summary>
/// Validates Gatekeeper-issued HS256 bearer tokens locally, without a network call.
/// Implements [REPORT-AUTH-BEARER].
/// </summary>
public static class GatekeeperBearerAuth
{
    private const string Scheme = "Bearer ";

    /// <summary>
    /// Configuration key holding the base64 Gatekeeper signing key (same key Gatekeeper uses).
    /// </summary>
    public const string SigningKeySetting = "Jwt:SigningKey";

    /// <summary>
    /// Reads the signing key from configuration. A missing or malformed key yields an
    /// empty key, which rejects every request (fail closed).
    /// </summary>
    /// <param name="base64Key">The configured base64 signing key, if any.</param>
    /// <returns>The decoded key, or an empty array.</returns>
    public static ImmutableArray<byte> ParseSigningKey(string? base64Key)
    {
        var buffer = new byte[base64Key?.Length ?? 0];
        return
            base64Key is { Length: > 0 }
            && Convert.TryFromBase64String(base64Key, buffer, out var written)
            ? [.. buffer.AsSpan(0, written)]
            : [];
    }

    /// <summary>
    /// True when the Authorization header carries an unexpired token signed with the key.
    /// </summary>
    /// <param name="authorizationHeader">The raw Authorization header value.</param>
    /// <param name="signingKey">The Gatekeeper HMAC-SHA256 signing key.</param>
    /// <param name="now">The current time, used for the expiry check.</param>
    /// <returns>Whether the request is authenticated.</returns>
    public static bool IsAuthorized(
        string? authorizationHeader,
        ImmutableArray<byte> signingKey,
        DateTimeOffset now
    ) =>
        !signingKey.IsDefaultOrEmpty
        && authorizationHeader is { } header
        && header.StartsWith(Scheme, StringComparison.Ordinal)
        && IsValidToken(token: header[Scheme.Length..], signingKey: signingKey, now: now);

    private static bool IsValidToken(
        string token,
        ImmutableArray<byte> signingKey,
        DateTimeOffset now
    )
    {
        var parts = token.Split('.');
        return parts.Length == 3
            && CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(Sign(parts[0], parts[1], signingKey)),
                Encoding.ASCII.GetBytes(parts[2])
            )
            && ExpiresAfter(payload: parts[1], now: now);
    }

    private static string Sign(string header, string payload, ImmutableArray<byte> signingKey) =>
        Base64Url.EncodeToString(
            HMACSHA256.HashData(signingKey.AsSpan(), Encoding.UTF8.GetBytes($"{header}.{payload}"))
        );

    private static bool ExpiresAfter(string payload, DateTimeOffset now)
    {
        try
        {
            using var document = JsonDocument.Parse(Base64Url.DecodeFromChars(payload));
            return document.RootElement.TryGetProperty("exp", out var exp)
                && exp.TryGetInt64(out var seconds)
                && seconds > now.ToUnixTimeSeconds();
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return false;
        }
    }
}
