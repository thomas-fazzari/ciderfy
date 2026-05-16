---
name: ciderfy-guidelines
description: Ciderfy .NET TUI guidelines. Use when changing src/Ciderfy, tests/Ciderfy.Tests, transfer matching, Apple/Spotify/Deezer provider behavior, typed HTTP clients, options/configuration, Spectre.Console TUI commands/rendering, or xUnit tests.
---

# Ciderfy Guidelines

Use AGENTS.md for generic repo rules. This skill keeps only Ciderfy-specific constraints.

## Boundaries

- Keep `Program.cs` thin: build host, call `AddCiderfy(...)`, run `TuiApp`.
- Register features through root `AddCiderfy(...)` and feature-local `DependencyInjection.cs`.
- Keep provider HTTP/parsing details in `Apple`, `Spotify`, or Deezer matching code.
- Keep transfer orchestration and business rules in `Matching`.
- Keep TUI in charge of user flow only.
- Keep `Web` limited to shared HTTP headers, MIME constants, and typed-client helpers.

## Transfer

Flow:

```text
Spotify URL -> Spotify playlist fetch -> merge/dedupe -> Deezer ISRC -> Apple ISRC lookup -> optional text fallback -> Apple playlist create
```

Rules:

- Preserve first-seen track order.
- Dedupe merged playlists by `SpotifyId`.
- Match by ISRC before text; text fallback is riskier.
- Put shared scoring constants in `MatchingConstants`.
- Cover matching changes with focused tests for normalization, thresholds, and duration penalties.

## Providers

- Spotify uses web-player behavior: persisted GraphQL hash, encapsulated TOTP, cookie container, and one unauthorized retry.
- Apple developer-token extraction stays automatic from Apple Music web assets.
- Apple user token enters through `/auth`; TUI input stays masked.
- `TokenCache` owns token persistence under `AppPaths.TokenCachePath`; persistence tests use temp paths.
- Apple 401/429 stay typed exceptions so TUI can show useful failures.
- For brittle undocumented web behavior, add parser/client tests and a short comment near the parser.

## HTTP And Config

- Use typed-client DI for production HTTP clients.
- Apple and Deezer rate limits use `SlidingWindowRateLimiter`; no arbitrary sleeps.
- Options classes need `SectionName`, validation attributes, `[OptionsValidator]`, DI `IValidateOptions<T>`, and `.ValidateOnStart()`.
- Keep option defaults synchronized with `ciderfy.ini`.
- Copy default user config from `ciderfy.ini` on first launch only; never overwrite normal user config.

## TUI

- `TuiApp` partial files split core loop, input, commands, rendering, and messages.
- `TuiState` owns mutable UI state.
- `Components` and `Theme` stay render-focused and side-effect free.
- Background operations post `TuiMessage`; state transitions happen on the UI loop.
- Register commands in `EnsureCommandsRegistered()` through `TuiCommandRegistry`.
- User-visible command changes update `/help` and README command docs.
- Do not block in render paths.

## Tests

- Use xUnit v3 and `TestContext.Current.CancellationToken`.
- Unit tests never hit live networks; use `FakeHttpMessageHandler`.
- Real-network tests require `[IntegrationTest]`.
- Reusable fakes live in `tests/Ciderfy.Tests/Fakers`.
- Persistence tests use temp paths and must not touch real user config/cache.

## Validation

- Start with the narrowest tests for changed behavior.
- Use `make test-unit` for normal code changes.
- Use `make test` when behavior spans providers, matching, or config.
- Use `make lint` for a final commit-worthy gate.
- Report exact commands and results.
