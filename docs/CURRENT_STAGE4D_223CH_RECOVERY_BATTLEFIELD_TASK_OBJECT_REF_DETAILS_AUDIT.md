# Stage 4D-223CH Recovery Battlefield Task Object-Reference Detail Audit

Date: 2026-06-19

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch: `main`

Code commit: `3ec3d5a5`

Status: accepted, project remains **NOT READY**.

## Scope

This shard narrows recovery validation diagnostics for snapshot and spectator replay-frame timing battlefield task object references. `MatchRecoveryValidator` now uses the shared detailed timing object-reference helpers for battlefield task `battlefieldObjectId` and `participantObjectIds`, so missing battlefield-task object ids keep their existing diagnostic prefix and now append stable expected/actual details.

Runtime changed: yes, narrow recovery validation diagnostic detail only.

Frontend changed: no.

Test coverage changed: yes. Existing snapshot and spectator replay timing battlefield task object-reference tests now assert the detailed `expected [] but got ...` suffixes while preserving the broad missing-object diagnostics.

## Rule Source

Rule source was re-checked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` from the five root PDFs. Relevant anchors: latest core rules 120-124 for object/public-zone identity, 142-143 for damage/object state anchors, 317.2.c and 333 for pending task / stack timing anchors, 342 and 376 for spell-duel / active-skill timing, and 454-461 especially 457, 459.2.b.1-459.2.b.4 and 460.2.c for battle lifecycle context.

## Validation

Passed before code commit:

- Focused battlefield task object-reference detail tests: `2/2`
- Changed-class `MatchRecoveryTests`: `1974/1974`
- Adjacent Recovery/SpectatorReplayTiming/BattlefieldTask/PendingTaskQueue/TriggerQueue/ContinuousEffect/BattleDamageAssignment/DamageAssignment/Battle filter: `2714/2714`
- Backend full via `Riftbound.slnx`: `8307/8307`
- `git diff --check`
- Anchored conflict-marker scan over `docs`, `src` and `tests`

## Coordination

No subagent was created. A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, 115 commits behind the post-code `main` and `0` commits ahead at the pre-docs-sync divergence check. A_MAIN must inspect it before integrating UI followup changes and must not develop directly there.

`codex/rule-audit-remaining-20260615` had no commits ahead of `main` at the pre-docs-sync check.

## Remaining Gaps

This shard does not change valid recovery replay behavior, battlefield task creation, spell-duel start, battle start, battle creation, battle damage assignment computation, damage legality, battle cleanup, battlefield control, prompt rendering, hidden-source redaction, source-object serialization, authoritative state serialization, random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final readiness.
