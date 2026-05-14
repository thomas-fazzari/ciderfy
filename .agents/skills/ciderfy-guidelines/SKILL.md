---
name: ciderfy-guidelines
description: Ciderfy .NET TUI guidelines. Use when changing src/Ciderfy, tests/Ciderfy.Tests, transfer matching, Apple/Spotify/Deezer provider behavior, typed HTTP clients, options/configuration, Spectre.Console TUI commands/rendering, or xUnit tests.
---

# Ciderfy Guidelines

Use this for `src/Ciderfy` and `tests/Ciderfy.Tests`.

## Decision Map

| Change                                    | Owner                                  | Required pattern                                                                                          |
| ----------------------------------------- | -------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| Host setup, app composition               | `Program.cs`, `DependencyInjection.cs` | Keep `Program.cs` thin; register via `AddCiderfy(...)`                                                    |
| Apple Music auth/client/token persistence | `Apple`                                | Automatic developer-token extraction; user token through `/auth`; typed exceptions for 401/429            |
| Spotify playlist access                   | `Spotify`                              | Web-player behavior; persisted GraphQL hash; encapsulated TOTP; cookie container; unauthorized retry once |
| Transfer orchestration, merge, match      | `Matching`                             | ISRC first; text fallback second; preserve first-seen order                                               |
| Commands, state, rendering                | `Tui`                                  | UI loop owns state transitions; background work posts `TuiMessage`                                        |
| HTTP constants/client helpers             | `Web`                                  | Shared headers, MIME constants, typed-client helpers only                                                 |
| Config paths/options                      | `Configuration`                        | Copy default INI once; validate meaningful options on start                                               |
| Tests/fakes                               | `tests/Ciderfy.Tests`                  | xUnit v3; no live network; use fakes/temp paths                                                           |

## Architecture Boundaries

- Keep one assembly unless concrete need says otherwise.
- Do not add generic service layers, repositories, mediators, CQRS, or new projects without explicit need.
- TUI coordinates user flow only. Business workflow stays in `Matching`.
- Provider HTTP/parsing details stay in `Apple`, `Spotify`, or Deezer resolver code.
- `Web` must not grow provider-specific behavior.
- Feature registration belongs in feature-local `DependencyInjection.cs` files and root `AddCiderfy(...)`.

## Implementation Rules

- Use typed-client DI for production HTTP clients; never manually instantiate production `HttpClient`.
- Use `AddStandardResilienceHandler` unless endpoint behavior requires narrower handling.
- Apple and Deezer rate limits use `SlidingWindowRateLimiter`; no arbitrary sleeps.
- Always pass and honor `CancellationToken`; preserve cancellation before broad catches.
- Options classes need `SectionName`, data annotations, `[OptionsValidator]`, DI `IValidateOptions<T>`, and `.ValidateOnStart()`.
- Keep option defaults synchronized with `ciderfy.ini`.
- User config is copied from `ciderfy.ini` on first launch only; never overwrite normal user config.
- Use `System.Text.Json`; keep private API DTOs as `file sealed record` near parser/client.
- Use `[GeneratedRegex(..., 1000)]` for regexes.

## Transfer Invariants

Flow:

```text
Spotify URL -> Spotify playlist fetch -> merge/dedupe -> Deezer ISRC -> Apple ISRC lookup -> optional text fallback -> Apple playlist create
```

Rules:

- Preserve first-seen track order.
- Dedupe merged playlists by `SpotifyId`.
- Keep ISRC matching first. Text matching is fallback because false positives are possible.
- Shared scoring constants belong in `MatchingConstants`.
- Matching changes need focused tests for normalization, thresholds, and duration penalties.
- Provider lookup tests must use `FakeHttpMessageHandler`; unit tests must not hit live networks.

## Provider Rules

- Ciderfy must not require Spotify, Apple, or Deezer developer accounts.
- Spotify uses web-player behavior: persisted GraphQL query hash, encapsulated TOTP, cookie container, unauthorized retry once.
- Apple developer token extraction stays automatic from Apple Music web assets.
- User token enters via `/auth`; TUI input stays masked.
- `TokenCache` owns token persistence under `AppPaths.TokenCachePath`; persistence tests use temp paths.
- Apple 401/429 stay typed exceptions so TUI can show useful failures.
- For undocumented Spotify/Apple web behavior, add parser/client tests and a short brittle-structure comment near the parser.

## TUI Rules

- `TuiApp` partial files split core loop, input, commands, rendering, and messages.
- `TuiState` owns mutable UI state.
- `Components` and `Theme` are render-focused and side-effect free.
- Background operations post `TuiMessage`; state transitions happen on UI loop.
- Register commands in `EnsureCommandsRegistered()` through `TuiCommandRegistry`.
- User-visible command changes must update `/help` and README command docs.
- No blocking work in render path.

## Tests

Use xUnit v3.

Rules:

- Use `TestContext.Current.CancellationToken`; avoid `CancellationToken.None`.
- Reusable fakes live in `tests/Ciderfy.Tests/Fakers`.
- Real-network tests require `[IntegrationTest]`.
- Avoid sleeps/delays. Add deterministic seams, `TaskCompletionSource`, fake handlers, fake time, or blocking test doubles.
- Bug fixes should add/update a failing test when feasible.
- Refactors must keep behavior unchanged and prove it with tests.
- Persistence tests use temp paths and must not touch real user config/cache.

## Review Checklist

Before final response, verify:

- Boundaries still match TUI -> Matching -> providers.
- Transfer still preserves first-seen order and ISRC-first matching.
- Provider web behavior remains account-free and covered by parser/client tests.
- Production HTTP still uses typed-client DI.
- Options/defaults are validated and synchronized with `ciderfy.ini`.
- TUI state transitions happen on UI loop, not render path.
- Tests avoid live networks and use deterministic cancellation/seams.
- Narrow validation command results are reported exactly.
