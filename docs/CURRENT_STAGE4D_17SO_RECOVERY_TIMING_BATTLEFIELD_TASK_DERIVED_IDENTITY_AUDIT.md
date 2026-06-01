# Stage 4D-17SO Recovery Timing Battlefield Task Derived Identity Audit

Date: 2026-06-02

Status: Accepted. Project remains **NOT READY**.

## Scope

A_MAIN tightened recovery frame validation for timing battlefield-task derived identities:

- Recovered player-view snapshot `Timing["battlefieldTasks"][]` `START_SPELL_DUEL` tasks now require a readable `spellDuelId` equal to `BattleLifecycleIds.SpellDuelIdForBattlefield(battlefieldObjectId)`.
- Recovered player-view snapshot `START_BATTLE` tasks now require a readable `battleId` equal to `BattleLifecycleIds.BattleIdForBattlefield(battlefieldObjectId)`.
- Spectator replay-frame timing battlefield-task payloads now run the same same-payload derived-identity checks before authoritative battlefield-task parity comparison.

This follows Stage 4D-17SN player-reference membership by covering the remaining battlefield-task derived-id boundary. Forged or missing `spellDuelId` / `battleId` values now emit explicit same-payload diagnostics, including when spectator count mismatch skips authoritative parity.

## Runtime Changes

Changed `src/Riftbound.Engine/MatchRecovery.cs` only in recovery validation helpers:

- Captured battlefield-task `kind`, `battlefieldObjectId`, `spellDuelId` and `battleId` scalar validation results.
- Added shared battlefield-task derived-identity validation for recovered snapshot and spectator replay-frame payloads.
- Preserved existing required field, scalar/list shape/value, object-reference registry, player-reference membership and spectator authoritative parity checks.

No protocol, command resolution, frontend, official catalog, matrix JSON or `fullOfficial` behavior changed.

## Tests

Changed `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`:

- `RecoveryValidatorRejectsSnapshotTimingBattlefieldTaskDerivedIdentityDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskDerivedIdentityWithCountMismatch`

These cover recovered snapshot derived-id drift and missing derived ids, plus spectator replay-frame same-payload diagnostics under a battlefield-task count mismatch where authoritative parity is skipped.

## Validation

Passed:

- Focused battlefield-task derived-identity tests: `2/2`
- `BattlefieldTask` filter: `30/30`
- `MatchRecoveryTests` filter: `604/604`
- Adjacent recovery/opening/store-smoke filter: `1204/1204`
- Full backend: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` `6550/6550`
- Mechanical: `git diff --check`
- Mechanical: anchored conflict-marker scan over `docs src tests`
- Mechanical: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining

This narrows P1-004 replay/recovery determinism and timing battlefield-task derived-identity enforcement only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
