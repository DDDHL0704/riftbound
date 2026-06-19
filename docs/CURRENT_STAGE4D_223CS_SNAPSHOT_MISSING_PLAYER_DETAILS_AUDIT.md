# Stage 4D-223CS Snapshot Missing-Player Detail Audit

Date: 2026-06-19 09:42 CST

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / commit: `main` at code commit `c94b9933`

Project status: **NOT READY**

## Scope

A_MAIN continued directly on local `main` and accepted a narrow recovery validation diagnostic-detail shard. Runtime changed: yes, diagnostic detail only. Frontend changed: no.

`MatchRecoveryValidator` now appends stable expected/actual details while preserving the existing diagnostic prefixes for:

- recovered snapshot player maps missing an expected recovered player
- spectator replay snapshot player maps missing an expected authoritative seat player

The validator still performs the same player-map and authoritative-seat coverage checks. This checkpoint only improves rejected recovery/spectator payload diagnostics by reporting the sorted expected player set and the actual snapshot player set.

## Files Touched

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- `docs/CURRENT_STAGE4D_223CS_SNAPSHOT_MISSING_PLAYER_DETAILS_AUDIT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- `docs/CURRENT_STAGE4D_SHARED_COORDINATION_BOARD.md`

## Validation

Passed:

- Focused recovered/spectator missing-player detail tests: `2/2`
- Changed-class `MatchRecoveryTests`: `1976/1976`
- Adjacent Recovery/SpectatorReplay/Snapshot/Player/Seat/AuthoritativeState/Timing/TriggerQueue/Stack/Battle filter: `3722/3722`
- Backend full via `Riftbound.slnx`: `8309/8309`
- `git diff --check`
- Code/test/docs anchored conflict-marker scan had no findings
- Residual check found no selected recovered/spectator missing-player diagnostics without expected/actual details

## Rule Source

Rule source checked through `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` from the five root PDFs. This slice does not change rules behavior. Relevant standing anchors re-read for diagnostic context: latest core rules 107-129 for player zones, hidden/private/public boundaries and card-back redaction; and 649-652 for player surrender/removal boundaries and removed-player inability to affect the game.

## Coordination

No subagent was created. A_MAIN continued in `/Users/dinghaolin/IdeaProjects/riftbound` on local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, `164` commits behind current local `main` and `0` commits ahead at the post-code divergence check. A_MAIN did not develop there.

`codex/rule-audit-remaining-20260615` had no commits ahead of `main`; divergence was `237 0` from `main...codex/rule-audit-remaining-20260615`.

## Non-Goals

This narrows recovered/spectator missing-player detail diagnostics only. It does not change valid recovery replay behavior, player identity normalization, seat validation, trigger ordering, hidden-source redaction, hidden standby redaction, battlefield lane semantics, stack placement, skill activation/trigger confirmation, payment, legality, battle creation, battle damage assignment computation, damage legality, battle cleanup, battlefield control, prompt rendering, source-object serialization, authoritative state serialization, random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final readiness.

Project remains **NOT READY**.
