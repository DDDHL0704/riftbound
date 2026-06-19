# Stage 4D-223CL Recovery Workflow Object-Reference Detail Audit

Date: 2026-06-19

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch: `main`

Code commit: `1783a870`

Status: accepted, project remains **NOT READY**.

## Scope

This shard narrows recovery validation diagnostics for authoritative workflow object references. `MatchRecoveryValidator` now appends stable expected/actual details to missing trigger queue source object ids, pending hand choice source/legal object ids, and temporary payment resource source object ids while preserving the existing diagnostic prefixes.

Runtime changed: yes, narrow recovery validation diagnostic detail only.

Frontend changed: no.

Test coverage changed: yes. Existing authoritative-state workflow object-reference tests and adjacent trigger queue visible-source identity tests now assert detailed suffixes for the covered missing source/legal object-reference surfaces.

## Rule Source

Rule source was re-checked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` from the five root PDFs. Relevant anchors: latest core rules 120-124 for game object identity, 303.2.a and 333 for simultaneous action/effect/trigger and stack adjacency, 382-383 for triggered-skill placement and source context, and 401-405 for skill activation/trigger confirmation, choices, costs and legality.

## Validation

Passed before code commit:

- Focused workflow object-reference detail tests: `6/6`
- Changed-class `MatchRecoveryTests`: `1974/1974`
- Adjacent Recovery/SpectatorReplay/TriggerQueue/PendingHandChoice/TemporaryPayment/Stack/ContinuousEffect/BattlefieldTask/PendingTaskQueue/Battle filter: `3200/3200`
- Backend full via `Riftbound.slnx`: `8307/8307`
- `git diff --check`
- Anchored conflict-marker scan over `docs`, `src` and `tests`
- PCRE2 residual check for selected authoritative workflow missing-object assertions without expected/actual details

## Coordination

No subagent was created. A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, 132 commits behind the post-code local `main` and `0` commits ahead at the pre-docs-sync divergence check. A_MAIN must inspect it before integrating UI followup changes and must not develop directly there.

`codex/rule-audit-remaining-20260615` had no commits ahead of `main` at the pre-docs-sync check.

## Remaining Gaps

This shard does not change valid recovery replay behavior, trigger ordering, pending hand-choice generation, temporary payment creation, stack placement, skill activation/trigger confirmation, payment, legality, battle creation, battle damage assignment computation, damage legality, battle cleanup, battlefield control, prompt rendering, hidden-source redaction, source-object serialization, authoritative state serialization, random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final readiness.
