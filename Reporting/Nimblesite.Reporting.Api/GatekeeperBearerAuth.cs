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
            && HasValidClaims(header: parts[0], payload: parts[1], now: now);
    }

    private static string Sign(string header, string payload, ImmutableArray<byte> signingKey) =>
        Base64Url.EncodeToString(
            HMACSHA256.HashData(signingKey.AsSpan(), Encoding.UTF8.GetBytes($"{header}.{payload}"))
        );

    private static bool HasValidClaims(string header, string payload, DateTimeOffset now)
    {
        try
        {
            using var jose = JsonDocument.Parse(Base64Url.DecodeFromChars(header));
            using var claims = JsonDocument.Parse(Base64Url.DecodeFromChars(payload));
            return HasHs256Algorithm(jose.RootElement)
                && HasValidLifetime(claims.RootElement, now.ToUnixTimeSeconds());
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return false;
        }
    }

    private static bool HasHs256Algorithm(JsonElement header) =>
        header.ValueKind == JsonValueKind.Object
        && header.TryGetProperty("alg", out var algorithm)
        && algorithm.ValueKind == JsonValueKind.String
        && algorithm.GetString() == "HS256";

    private static bool HasValidLifetime(JsonElement claims, long now) =>
        claims.ValueKind == JsonValueKind.Object
        && claims.TryGetProperty("exp", out var expiry)
        && expiry.ValueKind == JsonValueKind.Number
        && expiry.TryGetInt64(out var expiresAt)
        && expiresAt > now
        && (
            !claims.TryGetProperty("nbf", out var notBefore)
            || notBefore.ValueKind == JsonValueKind.Number
                && notBefore.TryGetInt64(out var startsAt)
                && startsAt <= now
        );
}
