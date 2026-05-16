# AGENTS.md

## Defaults

- Use `$caveman` for every response. Stop only when the user says `normal mode` or `stop caveman`.
- For non-trivial feature, refactor, or architecture work, run `$grill-me` before editing unless the user explicitly skips it.
- Use `$ciderfy-guidelines` when changing `src/Ciderfy`, `tests/Ciderfy.Tests`, provider behavior, matching, config, HTTP setup, TUI behavior, or validation flow.
- Use Context7 for current external library/framework/API/CLI docs before changing code that depends on them.

## Operating Rules

- Inspect existing code before choosing a pattern.
- Keep the smallest correct change and preserve current boundaries.
- Ask one focused question when requirements, API behavior, or architecture direction are ambiguous.
- Touch only files needed for the task; do not silently fix unrelated issues.
- Remove code made obsolete by the change.
- Never log or expose tokens, cookies, JWTs, user config, or obfuscated secret material.

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

- `make format` may rewrite files.
- `make lint` is the commit-worthy gate: CSharpier check, Slopwatch, build.

## Validation

For Ciderfy code, use `$ciderfy-guidelines`. Report exact commands and results. If validation cannot run, say why.

## Git

- Do not stage, unstage, commit, reset, restore, checkout, branch, push, or otherwise mutate git state unless the user explicitly asks for that exact git action.
- For git output requests, run only the requested read-only command and report the important lines.
