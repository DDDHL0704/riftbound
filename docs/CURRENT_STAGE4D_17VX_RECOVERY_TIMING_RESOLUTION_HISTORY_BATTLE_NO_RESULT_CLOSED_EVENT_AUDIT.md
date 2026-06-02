# Stage 4D-17VX Recovery Timing Resolution-History Battle No-Result Closed Event Audit

Date: 2026-06-03

Owner: A_MAIN

Status: Accepted. Project remains **NOT READY**.

## Scope

- Runtime surface: `MatchRecoveryValidator` validation for recovered snapshot, authoritative state and spectator replay-frame timing battle resolution-history entries.
- Closure target: server P1-004 recovery/replay determinism for `NO_RESULT` battle-resolution related event compatibility.
- Out of scope: command execution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

## Runtime Evidence

- Runtime `BATTLE_NO_RESULT` resolution paths close the battle through cleanup before retained history is appended.
- `CloseResolvedBattle` emits `BATTLE_CLOSED` cleanup event data, and `AppendBattleResolutionEvents` copies related combat event kinds into retained `BattleResolutionState.RelatedEventKinds`.
- Legal no-result retained history therefore includes both `BATTLE_NO_RESULT` and `BATTLE_CLOSED` related event kinds while keeping the concrete no-result reason from Stage 4D-17VW.

## Validation Added

- Recovered snapshot timing `battleResolutions[]` rejects `kind = NO_RESULT` entries missing the cleanup `BATTLE_CLOSED` related event kind.
- Authoritative state `BattleResolutions` rejects the same missing cleanup event for `NO_RESULT` retained entries.
- Spectator replay-frame timing `battleResolutions[]` rejects the same drift while preserving existing keyed authoritative parity diagnostics.

## Tests

- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryBattleNoResultMissingClosedEventDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryBattleNoResultMissingClosedEventDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryBattleNoResultMissingClosedEventDrift`

## Validation

- Focused new no-result missing-closed-event tests: `3/3`.
- Focused `ResolutionHistory` filter: `90/90`.
- Focused recovery filter: `753/753`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1333/1333`.
- Backend full: `6698/6698`.
- Touched-file scoped whitespace format passed. `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.
