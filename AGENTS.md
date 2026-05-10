# AGENTS.md

## Prime Directive

- Inspect existing code before deciding.
- Make the smallest correct change.
- Ask one focused question when requirements, API behavior, or architecture direction are unclear.
- Touch only files needed for the task. Do not silently fix unrelated issues.
- Validate with exact commands, then report results.
- Never log or expose tokens, cookies, JWTs, user config, or obfuscated secret material.

## Project Shape

| Path | Owns |
| --- | --- |
| `src/Ciderfy` | host setup, composition root |
| `src/Ciderfy/Apple` | Apple Music auth, API client, token cache |
| `src/Ciderfy/Spotify` | Spotify web-player auth, GraphQL playlist client, URL parsing |
| `src/Ciderfy/Matching` | transfer workflow, Deezer ISRC lookup, fuzzy matching, playlist merge |
| `src/Ciderfy/Tui` | Spectre.Console UI, commands, state, rendering, messages |
| `src/Ciderfy/Web` | shared HTTP constants and typed-client helpers |
| `src/Ciderfy/Configuration` | app paths, INI bootstrap, validation attributes |
| `tests/Ciderfy.Tests` | xUnit v3 tests and fakes |

## Boundaries

- Keep one assembly unless concrete need says otherwise.
- `Program.cs` stays thin: build host, add config, register `AddCiderfy(...)`, run `TuiApp`, stop host.
- Register features through `AddCiderfy(...)` and feature-local `DependencyInjection.cs` files.
- TUI coordinates user flow only. Business workflow stays in `Matching`.
- Provider HTTP/parsing details stay in `Apple`, `Spotify`, or Deezer resolver code.
- `Web` stays shared HTTP headers, MIME constants, and handler/client configuration only.
- Do not add generic service layers, repositories, mediators, CQRS, or new projects without explicit need.

## Transfer Invariants

Flow: Spotify URL -> Spotify playlist fetch -> merge/dedupe -> Deezer ISRC -> Apple ISRC lookup -> optional text fallback -> Apple playlist create.

- Preserve first-seen track order; dedupe merged playlists by `SpotifyId`.
- Keep ISRC matching first. Text matching is fallback because false positives are possible.
- Shared scoring constants belong in `MatchingConstants`.
- Matching changes need focused tests for normalization, thresholds, and duration penalties.
- Unit tests must not hit live networks. Use `FakeHttpMessageHandler`.

## Provider Invariants

- Ciderfy must not require Spotify, Apple, or Deezer developer accounts.
- Spotify uses web-player behavior: persisted GraphQL query hash, encapsulated TOTP, cookie container, unauthorized retry once.
- Apple developer token extraction stays automatic from Apple Music web assets.
- User token enters via `/auth`; TUI input stays masked.
- `TokenCache` owns token persistence under `AppPaths.TokenCachePath`; persistence tests use temp paths.
- Apple 401/429 stay typed exceptions so TUI can show useful failures.

## HTTP, Options, Config

- Production HTTP clients come from typed-client DI only. Do not manually instantiate production `HttpClient`.
- Use `AddStandardResilienceHandler` unless endpoint behavior requires narrower handling.
- Apple and Deezer rate limits use `SlidingWindowRateLimiter`; no arbitrary sleeps.
- Always pass and honor `CancellationToken`; preserve cancellation before broad catches.
- Options classes need `SectionName`, data annotations, `[OptionsValidator]`, DI `IValidateOptions<T>`, and `.ValidateOnStart()`.
- Keep option defaults synchronized with `ciderfy.ini`.
- User config is copied from `ciderfy.ini` on first launch only; never overwrite normal user config.

## TUI

- `TuiApp` partial files split core loop, input, commands, rendering, messages.
- `TuiState` owns mutable UI state. `Components` and `Theme` are render-focused and side-effect free.
- Background operations post `TuiMessage`; state transitions happen on UI loop.
- Register commands in `EnsureCommandsRegistered()` through `TuiCommandRegistry`.
- User-visible command changes must update `/help` and README command docs.
- No blocking work in render path.

## Code Style

- Target .NET 10, nullable enabled, latest C#, analyzers enabled, warnings as errors.
- Prefer `internal sealed`; use `static` for stateless helpers.
- Use file-scoped namespaces and `ConfigureAwait(false)` in async code.
- Use `System.Text.Json`; keep private API DTOs as `file sealed record` near parser/client.
- Use `[GeneratedRegex(..., 1000)]` for regexes.
- Keep constants near owning feature unless shared.
- No broad warning suppressions. If suppression is needed, keep it narrow and explain why.
- No backwards compatibility code unless persisted data, shipped behavior, external consumers, or explicit user need requires it.

## Tests And Gates

Use `make` as source of truth.

| Command | Purpose |
| --- | --- |
| `make install` | restore solution and local tools |
| `make build` | build with analyzers |
| `make test-unit` | tests excluding `Category=Integration` |
| `make test` | all tests |
| `make lint` | CSharpier check, Slopwatch, build |
| `make format` | CSharpier format |

Validation order:

1. Targeted tests for changed behavior.
2. `make test-unit` for normal code changes.
3. `make test` when behavior spans providers, matching, or config.
4. `make lint` before commit-worthy state.

Test rules:

- xUnit v3. Use `TestContext.Current.CancellationToken`; not `CancellationToken.None`.
- Reusable fakes live in `tests/Ciderfy.Tests/Fakers`.
- Real-network tests require `[IntegrationTest]`.
- Avoid sleeps/delays; add deterministic seams.

## External Docs

- Use Context7 before changing code that depends on .NET, Spectre.Console, xUnit, or other library APIs.
- For undocumented Spotify/Apple web behavior, protect changes with parser/client tests and clear brittle-structure comments.

## Hard No

- No token/cookie/JWT/Music-User-Token/client-token logging.
- No production `HttpClient` outside DI typed-client registration.
- No unvalidated meaningful options.
- No arbitrary sleeps for rate limits, retries, or tests.
- No disabled tests or broad suppressions to make gates pass.
- No unrelated rewrites or architecture layers.
