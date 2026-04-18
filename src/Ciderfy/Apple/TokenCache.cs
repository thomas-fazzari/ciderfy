using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ciderfy.Apple;

[JsonSerializable(typeof(TokenCache))]
internal sealed partial class TokenCacheJsonContext : JsonSerializerContext;

internal sealed class TokenCache
{
    private static readonly string _cacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Ciderfy"
    );

    private static string DefaultCachePath => Path.Combine(_cacheDir, "tokens.json");

    private string _cachePath = DefaultCachePath;

    [JsonConstructor]
    public TokenCache() { }

    internal TokenCache(string cachePath)
    {
        _cachePath = cachePath;
    }

    public string? DeveloperToken { get; set; }
    public DateTimeOffset? DeveloperTokenExpiry { get; set; }
    public string? UserToken { get; set; }
    public DateTimeOffset? UserTokenExpiry { get; set; }

    public bool HasValidDeveloperToken =>
        !string.IsNullOrEmpty(DeveloperToken)
        && DeveloperTokenExpiry.HasValue
        && DeveloperTokenExpiry.Value > DateTimeOffset.UtcNow;

    public bool HasValidUserToken =>
        !string.IsNullOrEmpty(UserToken)
        && (!UserTokenExpiry.HasValue || UserTokenExpiry.Value > DateTimeOffset.UtcNow);

    public static TokenCache Load() => LoadFromPath(DefaultCachePath);

    internal static TokenCache LoadFromPath(string cachePath)
    {
        try
        {
            if (!File.Exists(cachePath))
                return new TokenCache(cachePath);

            var json = File.ReadAllText(cachePath);
            var cache =
                JsonSerializer.Deserialize(json, TokenCacheJsonContext.Default.TokenCache)
                ?? new TokenCache(cachePath);
            cache._cachePath = cachePath;
            return cache;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new TokenCache(cachePath);
        }
    }

    public void Save()
    {
        var tempPath = $"{_cachePath}.tmp";
        try
        {
            var cacheDirectory = Path.GetDirectoryName(_cachePath);
            if (!string.IsNullOrEmpty(cacheDirectory))
                Directory.CreateDirectory(cacheDirectory);

            var json = JsonSerializer.Serialize(this, TokenCacheJsonContext.Default.TokenCache);
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _cachePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _ = TryDeleteTempFile(tempPath);
        }
    }

    public void Clear()
    {
        DeveloperToken = null;
        DeveloperTokenExpiry = null;
        UserToken = null;
        UserTokenExpiry = null;
        Save();
    }

    public void ClearDeveloperToken()
    {
        DeveloperToken = null;
        DeveloperTokenExpiry = null;
        Save();
    }

    private static bool TryDeleteTempFile(string tempPath)
    {
        try
        {
            if (!File.Exists(tempPath))
                return true;

            File.Delete(tempPath);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
