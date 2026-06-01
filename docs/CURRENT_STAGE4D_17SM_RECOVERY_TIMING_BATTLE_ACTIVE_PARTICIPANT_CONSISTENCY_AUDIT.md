# Stage 4D-17SM Recovery Timing Battle Active Participant Consistency Audit

Date: 2026-06-02

Status: Accepted. Project remains **NOT READY**.

## Scope

A_MAIN tightened recovery frame validation for timing battle active-state consistency:

- Recovered player-view snapshot `Timing["battle"]["isActive"]` now must match whether readable `attackerObjectIds` / `defenderObjectIds` participants are present.
- Recovered player-view snapshot battle payloads now reject `isActive=true` with no readable battle participants and `isActive=false` with readable battle participants.
- Spectator replay-frame timing battle payloads now run the same same-payload active-participant consistency checks before authoritative battle parity comparison.

This follows Stage 4D-17SG through 17SL by covering the remaining battle active-state boundary implied by the runtime battle-state builder. Forged active flags now emit explicit same-payload diagnostics instead of relying only on later spectator battle parity drift.

## Runtime Changes

Changed `src/Riftbound.Engine/MatchRecovery.cs` only in recovery validation helpers:

- Added battle active-participant consistency validation from the enclosing battle attacker/defender participant object ids.
- Wired the check into recovered snapshot and spectator replay-frame battle validation.
- Reused existing battle participant set and boolean readers so raw snapshot JSON payloads and spectator typed dictionary payloads are both validated.
- Kept existing required field, list/map shape, object-reference registry, player-reference membership, participant-controller membership, damage-assignment membership and spectator authoritative parity checks intact.

No protocol, command resolution, frontend, official catalog, matrix JSON or `fullOfficial` behavior changed.

## Tests

Changed `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`:

- `RecoveryValidatorRejectsSnapshotTimingBattleActiveParticipantConsistencyDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleActiveParticipantConsistencyDrift`

These cover recovered snapshot active flags with no battle participants and spectator replay-frame active flags that contradict present battle participants.

## Validation

Passed:

- Focused active-participant consistency tests: `2/2`
- `Battle` filter: `685/685`
- `MatchRecoveryTests` filter: `600/600`
- Adjacent recovery/opening/store-smoke filter: `1200/1200`
- Full backend: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` `6546/6546`
- Mechanical: `git diff --check`
- Mechanical: anchored conflict-marker scan over `docs src tests`
- Mechanical: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining

This narrows P1-004 replay/recovery determinism and timing battle active-participant consistency only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
