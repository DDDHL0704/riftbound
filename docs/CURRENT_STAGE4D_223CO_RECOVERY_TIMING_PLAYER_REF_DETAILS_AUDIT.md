# Stage 4D-223CO Recovery Timing Player-Reference Detail Audit

Date: 2026-06-19 08:44 CST

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / commit: `main` at code commit `330e054e`

Project status: **NOT READY**

## Scope

A_MAIN continued directly on local `main` and accepted a narrow recovery validation diagnostic-detail shard. Runtime changed: yes, diagnostic detail only. Frontend changed: no.

`MatchRecoveryValidator` now appends stable expected/actual details to recovered snapshot and spectator replay timing player-reference diagnostics while preserving the existing missing-player prefixes. Covered surfaces:

- recovered snapshot timing required/optional player pointers and player-id lists
- recovered snapshot timing trigger queue controller membership
- shared timing player-reference helper callers, including spectator replay snapshot stack/lane and spectator replay timing queue/task/battle/battle-damage/resolution player references
- spectator replay trigger queue controller membership helper

The validator still performs the same player/seat membership checks. This checkpoint only improves rejected recovery payload diagnostics by reporting the sorted known player set and the offending player id.

## Files Touched

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Validation

Passed:

- Focused recovery timing player-reference detail tests: `6/6`
- Changed-class `MatchRecoveryTests`: `1974/1974`
- Adjacent Recovery/SpectatorReplay/TriggerQueue/Trigger/Timing/Player/Controller/Lane/Battlefield/Standby/Stack/Battle filter: `3837/3837`
- Backend full via `Riftbound.slnx`: `8307/8307`
- Code/test `git diff --check` before code commit
- Code/test anchored conflict-marker scan had no findings before code commit

## Rule Source

Rule source checked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` from the five root PDFs.

Relevant anchors re-read for this slice: latest core rules 107-129 for player snapshot visibility and hidden/private boundaries, 120-124 for object/card identity context adjacent to snapshot recovery, 303.2.a and 333 for stack/recovery-adjacent state preservation, 382-383 for triggered-skill ordering and controller/source context, 401-405 for active/triggered skill choice/cost/source legality context, 454-461 for battle participant/controller context, and 808.1.d for Last Breath source/detail preservation before discard movement.

## Coordination

No subagent was created. A_MAIN continued in `/Users/dinghaolin/IdeaProjects/riftbound` on local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, `144` commits behind current local `main` and `0` commits ahead at the pre-docs-sync divergence check. Exact divergence must be rechecked before integration.

`codex/rule-audit-remaining-20260615` had no commits ahead of `main`; divergence was `217 0` from `main...codex/rule-audit-remaining-20260615`.

## Non-Goals

This narrows recovered snapshot and spectator replay timing player-reference detail diagnostics only. It does not change valid recovery replay behavior, player identity normalization, trigger ordering, hidden-source redaction, hidden standby redaction, battlefield lane semantics, stack placement, skill activation/trigger confirmation, payment, legality, battle creation, battle damage assignment computation, damage legality, battle cleanup, battlefield control, prompt rendering, source-object serialization, authoritative state serialization, random determinism, remaining authoritative-state direct player diagnostics, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final readiness.

Project remains **NOT READY**.
