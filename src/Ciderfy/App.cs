using Ciderfy.Apple;
using Ciderfy.Matching;
using Ciderfy.Spotify;
using Spectre.Console;
using static Ciderfy.TuiHelper;

namespace Ciderfy;

internal sealed class App : IDisposable
{
    private readonly TokenCache _tokenCache;
    private readonly AppleMusicAuth _auth;
    private readonly PlaylistTransferService _transferService;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConsoleCancelEventHandler _cancelKeyPressHandler;

    private string _storefront = "us";
    private string? _nextPlaylistName;

    public App(TokenCache tokenCache, AppleMusicAuth auth, PlaylistTransferService transferService)
    {
        _tokenCache = tokenCache;
        _auth = auth;
        _transferService = transferService;
        _cancelKeyPressHandler = (_, e) =>
        {
            e.Cancel = true;
            _cts.Cancel();
        };
    }

    public async Task<int> RunAsync()
    {
        Console.CancelKeyPress += _cancelKeyPressHandler;
        try
        {
            PrintBanner();

            AnsiConsole.MarkupLine(
                $"[dim]Paste a Spotify playlist URL to transfer, or type [{Accent}]/help[/][/]"
            );
            PrintAuthStatus(
                _tokenCache.HasValidDeveloperToken,
                _tokenCache.HasValidUserToken,
                _storefront
            );
            AnsiConsole.WriteLine();

            while (!_cts.IsCancellationRequested)
            {
                string input;
                try
                {
                    input = await AnsiConsole.PromptAsync(
                        new TextPrompt<string>($"[{Accent}]>[/]").AllowEmpty(),
                        _cts.Token
                    );
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                try
                {
                    if (!await HandleInputAsync(input.Trim(), _cts.Token))
                        return 0;
                }
                catch (OperationCanceledException)
                {
                    AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
                }
                catch (AppleMusicRateLimitException ex)
                {
                    var details = ex.RetryAfterSeconds is { } retryAfter
                        ? $" Retry after about [bold]{retryAfter}[/]s."
                        : string.Empty;
                    AnsiConsole.MarkupLine(
                        $"[red]Apple Music rate limited (429). Transfer stopped.[/]{details}"
                    );
                }
                catch (AppleMusicUnauthorizedException)
                {
                    _tokenCache.ClearDeveloperToken();
                    AnsiConsole.MarkupLine(
                        $"[red]Developer token expired.[/] Run [bold {Accent}]/auth[/] to refresh it."
                    );
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
                }

                AnsiConsole.WriteLine();
            }

            return 0;
        }
        finally
        {
            Console.CancelKeyPress -= _cancelKeyPressHandler;
        }
    }

    private async Task<bool> HandleInputAsync(string input, CancellationToken ct)
    {
        if (input.StartsWith('/'))
            return await HandleCommandAsync(input, ct);

        if (SpotifyUrlInfo.TryParse(input, out var urlInfo) && urlInfo is not null)
        {
            if (urlInfo.Type != SpotifyUrlType.Playlist)
                AnsiConsole.MarkupLine(
                    "[red]Only playlist URLs are supported.[/] Paste a Spotify playlist URL."
                );
            else
            {
                await HandleTransferAsync(urlInfo.Id, ct);
                _nextPlaylistName = null;
            }

            return true;
        }

        AnsiConsole.MarkupLine($"[red]Not a valid command or Spotify URL.[/] Type [grey]/help[/]");
        return true;
    }

    /// <returns>false to exit the REPL</returns>
    private async Task<bool> HandleCommandAsync(string input, CancellationToken ct)
    {
        var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLowerInvariant();
        var arg = parts.Length > 1 ? parts[1].Trim() : null;

        switch (cmd)
        {
            case "/quit" or "/exit" or "/q":
                return false;

            case "/help"
            or "/h":
                PrintHelp();
                break;

            case "/auth":
                await HandleAuthAsync(arg, ct);
                PrintAuthStatus(
                    _tokenCache.HasValidDeveloperToken,
                    _tokenCache.HasValidUserToken,
                    _storefront
                );
                break;

            case "/status":
                PrintAuthStatus(
                    _tokenCache.HasValidDeveloperToken,
                    _tokenCache.HasValidUserToken,
                    _storefront
                );
                break;

            case "/storefront"
            or "/sf":
                if (string.IsNullOrEmpty(arg))
                    AnsiConsole.MarkupLine(
                        $"Storefront: [bold]{_storefront}[/]. Usage: [grey]/storefront fr[/]"
                    );
                else
                {
                    _storefront = arg.ToLowerInvariant();
                    AnsiConsole.MarkupLine($"Storefront set to [bold]{_storefront}[/]");
                }
                break;

            case "/name":
                if (string.IsNullOrEmpty(arg))
                {
                    _nextPlaylistName = null;
                    AnsiConsole.MarkupLine("Playlist name cleared (will use default)");
                }
                else
                {
                    _nextPlaylistName = arg;
                    AnsiConsole.MarkupLine(
                        $"Next playlist will be named [bold]{_nextPlaylistName}[/]"
                    );
                }
                break;

            default:
                AnsiConsole.MarkupLine($"[red]Unknown command:[/] {cmd}. Type [grey]/help[/]");
                break;
        }

        return true;
    }

    private async Task HandleAuthAsync(string? arg, CancellationToken ct)
    {
        if ("reset".Equals(arg, StringComparison.OrdinalIgnoreCase))
        {
            _tokenCache.Clear();
            AnsiConsole.MarkupLine("[yellow]Tokens cleared.[/]");
        }

        await WithStatusAsync(
            "Extracting developer token from Apple Music...",
            () => _auth.GetDeveloperTokenAsync(ct)
        );
        AnsiConsole.MarkupLine($"[{Accent}]Developer token OK[/]");

        if (_tokenCache.HasValidUserToken)
        {
            AnsiConsole.MarkupLine($"[{Accent}]User token OK[/] [dim](cached)[/]");
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Panel(
                $"[bold]1.[/] Open [link=https://music.apple.com]https://music.apple.com[/] in your browser and sign in\n"
                    + $"[bold]2.[/] Open DevTools with [bold]F12[/] (or [bold]Cmd+Option+I[/]) -> Console\n"
                    + $"[bold]3.[/] Run this command:\n"
                    + $"   [bold {Accent}]MusicKit.getInstance().musicUserToken[/]\n"
                    + $"[bold]4.[/] Copy the returned string"
            )
            {
                Header = new PanelHeader(" Get your Music User Token "),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(AccentColor),
                Padding = new Padding(2, 1),
            }
        );
        AnsiConsole.WriteLine();

        var token = await AnsiConsole.PromptAsync(
            new TextPrompt<string>("Paste your token here:").PromptStyle(new Style(AccentColor)),
            ct
        );

        token = token.Trim().Trim('"', '\'');

        if (string.IsNullOrWhiteSpace(token))
        {
            AnsiConsole.MarkupLine("[red]No token provided.[/]");
            return;
        }

        _tokenCache.UserToken = token;
        _tokenCache.UserTokenExpiry = DateTimeOffset.UtcNow.AddMonths(3);
        _tokenCache.Save();

        AnsiConsole.MarkupLine($"[{Accent}]Authentication complete![/] Tokens cached.");
    }

    private async Task HandleTransferAsync(string playlistId, CancellationToken ct)
    {
        if (!await EnsureAuthenticatedAsync(ct))
            return;

        var playlist = await WithStatusAsync(
            "Fetching playlist from Spotify...",
            () => _transferService.FetchSpotifyPlaylistAsync(playlistId, ct)
        );

        if (playlist.Tracks.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No tracks found.[/]");
            return;
        }

        var name = _nextPlaylistName ?? playlist.Name;
        AnsiConsole.MarkupLine(
            $"Playlist [{Accent} bold]{Markup.Escape(playlist.Name)}[/] — [bold]{playlist.Tracks.Count}[/] track(s)"
        );

        // ISRC resolution and batch Apple Music matching
        List<MatchResult.Matched> isrcMatched = [];
        List<TrackMetadata> unmatched = [];
        await WithProgressAsync(
            "Resolving ISRCs via Deezer",
            async task =>
            {
                task.MaxValue = playlist.Tracks.Count;
                var progress = new Progress<(int Current, int Total)>(p => task.Value = p.Current);

                (isrcMatched, unmatched) = await _transferService.MatchByIsrcAsync(
                    playlist.Tracks,
                    _storefront,
                    progress,
                    ct
                );
                task.Value = playlist.Tracks.Count;
            }
        );

        AnsiConsole.MarkupLine(
            $"  [{Accent}]{isrcMatched.Count}[/]/{playlist.Tracks.Count} matched via ISRC"
                + (unmatched.Count > 0 ? $"  [dim]{unmatched.Count} remaining[/]" : "")
        );

        // Then optional text matching for tracks not found
        List<MatchResult> textResults = [];
        if (
            unmatched.Count > 0
            && await AnsiConsole.ConfirmAsync(
                $"Try text matching for [bold]{unmatched.Count}[/] remaining track(s)? [dim](can be slow)[/]",
                defaultValue: false,
                cancellationToken: ct
            )
        )
        {
            await WithProgressAsync(
                "Text matching",
                async task =>
                {
                    task.MaxValue = unmatched.Count;
                    var progress = new Progress<TrackMatchProgress>(p =>
                    {
                        task.Description =
                            $"Matching: {Markup.Escape(Truncate(p.Track.Artist + " - " + p.Track.Title, 50))}";
                        task.Value = p.CurrentIndex;
                    });

                    textResults = await _transferService.MatchByTextAsync(
                        unmatched,
                        _storefront,
                        progress,
                        ct
                    );
                    task.Value = unmatched.Count;
                }
            );
        }

        // Combine results
        var allResults = new List<MatchResult>();
        var isrcMap = isrcMatched
            .DistinctBy(m => m.SpotifyTrack.SpotifyId)
            .ToDictionary(m => m.SpotifyTrack.SpotifyId);
        var textMap = textResults
            .DistinctBy(m => m.SpotifyTrack.SpotifyId)
            .ToDictionary(m => m.SpotifyTrack.SpotifyId);

        foreach (var track in playlist.Tracks)
        {
            if (isrcMap.TryGetValue(track.SpotifyId, out var isrcMatch))
                allResults.Add(isrcMatch);
            else if (textMap.TryGetValue(track.SpotifyId, out var textMatch))
                allResults.Add(textMatch);
            else
                allResults.Add(
                    new MatchResult.NotFound(track, "Skipped (text matching not requested)")
                );
        }

        PrintMatchResults(allResults);

        var matched = allResults.OfType<MatchResult.Matched>().Count();
        var notFound = allResults.OfType<MatchResult.NotFound>().Count();

        if (matched == 0)
        {
            AnsiConsole.MarkupLine("[red]No tracks matched. Nothing to transfer.[/]");
            return;
        }

        var createResult = await WithStatusAsync(
            $"Creating playlist \"{Markup.Escape(name)}\"...",
            () => _transferService.CreatePlaylistAsync(name, allResults, ct)
        );

        PrintTransferResult(
            createResult.PlaylistId,
            createResult.Success,
            name,
            matched,
            playlist.Tracks.Count,
            notFound
        );
    }

    private async Task<bool> EnsureAuthenticatedAsync(CancellationToken ct)
    {
        if (!_tokenCache.HasValidDeveloperToken)
            await WithStatusAsync(
                "Extracting developer token...",
                () => _auth.GetDeveloperTokenAsync(ct)
            );

        if (!_tokenCache.HasValidUserToken)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]User token missing.[/] Run [bold {Accent}]/auth[/] first."
            );
            return false;
        }

        return true;
    }

    public void Dispose()
    {
        _cts.Dispose();
    }
}
