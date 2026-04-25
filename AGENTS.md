# AGENTS.md

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

- Never guess. If requirements are uncertain, stop and ask.
- If multiple interpretations exist, list them. Do not silently pick one.
- If a simpler approach exists, propose it first.

### 2. Simplicity First

- Write the minimum code required. No unrequested features, no premature abstractions.
- If the solution grows unnecessarily, shrink it back.

### 3. Surgical Changes

- Touch only the code required by the task.
- Match existing style and formatting exactly.
- Report unrelated issues — do not silently fix them.
- Remove only what your change made obsolete.

### 4. Goal-Driven Execution

- Translate requests into a concrete, verifiable goal before writing code.
- Bug fixes: write a failing test first, then fix.
- Refactors: prove tests pass before and after.
- Never claim "done" without evidence (test output, logs, build).
