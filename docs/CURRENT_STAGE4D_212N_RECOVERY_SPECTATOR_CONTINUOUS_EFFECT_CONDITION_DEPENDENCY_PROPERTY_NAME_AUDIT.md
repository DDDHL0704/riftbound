# Stage 4D-212N Recovery Spectator Continuous Effect Condition Dependency Property Name Audit

Timestamp: 2026-06-12 20:27 CST

Owner: A_MAIN

## Scope

- Covered spectator replay timing keyed condition/lifecycle and dependency-list item property-name validation for `continuousEffects[]`.
- Added one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees created: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedConditionDependencyPropertyNameWithoutCountMismatch` builds a spectator replay frame from authoritative continuous-effect state.
- The test replaces the single spectator continuous-effect item with raw JSON carrying duplicate `condition`, whitespace-wrapped `lifecycle` beside canonical `lifecycle`, duplicate `participantObjectIds`, whitespace-wrapped `sourceDependencyObjectIds` beside canonical `sourceDependencyObjectIds`, duplicate `targetDependencyObjectIds`, whitespace-wrapped `participantDependencyObjectIds` beside canonical `participantDependencyObjectIds`, and an empty property name.
- Recovery validation emits all spectator continuous-effect condition/dependency property-name diagnostics and does not emit a continuous-effect count mismatch.

## Validation

- Focused test: `1/1`.
- Changed-class filter `MatchRecoveryTests`: `1811/1811`.
- Adjacent recovery filter `MatchRecovery`: `1816/1816`.
- Backend full: `8099/8099`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests`, and `src`: passed.

## Commits

- Code commit: `68aea302` (`test: cover spectator continuous effect condition property names`).
- Docs checkpoint: pending at document creation time.

## Remaining Risk

- This narrows spectator replay timing continuous-effect condition/lifecycle/dependency-list property-name validation only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.
- Project remains **NOT READY**.
