# Stage 4D-17SN Recovery Timing Battlefield Task Player Reference Membership Audit

Date: 2026-06-02

Status: Accepted. Project remains **NOT READY**.

## Scope

A_MAIN tightened recovery frame validation for timing battlefield-task player references:

- Recovered player-view snapshot `Timing["battlefieldTasks"][]` `participantControllerIds` now must exist in snapshot `players`.
- Recovered player-view snapshot battlefield-task `actingPlayerId` now must exist in snapshot `players` when present and readable.
- Spectator replay-frame timing battlefield-task payloads now run the same same-payload player-reference checks against authoritative `Seats` before authoritative battlefield-task parity comparison.

This follows Stage 4D-17SF object-reference validation by covering the remaining battlefield-task player-reference boundary. Forged participant-controller and acting-player values now emit explicit same-payload diagnostics instead of relying only on later spectator parity drift.

## Runtime Changes

Changed `src/Riftbound.Engine/MatchRecovery.cs` only in recovery validation helpers:

- Added a reusable timing player-reference list membership validator.
- Wired battlefield-task `participantControllerIds` and `actingPlayerId` checks into recovered snapshot validation against snapshot players.
- Wired the same checks into spectator replay-frame validation against authoritative seats.
- Kept existing required field, list shape/value, object-reference registry, derived-id and spectator authoritative parity checks intact.

No protocol, command resolution, frontend, official catalog, matrix JSON or `fullOfficial` behavior changed.

## Tests

Changed `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`:

- `RecoveryValidatorRejectsSnapshotTimingBattlefieldTaskPlayerReferencesOutsideSnapshotPlayers`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskPlayerReferencesOutsideSeats`

These cover recovered snapshot battlefield-task participant-controller / acting-player drift and spectator replay-frame same-payload diagnostics under a battlefield-task count mismatch where authoritative parity is skipped.

## Validation

Passed:

- Focused battlefield-task player-reference tests: `2/2`
- `BattlefieldTask` filter: `28/28`
- `MatchRecoveryTests` filter: `602/602`
- Adjacent recovery/opening/store-smoke filter: `1202/1202`
- Full backend: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` `6548/6548`
- Mechanical: `git diff --check`
- Mechanical: anchored conflict-marker scan over `docs src tests`
- Mechanical: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining

This narrows P1-004 replay/recovery determinism and timing battlefield-task player-reference membership only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
