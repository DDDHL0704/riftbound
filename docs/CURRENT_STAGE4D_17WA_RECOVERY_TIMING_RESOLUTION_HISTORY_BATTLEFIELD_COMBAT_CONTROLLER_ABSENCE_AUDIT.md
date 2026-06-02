# Stage 4D-17WA Recovery Timing Resolution-History Battlefield Combat Controller Absence Audit

Date: 2026-06-03

Owner: A_MAIN

Status: Accepted. Project remains **NOT READY**.

## Scope

- Runtime surface: `MatchRecoveryValidator` validation for recovered snapshot, authoritative state and spectator replay-frame timing battlefield resolution-history entries.
- Closure target: server P1-004 recovery/replay determinism for combat-derived battlefield resolution controller-scalar absence.
- Out of scope: command execution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

## Runtime Evidence

- Runtime `BATTLEFIELD_HELD` events carry battlefield combat result fields such as player, battlefield, source object and defender object ids, but do not carry `previousControllerId` or `controllerId`.
- Runtime `BATTLEFIELD_CONQUERED` events carry battlefield combat result fields such as player, battlefield, source object and defeated object ids, but do not carry `previousControllerId` or `controllerId`.
- Runtime `BATTLEFIELD_CONTROL_RESOLVED` events record battlefield controller transitions separately, including `previousControllerId` and `controllerId`.
- `AppendBattlefieldResolutionEvents` persists controller scalars only from event payloads. Therefore legal combat-derived `HELD` and `CONQUERED` retained battlefield-resolution history cannot contain controller transition scalars.

## Validation Added

- Recovered snapshot timing `battlefieldResolutions[]` rejects combat-derived `HELD` / `CONQUERED` entries with non-blank `previousControllerId`.
- Recovered snapshot timing `battlefieldResolutions[]` rejects combat-derived `HELD` / `CONQUERED` entries with non-blank `controllerId`.
- Authoritative state and spectator replay-frame timing battlefield-resolution history apply the same combat controller-scalar absence diagnostics.

## Tests

- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryBattlefieldCombatControllerAbsenceDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryBattlefieldCombatControllerAbsenceDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryBattlefieldCombatControllerAbsenceDrift`

## Validation

- Focused new battlefield combat controller-absence tests: `3/3`.
- Focused `ResolutionHistory` filter: `99/99`.
- Focused recovery filter: `762/762`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1342/1342`.
- Backend full: `6707/6707`.
- Touched-file scoped whitespace format passed. `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.
