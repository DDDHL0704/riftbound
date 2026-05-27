# Stage 4D-17KY Recovery Spectator Timing Resolution-History List Payload Shape Audit

Date: 2026-05-27 20:09 CST

Project status: **NOT READY**.

## Scope

This slice covers spectator replay-frame timing `battlefieldResolutions` and `battleResolutions` list payload shape only. It does not change resolution-history value parity, protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final status.

Stage 4D-17KW and Stage 4D-17KX covered spectator timing list payload shape for continuous effects, trigger queue, temporary payment resources and battlefield tasks. Stage 4D-17KY extends the same top-level list payload-shape split to resolution history.

## Runtime Change

- Missing/null spectator timing `battlefieldResolutions` still fails with `spectator replay frame timing battlefield resolutions are required`; present non-list payloads now fail with `spectator replay frame timing battlefield resolutions payload is required`.
- Missing/null spectator timing `battleResolutions` still fails with `spectator replay frame timing battle resolutions are required`; present non-list payloads now fail with `spectator replay frame timing battle resolutions payload is required`.
- Existing non-object battlefield-resolution and battle-resolution item diagnostics continue to accumulate across all expected resolution-history entries in the same validation pass.

## Test Coverage

`RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryPayloadShapeDrift` mutates spectator replay-frame timing payloads so top-level `battlefieldResolutions` and `battleResolutions` values are non-list payloads, then mutates two battlefield-resolution entries and two battle-resolution entries to non-object payloads. The regression proves explicit top-level payload diagnostics and full per-item payload diagnostic accumulation.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `330/330`
- Adjacent recovery/opening/store-smoke filter: `911/911`
- Backend full: `6276/6276`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for spectator replay-frame timing resolution-history list payload shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
