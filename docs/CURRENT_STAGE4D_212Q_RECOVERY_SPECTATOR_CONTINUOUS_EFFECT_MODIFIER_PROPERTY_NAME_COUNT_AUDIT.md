# Stage 4D-212Q Recovery Spectator Continuous Effect Modifier Property Name Count Audit

Timestamp: 2026-06-12 20:50 CST

Owner: A_MAIN

## Scope

- Covered spectator replay timing keyed modifier scalar/order item property-name validation for `continuousEffects[]` when a continuous-effect count mismatch is also present.
- Added one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees created: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedModifierScalarPropertyNameWithCountMismatch` builds a spectator replay frame from authoritative continuous-effect state.
- The test replaces the first spectator continuous-effect item with raw JSON carrying duplicate `requestedPowerDelta`, whitespace-wrapped `appliedPowerDelta` beside canonical `appliedPowerDelta`, duplicate `minimumPower`, whitespace-wrapped `resultingPower` beside canonical `resultingPower`, duplicate `appliedOrder`, whitespace-wrapped `sourceOrder` beside canonical `sourceOrder`, and an empty property name.
- The test appends `effect-extra`, forcing spectator continuous-effect count drift from `1` to `2`.
- Recovery validation emits all spectator continuous-effect modifier/order property-name diagnostics, reports `effect-extra`, and emits the continuous-effect count mismatch.

## Validation

- Focused test: `1/1`.
- Changed-class filter `MatchRecoveryTests`: `1814/1814`.
- Adjacent recovery filter `MatchRecovery`: `1819/1819`.
- Backend full: `8102/8102`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests`, and `src`: passed.

## Commits

- Code commit: `85801eb3` (`test: cover spectator continuous effect modifier property names with count drift`).
- Docs checkpoint: pending at document creation time.

## Remaining Risk

- This narrows spectator replay timing continuous-effect modifier scalar/order property-name validation with count mismatch only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.
- Project remains **NOT READY**.
