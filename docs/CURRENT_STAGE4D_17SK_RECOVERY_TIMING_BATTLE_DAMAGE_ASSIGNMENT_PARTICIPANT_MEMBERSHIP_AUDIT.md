# Stage 4D-17SK Recovery Timing Battle Damage Assignment Participant Membership Audit

Date: 2026-06-02

Status: Accepted. Project remains **NOT READY**.

## Scope

A_MAIN tightened recovery frame validation for timing battle damage-assignment participant membership:

- Recovered player-view snapshot `Timing["battle"]["damageAssignment"]` `damagePool` map keys, `legalTargets` map keys and target lists, `existingDamage` map keys, `lethalDamageThreshold` map keys and `requiredAssignments[]` source/legal target object ids now must be members of the enclosing `Timing["battle"]` attacker/defender participant lists when pending damage-assignment fields are present.
- Spectator replay-frame timing battle damage-assignment payloads now run the same participant membership checks against the enclosing battle participants before authoritative battle parity comparison.
- The check covers the case where an object id exists in the object registry and the nested damage-assignment identity matches the enclosing battle, but the object is not actually a participant in that battle.

This follows Stage 4D-17SG, 17SH, 17SI and 17SJ by covering the remaining near-field damage-assignment membership boundary inside the same `battle` / `damageAssignment` payload pair. Forged nonparticipant damage-assignment object ids now emit explicit same-payload diagnostics instead of relying only on later spectator battle parity drift.

## Runtime Changes

Changed `src/Riftbound.Engine/MatchRecovery.cs` only in recovery validation helpers:

- Added battle damage-assignment participant membership validation from the enclosing battle attacker/defender participant object ids.
- Wired the check into recovered snapshot and spectator replay-frame battle damage-assignment validation.
- Reused typed object-int and object-string-list dictionary readers so both raw snapshot JSON payloads and spectator typed dictionary payloads are validated.
- Kept existing required field, map/list shape, object-reference registry, player-reference membership, identity-consistency and spectator authoritative parity checks intact.

No protocol, command resolution, frontend, official catalog, matrix JSON or `fullOfficial` behavior changed.

## Tests

Changed `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`:

- `RecoveryValidatorRejectsSnapshotTimingBattleDamageAssignmentParticipantsOutsideBattle`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentParticipantsOutsideBattle`

These cover recovered snapshot and spectator replay-frame damage-assignment references that point at object-registry members outside the enclosing battle participants.

## Validation

Passed:

- Focused participant-membership tests: `2/2`
- `BattleDamageAssignment` filter: `68/68`
- `Battle` filter: `681/681`
- `MatchRecoveryTests` filter: `596/596`
- Adjacent recovery/opening/store-smoke filter: `1196/1196`
- Full backend: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` `6542/6542`
- Mechanical: `git diff --check`
- Mechanical: anchored conflict-marker scan over `docs src tests`
- Mechanical: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining

This narrows P1-004 replay/recovery determinism and timing battle damage-assignment participant membership only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
