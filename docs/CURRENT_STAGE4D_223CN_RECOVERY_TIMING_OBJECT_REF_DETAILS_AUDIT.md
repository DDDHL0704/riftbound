# Stage 4D-223CN Recovery Timing Object-Reference Detail Audit

Date: 2026-06-19 08:23 CST

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / commit: `main` at code commit `0bcb5976`

Project status: **NOT READY**

## Scope

A_MAIN continued directly on local `main` and accepted a narrow recovery validation diagnostic-detail shard. Runtime changed: yes, diagnostic detail only. Frontend changed: no.

`MatchRecoveryValidator` now appends stable expected/actual details to recovered snapshot and spectator replay timing object-reference diagnostics while preserving the existing missing-object prefixes. Covered surfaces:

- recovered snapshot timing trigger queue visible source, Kogmaw Last Breath battlefield context, and friendly-destroyed destroyed-object context
- spectator replay snapshot lane battlefield object-id references
- spectator replay snapshot lane battlefield occupant, units-by-side, visible standby, and visible standby-slot object references
- the shared timing object-reference helper, so remaining legacy timing object-reference callers now report the sorted known object set and the offending object id

The validator still performs the same object membership checks. This checkpoint only improves rejected recovery payload diagnostics.

## Files Touched

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Validation

Passed:

- Focused timing/snapshot object-reference detail tests: `8/8`
- Changed-class `MatchRecoveryTests`: `1974/1974`
- Adjacent Recovery/SpectatorReplay/TriggerQueue/Trigger/Lane/Battlefield/Standby/ContinuousEffect/Battle filter: `2941/2941`
- Backend full via `Riftbound.slnx`: `8307/8307`
- `git diff --check`
- `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs src tests` had no findings
- PCRE2 residual check found no selected recovered snapshot or spectator lane missing-object assertions without expected/actual details before docs sync

## Rule Source

Rule source checked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` from the five root PDFs.

Relevant anchors re-read for this slice: latest core rules 120-124 for object/card identity, 128-129 for hidden/private object visibility, 303.2.a and 333 for stack/recovery-adjacent object preservation, 382-383 for triggered-skill ordering and source context, 401-405 for triggered-skill choice/cost/source legality context, and 808.1.d for Last Breath source/detail recording before discard movement.

## Coordination

No subagent was created. A_MAIN continued in `/Users/dinghaolin/IdeaProjects/riftbound` on local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, `136` commits behind current local `main` and `0` commits ahead at the pre-docs-sync divergence check. Exact divergence must be rechecked before integration.

`codex/rule-audit-remaining-20260615` had no commits ahead of `main`; divergence was `209 0` from `main...codex/rule-audit-remaining-20260615`.

## Non-Goals

This narrows recovered snapshot and spectator replay timing object-reference detail diagnostics only. It does not change valid recovery replay behavior, trigger ordering, Kogmaw/Last Breath behavior, hidden standby redaction, battlefield lane semantics, object location semantics, stack placement, skill activation/trigger confirmation, payment, legality, battle creation, battle damage assignment computation, damage legality, battle cleanup, battlefield control, prompt rendering, source-object serialization, authoritative state serialization, random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final readiness.

Project remains **NOT READY**.
