# Stage 4D-212P Recovery Spectator Continuous Effect Metadata Property Name Count Audit

Timestamp: 2026-06-12 20:42 CST

Owner: A_MAIN

## Scope

- Covered spectator replay timing keyed metadata item property-name validation for `continuousEffects[]` when a continuous-effect count mismatch is also present.
- Added one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees created: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedMetadataPropertyNameWithCountMismatch` builds a spectator replay frame from authoritative continuous-effect state.
- The test replaces the first spectator continuous-effect item with raw JSON carrying duplicate `effectKind`, whitespace-wrapped `sourceCardNo` beside canonical `sourceCardNo`, duplicate `sourcePath`, whitespace-wrapped `layerEngineStatus` beside canonical `layerEngineStatus`, and an empty property name.
- The test appends `effect-extra`, forcing spectator continuous-effect count drift from `1` to `2`.
- Recovery validation emits all spectator continuous-effect metadata property-name diagnostics, reports `effect-extra`, and emits the continuous-effect count mismatch.

## Validation

- Focused test: `1/1`.
- Changed-class filter `MatchRecoveryTests`: `1813/1813`.
- Adjacent recovery filter `MatchRecovery`: `1818/1818`.
- Backend full: `8101/8101`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests`, and `src`: passed.

## Commits

- Code commit: `212fe39b` (`test: cover spectator continuous effect metadata property names with count drift`).
- Docs checkpoint: pending at document creation time.

## Remaining Risk

- This narrows spectator replay timing continuous-effect metadata property-name validation with count mismatch only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.
- Project remains **NOT READY**.
