# AGENTS.md

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

## Git

- Do not stage, unstage, commit, reset, restore, checkout, branch, push, or otherwise mutate git state unless the user explicitly asks for that exact git action.
- For git output requests, run only the requested read-only command and report the important lines.
