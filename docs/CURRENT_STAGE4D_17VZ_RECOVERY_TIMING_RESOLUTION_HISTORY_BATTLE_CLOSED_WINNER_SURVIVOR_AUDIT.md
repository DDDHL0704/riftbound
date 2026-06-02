# Stage 4D-17VZ Recovery Timing Resolution-History Battle Closed Winner Survivor Audit

Date: 2026-06-03

Owner: A_MAIN

Status: Accepted. Project remains **NOT READY**.

## Scope

- Runtime surface: `MatchRecoveryValidator` validation for recovered snapshot, authoritative state and spectator replay-frame timing battle resolution-history entries.
- Closure target: server P1-004 recovery/replay determinism for `CLOSED` battle-resolution winner/survivor compatibility.
- Out of scope: command execution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

## Runtime Evidence

- `TryResolveBattleWinnerPlayerId` returns a battle winner only when exactly one battle side has surviving unit objects on field.
- When no winner is resolved, runtime emits `BATTLE_NO_RESULT` through `BuildBattleNoResultEvent`; both-side retained and all-destroyed outcomes remain no-result history instead of closed winner history.
- `AppendBattleResolutionEvents` records `kind = CLOSED` only when no no-result event is present, and derives surviving attacker/defender object lists from current non-destroyed field objects.
- Therefore a legal `CLOSED` retained battle resolution must have surviving object ids only on the winner's side and none on the losing side.

## Validation Added

- Recovered snapshot timing `battleResolutions[]` rejects `CLOSED` entries where the attacking player wins but `survivingAttackerObjectIds[]` is empty or `survivingDefenderObjectIds[]` is non-empty.
- Recovered snapshot timing `battleResolutions[]` rejects `CLOSED` entries where the defending player wins but `survivingDefenderObjectIds[]` is empty or `survivingAttackerObjectIds[]` is non-empty.
- Authoritative state and spectator replay-frame timing battle-resolution history apply the same winner/survivor compatibility diagnostics.

## Tests

- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryBattleClosedWinnerSurvivorCompatibilityDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryBattleClosedWinnerSurvivorCompatibilityDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryBattleClosedWinnerSurvivorCompatibilityDrift`

## Validation

- Focused new closed winner/survivor compatibility tests: `3/3`.
- Focused `ResolutionHistory` filter: `96/96`.
- Focused recovery filter: `759/759`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1339/1339`.
- Backend full: `6704/6704`.
- Touched-file scoped whitespace format passed. `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.
