using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;

namespace Redhead.SitesCatalog.Api.Services;

public sealed class GoogleDriveOAuthStateService : IGoogleDriveOAuthStateService
{
    private const string Purpose = "Redhead.SitesCatalog.GoogleDrive.OAuthState.v1";
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDataProtector _protector;
    private readonly IMemoryCache _cache;

    public GoogleDriveOAuthStateService(
        IDataProtectionProvider dataProtectionProvider,
        IMemoryCache cache)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
        _cache = cache;
    }

    public string CreateState(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        var payload = new OAuthStatePayload(
            userId,
            Base64UrlEncode(RandomNumberGenerator.GetBytes(32)),
            DateTime.UtcNow);

        _cache.Set(GetCacheKey(payload.UserId, payload.Nonce), true, StateLifetime);

        return _protector.Protect(JsonSerializer.Serialize(payload, JsonOptions));
    }

    public bool ValidateAndConsumeState(string userId, string? state)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(state))
        {
            return false;
        }

        OAuthStatePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<OAuthStatePayload>(
                _protector.Unprotect(state),
                JsonOptions);
        }
        catch
        {
            return false;
        }

        if (payload == null ||
            !string.Equals(payload.UserId, userId, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(payload.Nonce) ||
            payload.IssuedAtUtc > DateTime.UtcNow ||
            DateTime.UtcNow - payload.IssuedAtUtc > StateLifetime)
        {
            return false;
        }

        var cacheKey = GetCacheKey(payload.UserId, payload.Nonce);
        if (!_cache.TryGetValue(cacheKey, out _))
        {
            return false;
        }

        _cache.Remove(cacheKey);
        return true;
    }

    private static string GetCacheKey(string userId, string nonce)
        => $"GoogleDriveOAuthState:{userId}:{nonce}";

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record OAuthStatePayload(
        string UserId,
        string Nonce,
        DateTime IssuedAtUtc);
}
