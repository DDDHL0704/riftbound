# Stage 4D-212L Recovery Spectator Continuous Effect Metadata Property Name Audit

Timestamp: 2026-06-12 20:11 CST

Owner: A_MAIN

## Scope

- Covered spectator replay timing keyed metadata item property-name validation for `continuousEffects[]`.
- Added one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees created: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedMetadataPropertyNameWithoutCountMismatch` builds a spectator replay frame from authoritative continuous-effect state.
- The test replaces the single spectator continuous-effect item with raw JSON carrying duplicate `effectKind`, whitespace-wrapped `sourceCardNo` beside canonical `sourceCardNo`, duplicate `sourcePath`, whitespace-wrapped `layerEngineStatus` beside canonical `layerEngineStatus`, and an empty property name.
- Recovery validation emits all spectator continuous-effect metadata property-name diagnostics and does not emit a continuous-effect count mismatch.

## Validation

- Focused test: `1/1`.
- Changed-class filter `MatchRecoveryTests`: `1809/1809`.
- Adjacent recovery filter `MatchRecovery`: `1814/1814`.
- Backend full: `8097/8097`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests`, and `src`: passed.

## Commits

- Code commit: `31d28f07` (`test: cover spectator continuous effect metadata property names`).
- Docs checkpoint: pending at document creation time.

## Remaining Risk

- This narrows spectator replay timing continuous-effect metadata property-name validation only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.
- Project remains **NOT READY**.
