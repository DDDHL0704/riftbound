# Stage 4D-10C Final Mulligan First Turn Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official final mulligan completion to first-turn prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens the existing official opening first-turn test in `OfficialOpeningTests`: `OfficialSubmittedDecksStartMulliganThenEnterFirstTurn`.

Runtime behavior was not changed. The existing final mulligan completion path already completed the mulligan phase, ran turn-start, called runes, drew for the active player and exposed the active player's first main-action prompt. This slice binds that success path to event payload, zone / object-location, prompt / snapshot and idle pending-queue contracts.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(...)`.
- Bound the helper to `OfficialSubmittedDecksStartMulliganThenEnterFirstTurn`.
- The audit proves the final accepted `MULLIGAN` moves the match from `MULLIGAN` to `MAIN` / `NEUTRAL_OPEN`, records both players as mulligan-completed, preserves active / turn player identity and keeps priority / focus clear.
- It proves the second player's selected hand card leaves hand, returns to that player's main deck with authoritative `MAIN_DECK` object location, and the replacement card moves to that player's hand with authoritative `HAND` object location.
- It proves the active player's earlier mulligan replacements remain in hand, the turn-start draw moves to hand, and the called runes move from rune deck to base with authoritative `BASE` object locations.
- It proves the event sequence is stable: `MULLIGAN_COMPLETED`, `MULLIGAN_PHASE_COMPLETED`, `TURN_START_BEGAN`, `RUNES_CALLED`, `CARD_DRAWN`, `RUNE_POOL_CLEARED`, `MAIN_PHASE_BEGAN`, with stable mulligan / phase / rune / draw payload counts and no `BURNOUT_APPLIED`.
- It proves snapshots keep main timing, empty stack, no priority / focus player and an idle pending-task queue.
- It proves the active player is actionable on `MAIN_ACTION` with `END_TURN` and no stale `MULLIGAN`, while the second player is non-actionable on `WAIT` / `SURRENDER` with no enabled `MULLIGAN` candidate.

## Non-Closure

This narrows official final-mulligan completion to first-turn prompt-queue, turn-start and stale-mulligan cleanup drift risk only. It does not close submit-deck breadth, ready breadth, remaining mulligan edge breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
