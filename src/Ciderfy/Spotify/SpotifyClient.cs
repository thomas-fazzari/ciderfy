using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Ciderfy.Web;
using OtpNet;

namespace Ciderfy.Spotify;

/// <summary>
/// Unauthenticated Spotify API client that uses the web player's GraphQL endpoint.
/// </summary>
/// <remarks>
/// Auth sequence:
/// <list type="number">
/// <item>Fetch session info</item>
/// <item>Obtain access token via TOTP</item>
/// <item>Get client token</item>
/// </list>
/// </remarks>
internal sealed partial class SpotifyClient(HttpClient httpClient, CookieContainer cookies)
    : IDisposable
{
    internal static readonly string WebBaseUrl = new UriBuilder(
        Uri.UriSchemeHttps,
        "open.spotify.com"
    ).Uri.GetLeftPart(UriPartial.Authority);

    internal static readonly string GraphQlEndpoint = new UriBuilder(
        Uri.UriSchemeHttps,
        "api-partner.spotify.com"
    )
    {
        Path = "pathfinder/v2/query",
    }
        .Uri
        .AbsoluteUri;

    internal static readonly string ClientTokenEndpoint = new UriBuilder(
        Uri.UriSchemeHttps,
        "clienttoken.spotify.com"
    )
    {
        Path = "v1/clienttoken",
    }
        .Uri
        .AbsoluteUri;

    internal const string PlaylistQueryHash =
        "bb67e0af06e8d6f52b531f97468ee4acd44cd0f82b988e15c2ea47b1148efc77";
    internal const string ClientTokenHeader = "Client-Token";
    internal const string AppVersionHeader = "Spotify-App-Version";
    internal const int TotpVersion = 61;

    private const string UserAgent = HttpClientDefaults.SpotifyUserAgent;

    private const int PlaylistPageSize = 1000;

    // csharpier-ignore-start
    private static readonly byte[] _totpSecret =
    [
        44, 55, 47, 42, 70, 40, 34, 114, 76, 74, 50, 111,
        120, 97, 75, 76, 94, 102, 43, 69, 49, 120, 118, 80, 64, 78,
    ];
    // csharpier-ignore-end

    private readonly string _clientTokenAuthority = new Uri(ClientTokenEndpoint).Host;
    private readonly SemaphoreSlim _authenticationLock = new(1, 1);

    private SpotifyAuthState? _authState;

    /// <summary>
    /// Fetches a playlist's metadata and tracks from Spotify using the unauthenticated GraphQL API
    /// </summary>
    public async Task<SpotifyPlaylist> GetPlaylistAsync(
        string playlistId,
        CancellationToken ct = default
    )
    {
        await EnsureAuthenticatedAsync(ct).ConfigureAwait(false);

        try
        {
            return await FetchPlaylistAsync(playlistId, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized)
        {
            // Auth state is stale, clear and retry once with fresh tokens
            InvalidateAuthState();
            await EnsureAuthenticatedAsync(ct).ConfigureAwait(false);
            return await FetchPlaylistAsync(playlistId, ct).ConfigureAwait(false);
        }
    }

    private async Task<SpotifyPlaylist> FetchPlaylistAsync(string playlistId, CancellationToken ct)
    {
        var tracks = new List<SpotifyTrack>();
        var name = "Spotify Import";
        var offset = 0;

        while (true)
        {
            var variables = new
            {
                uri = $"spotify:playlist:{playlistId}",
                offset,
                limit = PlaylistPageSize,
                enableWatchFeedEntrypoint = false,
            };

            using var response = await QueryGraphQlAsync(
                    PlaylistQueryHash,
                    "fetchPlaylist",
                    variables,
                    ct
                )
                .ConfigureAwait(false);

            var (pageName, pageTracks, totalCount) = ParsePlaylistPage(response);

            if (offset == 0)
            {
                name = pageName;
            }

            tracks.AddRange(pageTracks);
            offset += PlaylistPageSize;

            if (offset >= totalCount)
            {
                break;
            }
        }

        return new SpotifyPlaylist(name, tracks);
    }

    private void InvalidateAuthState()
    {
        _authenticationLock.Wait(CancellationToken.None);
        try
        {
            _authState = null;
        }
        finally
        {
            _authenticationLock.Release();
        }
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken ct)
    {
        if (Volatile.Read(ref _authState) is not null)
        {
            return;
        }

        await _authenticationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _authState) is not null)
            {
                return;
            }

            var authState = await InitializeAsync(ct).ConfigureAwait(false);
            Volatile.Write(ref _authState, authState);
        }
        finally
        {
            _authenticationLock.Release();
        }
    }

    /// <summary>
    /// Authenticates the client against the Spotify web player.
    /// </summary>
    /// <remarks>
    /// Three-step sequence:
    /// <list type="number">
    /// <item>Extract client version from the web page</item>
    /// <item>Obtain an access token using a TOTP code</item>
    /// <item>Request a client token</item>
    /// </list>
    /// </remarks>
    private async Task<SpotifyAuthState> InitializeAsync(CancellationToken ct)
    {
        var session = await GetSessionInfoAsync(ct).ConfigureAwait(false);
        var access = await GetAccessTokenAsync(ct).ConfigureAwait(false);
        var clientToken = await GetClientTokenAsync(
                session.ClientVersion,
                access.ClientId,
                access.DeviceId ?? session.DeviceId,
                ct
            )
            .ConfigureAwait(false);

        return new SpotifyAuthState(access.AccessToken, clientToken, session.ClientVersion);
    }

    private async Task<SessionInfoState> GetSessionInfoAsync(CancellationToken ct)
    {
        using var response = await httpClient
            .GetAsync(new Uri(WebBaseUrl), ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        string? clientVersion = null;
        var configMatch = AppServerConfigRegex().Match(html);

        if (!configMatch.Success)
        {
            return new SessionInfoState(clientVersion, ExtractDeviceIdFromCookies());
        }

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(configMatch.Groups[1].Value));
        using var doc = JsonDocument.Parse(json);
        clientVersion = doc.RootElement.Deserialize<SessionInfoResponse>()?.ClientVersion;

        return new SessionInfoState(clientVersion, ExtractDeviceIdFromCookies());
    }

    private async Task<AccessTokenState> GetAccessTokenAsync(CancellationToken ct)
    {
        var totpCode = GenerateTotpCode();
        var url =
            $"{WebBaseUrl}/api/token?reason=init&productType=web-player&totp={totpCode}&totpVer={TotpVersion}&totpServer={totpCode}";

        using var response = await httpClient.GetAsync(new Uri(url), ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);

        var tokenResponse = doc.RootElement.Deserialize<AccessTokenResponse>();
        if (string.IsNullOrWhiteSpace(tokenResponse?.AccessToken))
        {
            throw new InvalidOperationException("Spotify access token missing from response.");
        }

        return new AccessTokenState(
            tokenResponse.AccessToken,
            tokenResponse.ClientId,
            ExtractDeviceIdFromCookies()
        );
    }

    private async Task<string> GetClientTokenAsync(
        string? clientVersion,
        string? clientId,
        string? deviceId,
        CancellationToken ct
    )
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                client_data = new
                {
                    client_version = clientVersion,
                    client_id = clientId,
                    js_sdk_data = new
                    {
                        device_brand = "unknown",
                        device_model = "unknown",
                        os = "windows",
                        os_version = "NT 10.0",
                        device_id = deviceId,
                        device_type = "computer",
                    },
                },
            }
        );

        using var request = new HttpRequestMessage(HttpMethod.Post, ClientTokenEndpoint);
        request.Content = new StringContent(payload, Encoding.UTF8);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(
            MediaTypeNames.Application.Json
        );
        request.Headers.Add(HttpHeaderNames.Authority, _clientTokenAuthority);
        request.Headers.Add(HttpHeaderNames.Accept, MediaTypeNames.Application.Json);
        request.Headers.Add(HttpHeaderNames.UserAgent, UserAgent);

        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);

        var clientToken = ExtractClientToken(doc.RootElement);

        return string.IsNullOrWhiteSpace(clientToken)
            ? throw new InvalidOperationException("Spotify client token missing from response.")
            : clientToken;
    }

    private static string? ExtractClientToken(JsonElement root)
    {
        try
        {
            return root.Deserialize<ClientTokenResponse>()?.GrantedToken?.Token;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Sends a persisted GraphQL query identified by its SHA-256 hash (no dynamic query strings)
    /// </summary>
    private async Task<JsonDocument> QueryGraphQlAsync(
        string hash,
        string operationName,
        object variables,
        CancellationToken ct
    )
    {
        var authState =
            _authState ?? throw new InvalidOperationException("Spotify auth not initialized.");

        var serialized = JsonSerializer.Serialize(
            new
            {
                variables,
                operationName,
                extensions = new { persistedQuery = new { version = 1, sha256Hash = hash } },
            }
        );

        using var request = new HttpRequestMessage(HttpMethod.Post, GraphQlEndpoint);
        request.Content = new StringContent(
            serialized,
            Encoding.UTF8,
            MediaTypeNames.Application.Json
        );
        request.Headers.Add(HttpHeaderNames.Authorization, $"Bearer {authState.AccessToken}");
        request.Headers.Add(ClientTokenHeader, authState.ClientToken);
        if (!string.IsNullOrWhiteSpace(authState.ClientVersion))
        {
            request.Headers.Add(AppVersionHeader, authState.ClientVersion);
        }

        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return JsonDocument.Parse(json);
    }

    private string? ExtractDeviceIdFromCookies()
    {
        var cookieCollection = cookies.GetCookies(new Uri(WebBaseUrl));
        return cookieCollection["sp_t"]?.Value;
    }

    /// <summary>
    /// Generates a TOTP code from an XOR-obfuscated secret. The secret bytes are
    /// deobfuscated, converted to a Base32 key, and fed into a standard TOTP generator
    /// </summary>
    internal static string GenerateTotpCode()
    {
        Span<byte> transformed = stackalloc byte[_totpSecret.Length];
        for (var i = 0; i < _totpSecret.Length; i++)
        {
            transformed[i] = (byte)(_totpSecret[i] ^ ((i % 33) + 9));
        }

        var sb = new StringBuilder(transformed.Length * 3);
        foreach (var b in transformed)
        {
            sb.Append(b);
        }

        var key = Encoding.UTF8.GetBytes(sb.ToString());
        return new Totp(key, step: 30).ComputeTotp();
    }

    internal static (string Name, List<SpotifyTrack> Tracks, int TotalCount) ParsePlaylistPage(
        JsonDocument doc
    )
    {
        var tracks = new List<SpotifyTrack>();

        if (
            !doc.RootElement.TryGetProperty("data", out var data)
            || !data.TryGetProperty("playlistV2", out var playlist)
        )
        {
            return ("Spotify Import", tracks, 0);
        }

        var name = playlist.TryGetProperty("name", out var n)
            ? n.GetString() ?? "Spotify Import"
            : "Spotify Import";

        if (
            !playlist.TryGetProperty("content", out var content)
            || !content.TryGetProperty("items", out var items)
        )
        {
            return (name, tracks, 0);
        }

        var totalCount = content.TryGetProperty("totalCount", out var tc)
            ? tc.GetInt32()
            : items.GetArrayLength();

        tracks.AddRange(items.EnumerateArray().Select(ParsePlaylistItem).OfType<SpotifyTrack>());

        return (name, tracks, totalCount);
    }

    internal static SpotifyTrack? ParsePlaylistItem(JsonElement item)
    {
        if (
            !item.TryGetProperty("itemV2", out var itemV2)
            || !itemV2.TryGetProperty("data", out var itemData)
        )
        {
            return null;
        }

        var title = itemData.TryGetProperty("name", out var tn) ? tn.GetString() : null;
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        return new SpotifyTrack
        {
            SpotifyId = ExtractIdFromUri(itemData),
            Title = title,
            Artist = ExtractFirstArtist(itemData),
            Artists = ExtractArtists(itemData),
            AlbumTitle = ExtractAlbumTitle(itemData),
            DurationMs = ExtractDuration(itemData),
        };
    }

    internal static string ExtractFirstArtist(JsonElement element)
    {
        var artists = ExtractArtists(element);
        return artists.Count > 0 ? artists[0] : string.Empty;
    }

    internal static IReadOnlyList<string> ExtractArtists(JsonElement element)
    {
        if (
            TryGetArtistNames(element, "artists", out var names)
            || TryGetArtistNames(element, "firstArtist", out names)
        )
        {
            return names;
        }

        return [];
    }

    internal static bool TryGetArtistNames(
        JsonElement element,
        string propertyName,
        out IReadOnlyList<string> names
    )
    {
        names = [];
        if (
            !element.TryGetProperty(propertyName, out var artists)
            || !artists.TryGetProperty("items", out var items)
            || items.GetArrayLength() == 0
        )
        {
            return false;
        }

        var parsedNames = new List<string>();
        foreach (var item in items.EnumerateArray())
        {
            if (
                !item.TryGetProperty("profile", out var profile)
                || !profile.TryGetProperty("name", out var n)
            )
            {
                continue;
            }

            var name = n.GetString();
            if (!string.IsNullOrWhiteSpace(name))
            {
                parsedNames.Add(name);
            }
        }

        names = parsedNames;
        return parsedNames.Count > 0;
    }

    internal static string? ExtractAlbumTitle(JsonElement element)
    {
        if (
            element.TryGetProperty("albumOfTrack", out var album)
            && album.TryGetProperty("name", out var name)
        )
        {
            return name.GetString();
        }

        return null;
    }

    internal static string ExtractIdFromUri(JsonElement element) =>
        element.TryGetProperty("uri", out var uri)
            ? uri.GetString()?.Split(':').LastOrDefault() ?? string.Empty
            : string.Empty;

    internal static int ExtractDuration(JsonElement element)
    {
        if (
            !element.TryGetProperty("trackDuration", out var duration)
            || !duration.TryGetProperty("totalMilliseconds", out var milliseconds)
        )
        {
            return 0;
        }

        return milliseconds.ValueKind switch
        {
            JsonValueKind.Number when milliseconds.TryGetInt32(out var numericMs) => numericMs,
            JsonValueKind.String
                when int.TryParse(
                    milliseconds.GetString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var stringMs
                ) => stringMs,
            _ => 0,
        };
    }

    private sealed record SessionInfoState(string? ClientVersion, string? DeviceId);

    private sealed record AccessTokenState(string AccessToken, string? ClientId, string? DeviceId);

    private sealed record SpotifyAuthState(
        string AccessToken,
        string ClientToken,
        string? ClientVersion
    );

    public void Dispose()
    {
        _authenticationLock.Dispose();
        httpClient.Dispose();
    }

    [GeneratedRegex(
        @"<script id=""appServerConfig"" type=""text/plain"">([^<]+)</script>",
        RegexOptions.None,
        1000
    )]
    private static partial Regex AppServerConfigRegex();
}

file sealed record SessionInfoResponse(
    [property: JsonPropertyName("clientVersion")] string? ClientVersion
);

file sealed record AccessTokenResponse(
    [property: JsonPropertyName("accessToken")] string? AccessToken,
    [property: JsonPropertyName("clientId")] string? ClientId
);

file sealed record ClientTokenGrantedToken([property: JsonPropertyName("token")] string? Token);

file sealed record ClientTokenResponse(
    [property: JsonPropertyName("granted_token")] ClientTokenGrantedToken? GrantedToken
);
