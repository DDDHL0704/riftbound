# Stage 4D-18NM/18NN/18NO GameHub / Recovery / Ornn Audit

Date: 2026-06-06

Owner: A_MAIN

Status: accepted into main as a server test breadth checkpoint. Project remains **NOT READY**.

## Scope

- 18NM added `SubmitIntentAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`. It locks GameHub after-finished `SubmitIntent` behavior with sentinel raw payload and client intent id values: stable `MatchFinished` response, no raw/client-intent/internal text leakage, no caller/group event/snapshot/prompt broadcast, and no journal growth.
- 18NN added `RecoveryValidatorRejectsSpectatorReplayTimingPendingPaymentResourceActionListElementShapeDrift` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`. It locks spectator replay timing pending-payment `paymentResourceActions` element-shape validation separately from existing list-payload shape and readable value drift coverage.
- 18NO added `OrnnDynamicEquipmentRemovalRefreshesStaticAuraMetadataAcrossPlayerViews` in `tests/Riftbound.ConformanceTests/OrnnFriendlyEquipmentStaticPowerTests.cs`. It uses a real accepted play command with a return-friendly-equipment optional cost to move one friendly equipment out of public field, then verifies Ornn static-aura metadata and P1/P2 snapshots retain only the remaining friendly equipment.

## Integration Notes

- Worker source commits: `48c957a6` (18NM), `c3ad79bf` (18NN), and `d246c1f0` (18NO).
- All three worker commits were reviewed and cherry-picked with `-n` into main.
- Runtime changed: no. Test coverage only.

## Validation

- Focused new tests: `3/3`.
- Touched class filter (`GameHubJoinTests|MatchRecoveryTests|OrnnFriendlyEquipmentStaticPowerTests`): `1467/1467`.
- Broader adjacent server filter: `5392/5392`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7318/7318`.
- Mechanical checks passed: `git diff --cached --check`, `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Remaining Open

- P0/P1 closure, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
