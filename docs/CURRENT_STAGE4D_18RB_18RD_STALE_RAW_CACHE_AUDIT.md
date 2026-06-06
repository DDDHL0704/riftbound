# Stage 4D-18RB/18RC/18RD Stale Raw Cache Audit

Date: 2026-06-06

Owner: A_MAIN

Status: accepted into main after validation. Project remains **NOT READY**.

## Scope

This audit records the Stage 4D-18RB/18RC/18RD parallel server test bundle. The batch extends stale prompt-scoped raw rejected-intent cache coverage across three additional server surfaces:

- 18RB: typed temporary-resource `PAY_COST` after the typed temporary payment window closes.
- 18RC: Fluft Poro `ACTIVATE_ABILITY` after stack priority starts.
- 18RD: Edge of Night `ASSEMBLE_EQUIPMENT` after equipment attaches.

Runtime changed: no. This is server test coverage only.

## Integrated Commits

- Worker source `a433b0e7` was cherry-picked into main as `92059e87`.
- Worker source `c169c2fd` was cherry-picked into main as `a02ade46`.
- Worker source `594d8eca` was cherry-picked into main as `f7ebe433`.
- A_MAIN added `7a07f4b2` to align the Edge of Night stale-cache prompt/snapshot assertions with authoritative post-assemble projections after focused integration validation caught the mismatch.

## Test Coverage

- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs` extends `PendingPayCostPromptScopedTypedTemporaryResourceReplayAfterWindowClosesRecordsRejectedJournalWithoutMutation`.
- `tests/Riftbound.ConformanceTests/FluftPoroActivatedAbilityTests.cs` extends `FluftPoroActivationStalePromptReplayAfterStackPriorityStartsRejectsWithoutMutation`.
- `tests/Riftbound.ConformanceTests/EdgeOfNightAssembleGuardTests.cs` extends `EdgeOfNightAssembleStalePromptReplayAfterEquipmentAttachesRejectsWithoutMutation`.

Each surface now proves exact duplicate rejected raw submissions replay from the rejected-intent cache without additional journal growth, while changed raw payloads for the same rejected `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot or journal drift.

## Validation

- Focused changed tests: `3/3`.
- Touched class filter: `136/136`.
- Broader adjacent server filter: `4381/4381`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7409/7409`.
- Mechanical checks passed: `git diff --check`, `git diff 8915458c..HEAD --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Remaining Work

This narrows rejected stale raw cache semantics for typed temporary-resource PAY_COST, ACTIVATE_ABILITY and ASSEMBLE_EQUIPMENT only. Broader P0/P1 closure, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
