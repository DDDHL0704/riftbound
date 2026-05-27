# Stage 4D-17KW Recovery Spectator Timing List Payload Shape Audit

Date: 2026-05-27 19:52 CST

Project status: **NOT READY**.

## Scope

This slice covers spectator replay-frame timing `continuousEffects` and `triggerQueue` list payload shape only. It does not change timing value parity, protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final status.

Stage 4D-17GK covered spectator timing continuous-effect and trigger-queue item property-name shape. Stage 4D-17KW adds explicit top-level list payload-shape diagnostics and continues item-level payload-shape validation across all expected timing list entries.

## Runtime Change

- Missing/null spectator timing `continuousEffects` still fails with `spectator replay frame timing continuous effects are required`; present non-list payloads now fail with `spectator replay frame timing continuous effects payload is required`.
- Missing/null spectator timing `triggerQueue` still fails with `spectator replay frame timing trigger queue is required`; present non-list payloads now fail with `spectator replay frame timing trigger queue payload is required`.
- Non-object continuous-effect and trigger-queue entries now continue validation across the expected list instead of returning on the first malformed item, so multiple malformed item payloads are reported in one pass.

## Test Coverage

`RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectAndTriggerQueuePayloadShapeDrift` mutates spectator replay-frame timing payloads so top-level `continuousEffects` and `triggerQueue` values are non-list payloads, then mutates two continuous-effect entries and two trigger-queue entries to non-object payloads. The regression proves explicit top-level payload diagnostics and full per-item payload diagnostic accumulation.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `328/328`
- Adjacent recovery/opening/store-smoke filter: `909/909`
- Backend full: `6274/6274`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for spectator replay-frame timing continuous-effect and trigger-queue list payload shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
