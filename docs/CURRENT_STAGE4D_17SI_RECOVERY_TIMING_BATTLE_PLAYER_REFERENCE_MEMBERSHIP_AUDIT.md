# Stage 4D-17SI Recovery Timing Battle Player Reference Membership Audit

Date: 2026-06-02

Status: Accepted. Project remains **NOT READY**.

## Scope

A_MAIN tightened recovery frame validation for timing battle player references:

- Recovered player-view snapshot `Timing["battle"]["participantControllerIds"]` map values now must reference player ids present in snapshot `players`.
- Recovered player-view snapshot `Timing["battle"]["damageAssignment"]["assigningPlayerId"]` now must reference a player id present in snapshot `players` when damage-assignment pending fields are present.
- Spectator replay-frame `Timing["battle"]["participantControllerIds"]` map values now must reference player ids present in authoritative `Seats`.
- Spectator replay-frame `Timing["battle"]["damageAssignment"]["assigningPlayerId"]` now must reference a player id present in authoritative `Seats`.

This follows Stage 4D-17SG and 17SH object-reference registry checks by covering the player-id side of the same battle and battle damage-assignment payloads. Same-payload diagnostics run before and independently of spectator authoritative battle parity comparison.

## Runtime Changes

Changed `src/Riftbound.Engine/MatchRecovery.cs` only in recovery validation helpers:

- Added timing player-reference helpers for optional scalar player ids and string-map values.
- Wired battle participant-controller value validation to snapshot players / spectator seats.
- Wired battle damage-assignment assigning-player validation to snapshot players / spectator seats.

No protocol, command resolution, frontend, official catalog, matrix JSON or `fullOfficial` behavior changed.

## Tests

Changed `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`:

- `RecoveryValidatorRejectsSnapshotTimingBattlePlayerReferencesOutsideSnapshotPlayers`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattlePlayerReferencesOutsideSeats`

These cover missing participant-controller player ids and missing battle damage-assignment assigning player ids on recovered snapshot and spectator replay-frame payloads.

## Validation

Passed:

- Focused player-reference tests: `2/2`
- `BattleDamageAssignment` filter: `64/64`
- `Battle` filter: `677/677`
- `MatchRecoveryTests` filter: `592/592`
- Adjacent recovery/opening/store-smoke filter: `1192/1192`
- Full backend: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` `6538/6538`
- Mechanical: `git diff --check`
- Mechanical: anchored conflict-marker scan over `docs src tests`
- Mechanical: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining

This narrows P1-004 replay/recovery determinism and timing battle player-reference membership enforcement only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
