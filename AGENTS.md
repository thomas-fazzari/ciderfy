# AGENTS.md

**Current Stack:** .NET 10, Spectre.Console TUI, Microsoft.Extensions.Hosting, xUnit v3, Bogus, Moq

**Structure:**
- `src/Ciderfy/` -> feature-driven TUI app
- `tests/Ciderfy.Tests/` -> tests and fakers

**Useful Commands:** `make test` | `make test-unit` | `make lint` | `make fix`

**Enforcement:** csharpier (C#), TreatWarningsAsErrors, Meziantou+Roslynator analyzers, slopwatch, husky pre-commit (runs `make lint`)

**Goal**: maximize quality and delivery speed, avoid empty process, and keep changes precise.

## 1) Think Before Coding

Don't assume. Don't hide confusion. Surface tradeoffs.
Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2) Simplicity First

Minimum code that solves the problem. Nothing speculative.
- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3) Surgical Changes

Touch only what you must. Clean up only your own mess.

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

## 4) Goal-Driven Execution

Define success criteria. Loop until verified.

Transform tasks into verifiable goals:

- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:

1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

## 5) Skill Usage (Context-Aware)

- Load skills with clear triggers and expected benefit.
- Match analysis depth to task risk and complexity.
- Default to one process skill; add another when it meaningfully improves outcomes.
- Avoid mandatory workflow ceremony on simple tasks.
- Do not optimize for low token usage at the expense of correctness.

## 6) Verification Before Claims

- Never claim "done/fixed/passing" without fresh evidence.
- Report what was verified and what remains unverified.
- If blocked, state exactly why and what information is missing.
