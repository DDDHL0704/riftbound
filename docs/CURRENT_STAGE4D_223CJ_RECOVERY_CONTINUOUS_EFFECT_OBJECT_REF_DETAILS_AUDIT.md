# Stage 4D-223CJ Recovery Continuous Effect Object-Reference Detail Audit

Date: 2026-06-19

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch: `main`

Code commit: `ec5fac5b`

Status: accepted, project remains **NOT READY**.

## Scope

This shard narrows recovery validation diagnostics for timing `continuousEffects` object references across recovered snapshots and spectator replay frames. `MatchRecoveryValidator` now appends stable expected/actual details to missing continuous-effect target, source, participant and dependency object ids while preserving the existing diagnostic prefixes.

Runtime changed: yes, narrow recovery validation diagnostic detail only.

Frontend changed: no.

Test coverage changed: yes. Existing snapshot and spectator replay timing continuous-effect object-reference tests now assert detailed suffixes for all covered missing object-reference surfaces.

## Rule Source

Rule source was re-checked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` from the five root PDFs. Relevant anchors: latest core rules 120-124 for game object identity, 142-143 for unit/damage state adjacency, 317.2.c and 333 for cleanup/stack timing adjacency, 342 and 376 for spell-duel / active-skill timing adjacency, and 454-461 especially 457, 459.2.b.1-459.2.b.4, 460.2.c and 461 for battle lifecycle context.

## Validation

Passed before code commit:

- Focused continuous-effect object-reference detail tests: `8/8`
- Changed-class `MatchRecoveryTests`: `1974/1974`
- Adjacent Recovery/SpectatorReplayTiming/BattlefieldTask/PendingTaskQueue/TriggerQueue/ContinuousEffect/BattleDamageAssignment/DamageAssignment/Battle filter: `2714/2714`
- Backend full via `Riftbound.slnx`: `8307/8307`
- `git diff --check`
- Anchored conflict-marker scan over `docs`, `src` and `tests`
- PCRE2 residual check for continuous-effect missing-object assertions without expected/actual details

## Coordination

No subagent was created. A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, 122 commits behind the post-code local `main` and `0` commits ahead at the pre-docs-sync divergence check. A_MAIN must inspect it before integrating UI followup changes and must not develop directly there.

`codex/rule-audit-remaining-20260615` had no commits ahead of `main` at the pre-docs-sync check.

## Remaining Gaps

This shard does not change valid recovery replay behavior, continuous-effect creation, LayerEngine semantics, trigger ordering, stack placement, payment, legality, battle creation, battle damage assignment computation, damage legality, battle cleanup, battlefield control, prompt rendering, hidden-source redaction, source-object serialization, authoritative state serialization, random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final readiness.
