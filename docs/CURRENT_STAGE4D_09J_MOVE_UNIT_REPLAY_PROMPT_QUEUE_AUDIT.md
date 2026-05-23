# Stage 4D-09J Move-Unit Replay Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 move-unit replay prompt / queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens the existing move-unit accepted replay and stale prompt replay tests in `BoardTaskQueueFoundationTests`: `MoveUnitIntoOccupiedEnemyBattlefieldRejectsAcceptedCommandReplayWithoutMutation` and `MoveUnitStalePromptReplayAfterSpellDuelStartsRejectsWithoutMutation`.

Runtime behavior was not changed. The existing accepted and rejected replay paths already preserved the spell-duel state without mutation; this slice binds both the accepted result and the replay result to the same final spell-duel pending queue, snapshot queue, battlefield-task metadata and prompt action contract.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertMoveUnitSpellDuelPromptQueueAudit(...)` in `BoardTaskQueueFoundationTests`.
- Reused the helper for both the accepted move and the rejected accepted-command replay result.
- Reused the helper for both the accepted prompt-scoped move and the rejected stale-prompt replay result.
- The audit proves the final state remains `SPELL_DUEL_OPEN` with P2 focus and active task `task:start-spell-duel:BF-CONTEST`.
- It proves the state pending-task queue and P2 snapshot queue keep deterministic `BATTLEFIELD_CONTESTED`, `START_SPELL_DUEL` and `START_BATTLE` order.
- It proves P2 snapshot `battlefieldTasks` exposes active spell-duel plus waiting battle metadata for BF-CONTEST.
- It proves P2 remains actionable on `PASS_FOCUS` / `SURRENDER`, while stale `MOVE_UNIT` is absent.
- It proves P1 remains non-actionable on the same spell-duel prompt and also lacks stale `PASS_FOCUS` / `MOVE_UNIT`.

## Non-Closure

This narrows move-unit replay / stale prompt final prompt-queue drift risk only. It does not close full move-unit legality breadth, full battle lifecycle breadth, full spell-duel lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, READY or goal completion.
