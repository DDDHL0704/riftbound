# Stage 4D-18QY/18QZ/18RA Stale Raw Cache Audit

Date: 2026-06-06

Owner: A_MAIN

Status: accepted into main after validation. Project remains **NOT READY**.

## Scope

This audit records the Stage 4D-18QY/18QZ/18RA parallel server test bundle. The batch extends stale prompt-scoped raw rejected-intent cache coverage across three already-audited surfaces:

- 18QY: temporary-payment-resource `PAY_COST` after the temporary payment window closes.
- 18QZ: spell-duel `PASS_FOCUS` after focus handoff.
- 18RA: `ORDER_TRIGGERS` after stack priority starts.

Runtime changed: no. This is server test coverage only.

## Integrated Commits

- Worker source `0d0ec270` was cherry-picked into main as `bd2361f7`.
- Worker source `8021b5e9` was cherry-picked into main as `f410bff0`.
- Worker source `5b0eed99` was cherry-picked into main as `ac99a70f`.

## Test Coverage

- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs` extends `PendingPayCostPromptScopedTemporaryResourceReplayAfterWindowClosesRecordsRejectedJournalWithoutMutation`.
- `tests/Riftbound.ConformanceTests/SpellDuelBattleStateMachineTests.cs` extends `PassFocusStalePromptReplayAfterFocusHandoffRecordsRejectedJournalWithoutMutation`.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs` extends `OrderTriggersStaleRawPromptAfterStackPriorityStartsRecordsRejectedJournalWithoutMutation`.

Each surface now proves exact duplicate rejected raw submissions replay from the rejected-intent cache without additional journal growth, while changed raw payloads for the same rejected `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot or journal drift.

## Validation

- Focused changed tests: `3/3`.
- Touched class filter: `249/249`.
- Broader adjacent server filter: `5551/5551`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7409/7409`.
- Mechanical pre-doc checks passed: `git diff --check`, `git diff 0c851646..HEAD --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Remaining Work

This narrows rejected stale raw cache semantics for temporary-payment-resource PAY_COST, PASS_FOCUS and ORDER_TRIGGERS only. Broader P0/P1 closure, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
