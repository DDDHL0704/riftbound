# Stage 4D-10G Max-Selection Mulligan First-Turn Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official maximum-selection mulligan completion / first-turn prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice adds maximum-selection mulligan coverage to `OfficialOpeningTests`: `OfficialMaximumSelectionMulligansEnterFirstTurnWithStableDrawReturnCounts`.

Runtime behavior was not changed. The existing mulligan completion path already handled the configured maximum mulligan selection count, replaced the selected cards, returned them to main deck and advanced to the first turn when both players completed. This slice binds that path to event payload, zone / object-location, prompt / snapshot and idle pending-queue contracts.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `OfficialMaximumSelectionMulligansEnterFirstTurnWithStableDrawReturnCounts`.
- Reused `AssertOfficialMulliganSecondPlayerPromptQueueAudit(...)` to prove the active maximum-selection `MULLIGAN` opens the second-player window while moving selected objects to main deck.
- Reused `AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(...)` to prove both maximum-selection mulligans enter first turn with selected / drawn / called-rune object locations stable.
- The audit proves the active maximum-selection `MULLIGAN` emits `MULLIGAN_COMPLETED` with set-aside, drawn and returned counts equal to `OfficialDeckValidator.MaximumMulliganCount`.
- It proves the final maximum-selection `MULLIGAN` completes the mulligan phase, calls two active-player runes, draws the active player's turn-start card and exposes the active `MAIN_ACTION` prompt without stale `MULLIGAN`.

## Non-Closure

This narrows official maximum-selection mulligan completion, first-turn transition and prompt-queue drift risk only. It does not close submit-deck breadth, ready breadth, remaining mulligan edge breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
