# Stage 4D-17KV Recovery Spectator Snapshot Player Nested Payload Shape Audit

Date: 2026-05-27 18:02 CST

Project status: **NOT READY**.

## Scope

This slice covers spectator replay-frame snapshot player nested payload shape only. It does not change player scalar values, rune-pool/zone/object value parity, protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final status.

Stage 4D-17KT and Stage 4D-17KU covered spectator snapshot player rune-pool and zones value shape. Stage 4D-17KV adds explicit payload-shape diagnostics for malformed spectator player nested payload containers before property-name, value-shape and parity consumers can consume those payloads.

## Runtime Change

- Non-null non-object spectator player `runePool`, `zones` and `objects` payloads now fail with explicit `payload is required` diagnostics. Missing/null required spectator containers keep the existing `is required` / `are required` diagnostics.
- Spectator player `objects` payloads that look object-like for property-name enumeration but cannot be consumed as object maps now fail with `objects payload is required`.
- Expected spectator player object `location` payloads now distinguish missing/null location payloads from non-object location payloads; non-object locations fail with `location payload is required`.
- Existing non-object visible player object entry diagnostics remain in place and are covered with the same nested payload-shape regression.

## Test Coverage

`RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerNestedPayloadShapeDrift` mutates a spectator replay-frame player snapshot with malformed `runePool`, `zones`, `objects`, object-entry and object-location payload shapes, then proves explicit spectator nested payload-shape diagnostics.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `327/327`
- Adjacent recovery/opening/store-smoke filter: `908/908`
- Backend full: `6273/6273`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for spectator replay-frame snapshot player nested payload shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
