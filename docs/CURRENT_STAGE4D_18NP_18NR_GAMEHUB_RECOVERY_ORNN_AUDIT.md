# Stage 4D-18NP/18NQ/18NR GameHub / Recovery / Ornn Audit

Date: 2026-06-06

Owner: A_MAIN

Status: accepted into main as a server test breadth checkpoint. Project remains **NOT READY**.

## Scope

- 18NP added `RecoveryValidatorRejectsSpectatorReplayTimingPendingPaymentChoiceListElementShapeDrift` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`. It locks spectator replay timing pending-payment `paymentChoices` element-shape validation separately from existing list-payload shape, readable value drift, and resource-action element-shape coverage.
- 18NQ added `SubmitDeckAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`. It locks GameHub after-finished `SUBMIT_DECK` behavior with sentinel raw payload and client intent id values: stable `MatchFinished` response, no raw/client-intent/internal text leakage in the user-visible error, no caller/group event/snapshot/prompt broadcast, and a stable rejected journal entry.
- 18NR added `OrnnDynamicLastEquipmentRemovalOmitsStaticAuraParticipantMetadataAcrossPlayerViews` in `tests/Riftbound.ConformanceTests/OrnnFriendlyEquipmentStaticPowerTests.cs`. It uses a real accepted play command with a return-friendly-equipment optional cost to remove the last friendly equipment from public field, then verifies Ornn static-aura metadata and P1/P2 snapshots omit participant metadata and the removed equipment id.

## Integration Notes

- Worker source commits: `a1e72446` (18NP), `f97dcc32` (18NQ), and `dad67009` (18NR).
- All three worker commits were reviewed and cherry-picked with `-n` into main.
- A_MAIN corrected the 18NQ journal assertion during integration: after-finished `SUBMIT_DECK` records a rejected journal entry with snapshots for recovery, so the integrated test asserts the stable rejected record rather than no journal growth.
- Runtime changed: no. Test coverage only.

## Validation

- Focused new tests: `3/3`.
- Touched class filter (`GameHubJoinTests|MatchRecoveryTests|OrnnFriendlyEquipmentStaticPowerTests`): `1470/1470`.
- Broader adjacent server filter: `5395/5395`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7321/7321`.
- Mechanical checks passed: `git diff --cached --check`, `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Remaining Open

- P0/P1 closure, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
