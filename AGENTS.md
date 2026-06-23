# AGENTS.md

## Code style
- Avoid unnecessary abstractions, indirections, wrappers/helper methods and intermediate variables: inline simple expressions and one-off construction unless naming significantly improves correctness, readability, reuse, or test diagnostics.
- Avoid unnecessary fallbacks and backward compatibility layers: write code for the current, explicit requirements. Do not support legacy behaviors, deprecated APIs, or "just in case" edge cases unless explicitly requested.

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
