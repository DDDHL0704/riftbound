# Stage 4D-223CM Recovery Card Object-Reference Detail Audit

Date: 2026-06-19 08:10 CST

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / commit: `main` at code commit `245faf90`

Project status: **NOT READY**

## Scope

A_MAIN continued directly on local `main` and accepted a narrow recovery validation diagnostic-detail shard. Runtime changed: yes, diagnostic detail only. Frontend changed: no.

`MatchRecoveryValidator` now appends stable expected/actual details to authoritative-state card/object-location object-reference diagnostics while preserving the existing missing-object prefixes. Covered surfaces:

- `card object {id} attached object`
- `card object {id} power modifier {effectId} source object`
- `object location {id} battlefield object`

The validator still performs the same object registry membership checks. This checkpoint only makes rejected recovery payloads report the sorted known object registry and the offending object id.

## Files Touched

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Validation

Passed:

- Focused card/object-location object-reference detail tests: `2/2`
- Changed-class `MatchRecoveryTests`: `1974/1974`
- Adjacent Recovery/AuthoritativeState/CardObject/ObjectLocation/PowerModifier/Stack/TriggerQueue/PendingHandChoice/TemporaryPayment/SpectatorReplay filter: `2596/2596`
- Backend full via `Riftbound.slnx`: `8307/8307`
- `git diff --check`
- `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs src tests` had no findings
- PCRE2 residual check found no selected authoritative card/object-location missing-object assertions without expected/actual details before docs sync

## Rule Source

Rule source checked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` from the five root PDFs.

Relevant anchors re-read for this slice: latest core rules 120-124 for object/card identity and rule-text objects, 303.2.a and 333 for stack/recovery-adjacent object preservation, 382-383 for triggered-skill object/source context, and 401-405 for pending skill choice/cost/source legality context.

## Coordination

No subagent was created. A_MAIN continued in `/Users/dinghaolin/IdeaProjects/riftbound` on local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, `134` commits behind current local `main` and `0` commits ahead at the pre-docs-sync divergence check. Exact divergence must be rechecked before integration.

`codex/rule-audit-remaining-20260615` had no commits ahead of `main`; divergence was `207 0` from `main...codex/rule-audit-remaining-20260615`.

## Non-Goals

This narrows recovery authoritative card/object-location object-reference diagnostics only. It does not change valid recovery replay behavior, object location semantics, attachment semantics, power modifier creation, trigger ordering, stack placement, skill activation/trigger confirmation, payment, legality, battle creation, battle damage assignment computation, damage legality, battle cleanup, battlefield control, prompt rendering, hidden-source redaction, source-object serialization, authoritative state serialization, random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final readiness.

Project remains **NOT READY**.
