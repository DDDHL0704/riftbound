# Stage 4D-223CR Recovery Player-Coverage Detail Audit

Date: 2026-06-19 09:33 CST

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / commit: `main` at code commit `e1d58f72`; post-merge current head `c3a27845`

Project status: **NOT READY**

## Scope

A_MAIN continued directly on local `main` and accepted a narrow recovery validation diagnostic-detail shard. Runtime changed: yes, diagnostic detail only. Frontend changed: no in the code shard.

`MatchRecoveryValidator` now appends stable expected/actual details while preserving the existing diagnostic prefixes for:

- recovered snapshot active-player ids that are not present in that snapshot player map
- spectator replay snapshot top-level player ids that are not present in authoritative seats

The validator still performs the same player-map and seat-membership checks. This checkpoint only improves rejected recovery/spectator payload diagnostics by reporting the sorted known player set and the offending player id.

After the code commit, A_MAIN merged remote `main` commit `7afabcba` (`统一线框日志对象定位`) into local `main`, yielding merge commit `c3a27845`. That merge accepted DevUi wire log/object-reference positioning work only.

## Files Touched

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- `docs/CURRENT_STAGE4D_223CR_RECOVERY_PLAYER_COVERAGE_DETAILS_AUDIT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- `docs/CURRENT_STAGE4D_SHARED_COORDINATION_BOARD.md`

## Validation

Passed before the post-code DevUi merge:

- Focused recovery player-coverage detail tests: `2/2`
- Changed-class `MatchRecoveryTests`: `1975/1975`
- Adjacent Recovery/SpectatorReplay/Snapshot/Player/Seat/AuthoritativeState/Timing/TriggerQueue/Stack/Battle filter: `3721/3721`
- Backend full via `Riftbound.slnx`: `8308/8308`
- `git diff --check`
- Code/test/docs anchored conflict-marker scan had no findings
- Residual check found no selected active-player or spectator top-level seat diagnostics without expected/actual details

Passed after merging remote `main` commit `7afabcba`:

- `npm --prefix src/Riftbound.DevUi run build`

## Rule Source

Rule source checked through `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` from the five root PDFs. This slice does not change rules behavior. Relevant standing anchors re-read for diagnostic context: latest core rules 107-129 for player zones, hidden/private/public boundaries and card-back redaction; and 649-652 for player surrender/removal boundaries and removed-player inability to affect the game.

## Coordination

No subagent was created. A_MAIN continued in `/Users/dinghaolin/IdeaProjects/riftbound` on local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, `162` commits behind current local `main` and `0` commits ahead at the post-merge divergence check. A_MAIN did not develop there.

`codex/rule-audit-remaining-20260615` had no commits ahead of `main`; divergence was `235 0` from `main...codex/rule-audit-remaining-20260615`.

## Non-Goals

This narrows recovered/spectator player-coverage detail diagnostics only. It does not change valid recovery replay behavior, player identity normalization, seat validation, trigger ordering, hidden-source redaction, hidden standby redaction, battlefield lane semantics, stack placement, skill activation/trigger confirmation, payment, legality, battle creation, battle damage assignment computation, damage legality, battle cleanup, battlefield control, prompt rendering, source-object serialization, authoritative state serialization, random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final readiness.

Project remains **NOT READY**.
