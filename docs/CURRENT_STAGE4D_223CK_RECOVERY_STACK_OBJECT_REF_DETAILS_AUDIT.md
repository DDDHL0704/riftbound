# Stage 4D-223CK Recovery Stack Object-Reference Detail Audit

Date: 2026-06-19

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch: `main`

Code commit: `c9d03cca`

Status: accepted, project remains **NOT READY**.

## Scope

This shard narrows recovery validation diagnostics for stack source/target object references across authoritative state and spectator replay frame snapshots. `MatchRecoveryValidator` now appends stable expected/actual details to missing stack source and target object ids while preserving the existing diagnostic prefixes.

Runtime changed: yes, narrow recovery validation diagnostic detail only.

Frontend changed: no.

Test coverage changed: yes. Existing authoritative-state and spectator replay stack object-reference tests now assert detailed suffixes for all covered missing source/target object-reference surfaces.

## Rule Source

Rule source was re-checked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` from the five root PDFs. Relevant anchors: latest core rules 120-124 for game object identity, 303.2.a for simultaneous action/effect/trigger ordering adjacency, 333 for stack creation, 382-383 for triggered-skill stack placement, and 401-405 for skill activation/trigger confirmation, choice, cost and legality context.

## Validation

Passed before code commit:

- Focused stack object-reference detail tests: `3/3`
- Changed-class `MatchRecoveryTests`: `1974/1974`
- Adjacent Recovery/SpectatorReplay/Stack/TriggerQueue/ContinuousEffect/BattlefieldTask/PendingTaskQueue/Battle filter: `3156/3156`
- Backend full via `Riftbound.slnx`: `8307/8307`
- `git diff --check`
- Anchored conflict-marker scan over `docs`, `src` and `tests`
- PCRE2 residual check for authoritative-state and spectator replay stack missing-object assertions without expected/actual details

## Coordination

No subagent was created. A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, 127 commits behind the post-code local `main` and `0` commits ahead at the pre-docs-sync divergence check. A_MAIN must inspect it before integrating UI followup changes and must not develop directly there.

`codex/rule-audit-remaining-20260615` had no commits ahead of `main` at the pre-docs-sync check.

## Remaining Gaps

This shard does not change valid recovery replay behavior, stack placement, trigger ordering, skill activation/trigger confirmation, payment, legality, battle creation, battle damage assignment computation, damage legality, battle cleanup, battlefield control, prompt rendering, hidden-source redaction, source-object serialization, authoritative state serialization, random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final readiness.
