using System.Text.Json;

namespace Ciderfy.Apple;

internal sealed class TokenCache
{
    private static readonly string _cacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Ciderfy"
    );

    private static string CachePath => Path.Combine(_cacheDir, "tokens.json");

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
    };

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
        && UserTokenExpiry.HasValue
        && UserTokenExpiry.Value > DateTimeOffset.UtcNow;

    public static TokenCache Load()
    {
        try
        {
            if (!File.Exists(CachePath))
                return new TokenCache();

            var json = File.ReadAllText(CachePath);
            return JsonSerializer.Deserialize<TokenCache>(json) ?? new TokenCache();
        }
        catch (IOException)
        {
            return new TokenCache();
        }
        catch (UnauthorizedAccessException)
        {
            return new TokenCache();
        }
        catch (JsonException)
        {
            return new TokenCache();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(_cacheDir);
            var json = JsonSerializer.Serialize(this, _serializerOptions);
            File.WriteAllText(CachePath, json);
        }
        catch (IOException)
        {
            // Best effort
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort
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
}
