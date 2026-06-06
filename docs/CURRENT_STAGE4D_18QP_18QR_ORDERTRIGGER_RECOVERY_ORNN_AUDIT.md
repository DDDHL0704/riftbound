# Stage 4D 18QP-18QR Order Trigger Recovery Ornn Audit

Date: 2026-06-06 14:40 CST

Owner: A_MAIN

Status: accepted on main. Project remains **NOT READY**.

## Scope

- 18QP added `GameHubJoinTests.OrderTriggersDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation`.
- 18QQ added `MatchRecoveryTests.RecoveryValidatorRejectsOrderTriggersRawPayloadPropertyAndListDrift`.
- 18QR added `OrnnFriendlyEquipmentStaticPowerTests.OrnnDynamicEnemyEquipmentResolveDoesNotChangeStaticAuraParticipantMetadataAcrossPlayerViews`.

Runtime changed: no. This bundle is server test coverage only.

## Coverage Locked

- GameHub active `ORDER_TRIGGERS` coverage now proves exact raw-payload duplicate intent retries replay the accepted active trigger-ordering broadcast without journal growth, while changed raw payloads with the same `clientIntentId` return `CLIENT_INTENT_CONFLICT` without caller/group broadcasts, journal growth or snapshot drift.
- Recovery validation coverage now proves recovered raw `ORDER_TRIGGERS` payload drift diagnostics for malformed ordered list items, duplicate raw properties, surrounding-whitespace property names and surrounding-whitespace trigger id values.
- Ornn/LayerEngine coverage now proves dynamically resolved enemy equipment does not increase Ornn power, does not alter authoritative static-aura participant/dependency metadata, and does not leak ignored enemy equipment ids through P1/P2 snapshot `continuousEffects`.

## Source Commits

- 18QP worker source `d7cec46e7002d617814eda43e95f4d833cb12d91`, cherry-picked to main as `f5a3f207`.
- 18QQ worker source `b24d48bd68d5ca20b6f3a7abef2ed9502db5f3b9`, cherry-picked to main as `0b626300`.
- 18QR worker source `a6b0780be49c37c2b8d506eb96a733e24a29ebe9`, cherry-picked to main as `fa3b8610`.

## Validation

All validation was run on main after cherry-pick:

- Focused new tests: `3/3`.
- Touched class filter: `1495/1495`.
- Broader adjacent server filter: `5421/5421`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7407/7407`.
- `git diff --check`: passed.
- `git diff 0e35bb2d..HEAD --check`: passed before docs sync.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.

DOC_MATRIX_CURRENT remained clean at `17bde0c3` when observed from A_MAIN at 2026-06-06 14:40 CST.

## Remaining Open

This narrows GameHub active `ORDER_TRIGGERS` raw duplicate-intent coverage, recovery raw `ORDER_TRIGGERS` payload-shape diagnostics and Ornn dynamic ignored-enemy-equipment static-aura metadata coverage only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
