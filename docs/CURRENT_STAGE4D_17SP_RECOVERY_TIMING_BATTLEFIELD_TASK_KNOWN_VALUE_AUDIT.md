# Stage 4D-17SP Recovery Timing Battlefield Task Known Value Audit

Date: 2026-06-02

Status: Accepted. Project remains **NOT READY**.

## Scope

A_MAIN tightened recovery frame validation for timing battlefield-task known values:

- Recovered player-view snapshot `Timing["battlefieldTasks"][]` `kind` now rejects unknown task kinds outside `START_SPELL_DUEL` and `START_BATTLE`.
- Recovered player-view snapshot battlefield-task `status` now rejects unknown statuses outside `PENDING`, `ACTIVE`, `COMPLETED` and `WAITING_FOR_SPELL_DUEL`.
- Recovered player-view snapshot battlefield-task `reason` now rejects unknown reasons outside `BATTLEFIELD_CONTESTED` and `SPELL_DUEL_AFTER_BATTLEFIELD_CONTEST`.
- Spectator replay-frame timing battlefield-task payloads now run the same same-payload known-value checks before authoritative battlefield-task parity comparison.

This follows Stage 4D-17SO derived-identity validation by covering the remaining battlefield-task scalar value boundary. Forged `kind`, `status` or `reason` values now emit explicit same-payload diagnostics, including when spectator count mismatch skips authoritative parity.

## Runtime Changes

Changed `src/Riftbound.Engine/MatchRecovery.cs` only in recovery validation helpers:

- Added battlefield-task known-value predicates for kind, status and reason.
- Wired those predicates into recovered snapshot battlefield-task scalar validation.
- Wired the same predicates into spectator replay-frame battlefield-task scalar validation.
- Preserved existing required field, scalar/list shape/value, object-reference registry, player-reference membership, derived-identity and spectator authoritative parity checks.

No protocol, command resolution, frontend, official catalog, matrix JSON or `fullOfficial` behavior changed.

## Tests

Changed `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`:

- `RecoveryValidatorRejectsSnapshotTimingBattlefieldTaskKnownValueDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskKnownValuesWithCountMismatch`

These cover recovered snapshot unknown battlefield-task scalar values and spectator replay-frame same-payload diagnostics under a battlefield-task count mismatch where authoritative parity is skipped.

## Validation

Passed:

- Focused battlefield-task known-value tests: `2/2`
- `BattlefieldTask` filter: `32/32`
- `MatchRecoveryTests` filter: `606/606`
- Adjacent recovery/opening/store-smoke filter: `1206/1206`
- Full backend: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` `6552/6552`
- Mechanical: `git diff --check`
- Mechanical: anchored conflict-marker scan over `docs src tests`
- Mechanical: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining

This narrows P1-004 replay/recovery determinism and timing battlefield-task known-value enforcement only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
