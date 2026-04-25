---
name: grill-me
description: Interview the user relentlessly about a plan or design until reaching shared understanding, resolving each branch of the decision tree. Use when user wants to stress-test a plan, get grilled on their design, or mentions "grill me".
---

Interview me relentlessly about every aspect of this plan until we reach a shared understanding. Walk down each branch of the design tree, resolving dependencies between decisions one-by-one. For each question, provide your recommended answer.

## Trigger On

- user says "grill me", "stress-test my plan", or "challenge my design"
- user presents an architecture, feature plan, or technical proposal and wants critical review
- user wants to surface blind spots or unresolved assumptions before building
- AGENTS.md instructs: apply grill-me skill before non-trivial features or refactors
- user asks "what am I missing?" or "poke holes in this"
- user is preparing for a design review, RFC, or technical discussion
- plan has unresolved decision branches that need sequential resolution

Ask the questions one at a time.

If a question can be answered by exploring the codebase, explore the codebase instead.
