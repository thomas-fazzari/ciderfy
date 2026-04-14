# copilot-instructions.md

You MUST use modern CLI tools over legacy equivalents **when available on the system**. They are faster, respect .gitignore by default, and produce cleaner output. If a modern tool is installed, there are NO exceptions — always use it over the legacy equivalent. If a tool is not installed, fall back to the legacy command.

## Search & Navigation

| Use (required) | Instead of (forbidden) | Example |
|---|---|---|
| `rg` | ~~`grep`~~, ~~`grep -r`~~ | `rg "pattern"` |
| `fd` | ~~`find`~~ | `fd "\.ts$"` |
| `fzf` | manual file hunting | `rg -l "TODO" \| fzf` |
| `tree` | manual `ls` traversal | `tree -L 2 src/` |

**Enforcement:**
- Before modifying ANY code, you MUST run `rg` to find all usages and assess impact. No exceptions.
- Before claiming something doesn't exist, you MUST `rg` for it first. "I don't see X" without an `rg` search is an invalid claim.
- Use `rg -l` when you only need file paths, `rg -C 3` for context.
- Use `rg --type <lang>` to scope searches by language (e.g. `rg --type cs`).
- Use `fd` instead of `find` for ANY file discovery. `fd "\.cs$"` to find C# files, `fd -t d` for directories.

## File Manipulation

| Use (required) | Instead of (forbidden) | Example |
|---|---|---|
| `sd` | ~~`sed`~~, ~~`sed -i`~~ | `sd 'old' 'new' file` |
| `jq` | manual JSON parsing | `jq '.name' package.json` |
| `yq` | manual YAML/TOML parsing | `yq '.version' pubspec.yaml` |

**Enforcement:**
- NEVER use `sed` for string replacements. Always use `sd`. `sd` avoids regex escaping pitfalls and is safer.
- NEVER write custom parsers for JSON — use `jq`. NEVER write custom parsers for YAML/TOML — use `yq`.

## Code Analysis

| Use (required) | Instead of (forbidden) | Example |
|---|---|---|
| `bat` | ~~`cat`~~ | `bat src/main.rs` |
| `tokei` | manual LOC counting | `tokei` |
| `ast-grep` | fragile regex refactors | `ast-grep --pattern 'console.log($$$)'` |
| `difftastic` | ~~`diff`~~ (for code review) | `difftastic old.cs new.cs` |

**Enforcement:**
- NEVER use `cat` to read files — use `bat` for syntax highlighting and line numbers.
- Before proposing a refactoring plan, run `tokei` to assess project size and scope.
- Prefer `ast-grep` over regex for any structural code search or replacement. Regex-based refactoring is fragile and error-prone.

## Quick Reference: Forbidden Commands

The following commands are **banned when a modern alternative is available** and must never appear in any shell command you generate:

- ~~`grep`~~ → use `rg`
- ~~`find`~~ → use `fd`
- ~~`sed`~~ → use `sd`
- ~~`cat`~~ → use `bat`
- ~~`diff`~~ → use `difftastic` (for code review)
- ~~`awk`~~ → use `jq` (for JSON) or `rg` (for text extraction)
