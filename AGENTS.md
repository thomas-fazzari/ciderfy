# AGENTS.md

## Operating Loop

1. Inspect existing code before choosing a pattern.
2. Keep the smallest correct change.
3. Preserve current architecture boundaries.
4. Do not introduce layers, projects, services, mediators, repositories, CQRS, or compatibility shims unless the user asks, persisted/shipped behavior requires it, or at least two existing call sites duplicate the same 20+ lines or same 3+ operation workflow.
5. Run the narrowest useful validation first, then broader gates when done.
6. Report exact validation commands and results.

Do not guess. If requirements, API behavior, or architecture direction are ambiguous, ask one focused question before editing.

Never log or expose tokens, cookies, JWTs, user config, or obfuscated secret material.

## Skill Routing

- `$caveman` is active by default for every response. Disable it only when the user explicitly says `normal mode` or `stop caveman`.
- Use `$grill-me` before any non-trivial feature, refactor, or architecture work unless the user explicitly says to skip grill-me.
- Use `$ciderfy-guidelines` when changing `src/Ciderfy`, `tests/Ciderfy.Tests`, config defaults, provider behavior, matching behavior, TUI commands, or HTTP client setup.
- Use Context7 for current external library/framework/API docs before changing code that depends on .NET, Spectre.Console, xUnit, Polly/resilience, rate limiting, or other packages, SDKs, CLI tools, or framework APIs.

## Subagents

Subagents are authorized for non-trivial tasks when independent work can run in parallel.

Usecases:

- independent codebase questions
- large reviews split by area
- implementation split across disjoint files/modules
- verification that can run while local work continues

Rules:

- Do not delegate the immediate blocker on the critical path.
- Give each subagent one concrete scope and expected output.
- For code edits, assign disjoint file ownership and tell subagents not to revert others' changes.
- Continue local non-overlapping work while subagents run.
- Wait only when their result blocks the next step.
- Close subagents when done.

## Project Shape

| Path                        | Role                                                                  |
| --------------------------- | --------------------------------------------------------------------- |
| `src/Ciderfy`               | host setup and composition root                                       |
| `src/Ciderfy/Apple`         | Apple Music auth, API client, developer token extraction, token cache |
| `src/Ciderfy/Spotify`       | Spotify web-player auth, GraphQL playlist client, URL parsing         |
| `src/Ciderfy/Matching`      | transfer workflow, Deezer ISRC lookup, fuzzy matching, playlist merge |
| `src/Ciderfy/Tui`           | Spectre.Console UI, commands, state, rendering, messages              |
| `src/Ciderfy/Web`           | shared HTTP constants and typed-client helpers                        |
| `src/Ciderfy/Configuration` | app paths, INI bootstrap, validation attributes                       |
| `tests/Ciderfy.Tests`       | xUnit v3 tests and fakes                                              |

Rules:

- Keep one assembly unless concrete need says otherwise.
- `Program.cs` stays thin: build host, add config, register `AddCiderfy(...)`, run `TuiApp`, stop host.
- Register features through `AddCiderfy(...)` and feature-local `DependencyInjection.cs` files.
- TUI coordinates user flow only. Business workflow stays in `Matching`.
- Provider HTTP/parsing details stay in `Apple`, `Spotify`, or Deezer resolver code.
- `Web` stays shared HTTP headers, MIME constants, and handler/client configuration only.

## Commands

Use `make` as source of truth.

```bash
make install
make format
make build
make test-unit
make test
make lint
```

Rules:

- `make format` may rewrite files.
- `make lint` is the commit-worthy gate: CSharpier check, Slopwatch, build.
- Unit tests must not hit live networks. Use `FakeHttpMessageHandler`.

## Validation

Default order:

1. Targeted tests for changed behavior.
2. `make test-unit` for normal code changes.
3. `make test` when behavior spans providers, matching, or config.
4. `make build` after project, dependency, analyzer, or source-generator changes.
5. `make lint` for final commit-worthy state.

Never claim done without validation evidence. If validation cannot run, say why.

## Change Discipline

- Touch only files needed for the task.
- Preserve existing style and naming.
- Remove code made obsolete by the change.
- Do not silently fix unrelated issues.
- Do not add backward compatibility unless persisted data, shipped behavior, external consumers, or explicit user need requires it.
- For bug fixes, add or update a failing test first when feasible.
- For refactors, keep behavior unchanged and prove it with tests.
- Do not bypass formatters, analyzers, Slopwatch, warning-as-error gates, or strict test rules.
- Do not add warning suppressions, disabled tests, arbitrary delays, or project-level `NoWarn` unless explicitly justified.

## Hard Prohibitions

- No token/cookie/JWT/Music-User-Token/client-token logging.
- No production `HttpClient` outside DI typed-client registration.
- No live-network unit tests.
- No arbitrary sleeps for rate limits, retries, or tests.
- No unvalidated meaningful options.
- No disabled tests or broad suppressions to make gates pass.
- No unrelated rewrites or architecture layers.

## Git

- Do not stage, unstage, commit, reset, restore, checkout, branch, push, or otherwise mutate git state unless the user explicitly asks for that exact git action.
- Do not use git as a review workflow substitute. Prefer reading files and running validation.
- If the user asks for git output, run only the requested read-only command and report the important lines.
