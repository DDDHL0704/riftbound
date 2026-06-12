# Stage 4D-211Y Recovery Spectator Timing JSON Number Item Audit

Timestamp: 2026-06-12 18:29 CST

Owner: A_MAIN

## Scope

- Covered spectator replay recovery timing list item payload-shape validation for `continuousEffects[]` and `triggerQueue[]`.
- Added one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees created: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectAndTriggerQueueJsonNumberItemPayloadShapeDrift` builds a spectator replay frame from authoritative state with one redacted continuous effect and one trigger queue item.
- The test replaces the spectator `continuousEffects[0]` and `triggerQueue[0]` items with `RawJson("42")`.
- Recovery validation rejects both JSON number list items as missing object payloads:
  - `spectator replay frame timing continuous effect payload is required`
  - `spectator replay frame timing trigger queue item payload is required`

## Validation

- Focused test: `1/1`.
- Changed-class filter `MatchRecoveryTests`: `1796/1796`.
- Adjacent recovery filter `MatchRecovery`: `1801/1801`.
- Backend full: `8084/8084`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests`, and `src`: passed.

## Commits

- Code commit: `1feb32ed` (`test: cover recovery spectator timing json number items`).
- Docs checkpoint: pending at document creation time.

## Remaining Risk

- This narrows spectator replay timing JSON number item payload-shape validation only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.
- Project remains **NOT READY**.
