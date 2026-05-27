# Stage 4D-17KX Recovery Spectator Timing Resource/Task List Payload Shape Audit

Date: 2026-05-27 20:02 CST

Project status: **NOT READY**.

## Scope

This slice covers spectator replay-frame timing `temporaryPaymentResources` and `battlefieldTasks` list payload shape only. It does not change timing value parity, protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final status.

Stage 4D-17KW covered spectator timing `continuousEffects` and `triggerQueue` list payload shape. Stage 4D-17KX extends that same payload-shape split to temporary payment resources and battlefield tasks.

## Runtime Change

- Missing/null spectator timing `temporaryPaymentResources` still fails with `spectator replay frame timing temporary payment resources are required`; present non-list payloads now fail with `spectator replay frame timing temporary payment resources payload is required`.
- Missing/null spectator timing `battlefieldTasks` still fails with `spectator replay frame timing battlefield tasks are required`; present non-list payloads now fail with `spectator replay frame timing battlefield tasks payload is required`.
- Non-object temporary-payment-resource and battlefield-task entries now continue validation across the expected list instead of returning on the first malformed item, so multiple malformed item payloads are reported in one pass.

## Test Coverage

`RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceAndBattlefieldTaskPayloadShapeDrift` mutates spectator replay-frame timing payloads so top-level `temporaryPaymentResources` and `battlefieldTasks` values are non-list payloads, then mutates two temporary-payment-resource entries and two battlefield-task entries to non-object payloads. The regression proves explicit top-level payload diagnostics and full per-item payload diagnostic accumulation.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `329/329`
- Adjacent recovery/opening/store-smoke filter: `910/910`
- Backend full: `6275/6275`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for spectator replay-frame timing temporary-payment-resource and battlefield-task list payload shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
