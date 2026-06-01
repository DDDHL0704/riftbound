# Stage 4D-17SL Recovery Timing Battle Participant Controller Membership Audit

Date: 2026-06-02

Status: Accepted. Project remains **NOT READY**.

## Scope

A_MAIN tightened recovery frame validation for timing battle participant-controller membership:

- Recovered player-view snapshot `Timing["battle"]["participantControllerIds"]` keys now must be members of the enclosing `attackerObjectIds` / `defenderObjectIds` participant set.
- Recovered player-view snapshot battle participants now must each have a participant-controller entry when readable participant lists and controller maps are present.
- Spectator replay-frame timing battle payloads now run the same same-payload participant-controller membership checks before authoritative battle parity comparison.

This follows Stage 4D-17SG, 17SI and 17SK by covering the remaining battle participant-controller exactness boundary. Forged controller map keys that point at registry objects outside the battle, or missing controller entries for battle participants, now emit explicit same-payload diagnostics instead of relying only on later spectator battle parity drift.

## Runtime Changes

Changed `src/Riftbound.Engine/MatchRecovery.cs` only in recovery validation helpers:

- Added battle participant-controller membership validation from the enclosing battle attacker/defender participant object ids.
- Wired the check into recovered snapshot and spectator replay-frame battle validation.
- Reused existing battle participant set and object-string dictionary readers so raw snapshot JSON payloads and spectator typed dictionary payloads are both validated.
- Kept existing required field, list/map shape, object-reference registry, player-reference membership, damage-assignment membership and spectator authoritative parity checks intact.

No protocol, command resolution, frontend, official catalog, matrix JSON or `fullOfficial` behavior changed.

## Tests

Changed `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`:

- `RecoveryValidatorRejectsSnapshotTimingBattleParticipantControllerMembershipDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleParticipantControllerMembershipDrift`

These cover recovered snapshot and spectator replay-frame controller maps with one nonparticipant controller key and one missing participant controller entry.

## Validation

Passed:

- Focused participant-controller membership tests: `2/2`
- `Battle` filter: `683/683`
- `MatchRecoveryTests` filter: `598/598`
- Adjacent recovery/opening/store-smoke filter: `1198/1198`
- Full backend: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` `6544/6544`
- Mechanical: `git diff --check`
- Mechanical: anchored conflict-marker scan over `docs src tests`
- Mechanical: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining

This narrows P1-004 replay/recovery determinism and timing battle participant-controller membership only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
