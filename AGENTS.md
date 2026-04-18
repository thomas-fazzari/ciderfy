# AGENTS.md

## Project Overview

[README.md](README.md)

### Common Commands

Use `make` as the source of truth for most dev tasks:

| Command          | Purpose                         |
| ---------------- | ------------------------------- |
| `make build`     | Build the solution              |
| `make test`      | Run all tests                   |
| `make test-unit` | Run unit tests only             |
| `make lint`      | Run analyzers (errors only)     |
| `make format`    | Format code (CSharpier)         |
| `make fix`       | Format + lint                   |
| `make install`   | Restore tools and install hooks |

## Documentation

Use the **Context7 MCP** to fetch up-to-date library and framework documentation instead of relying on training data. Prefer it over web search for API references, configuration, and migration guides.

```
mcp_context7_resolve-library-id: "<library name>"
mcp_context7_query-docs: "<topic>"
```

### Useful Docs

| Library                | Context7 ID                       |
| ---------------------- | --------------------------------- |
| Spectre.Console        | `/websites/spectreconsole_net`    |
| Microsoft Learn (.NET) | `/websites/learn_microsoft_en-us` |
| xUnit v3               | `/xunit/xunit.net`                |

## Guidelines

### 1. Think Before Coding

- State assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, list them. Do not pick silently.
- If simpler approach exists, say so.

### 2. Simplicity First

- Write minimum code that solves problem.
- No unrequested features. No premature abstraction. No speculative edge-case handling.
- If solution grows larger than needed, shrink it.

### 3. Surgical Changes

- Touch only code task requires.
- Match existing style exactly.
- Do not rewrite adjacent code, comments, or formatting without reason.
- If unrelated issue appears, mention it. Do not fix silently.
- Remove only imports, variables, or functions made unused by your change.

### 4. Goal-Driven Execution

- Turn request into verifiable goal before coding.
- Bug fix: write or identify failing test first, then make it pass.
- Refactor: prove tests pass before and after.
- For multi-step work, give brief plan with verification steps.
- Never claim done without fresh evidence.

## Slopwatch

[Slopwatch](https://github.com/pmbanugo/slopwatch) detects reward-hacking shortcuts in LLM-generated code (disabled tests, empty catch blocks, suppressed warnings, etc.).

Run after every non-trivial code change:

```
dotnet slopwatch scan
```

A baseline exists at `.slopwatch/baseline.json` for known acceptable patterns. **Never** add new entries to the baseline to hide real problems, fix them instead.
