# Stage 4D-17VV Recovery Timing Resolution-History Retained Tick Order Audit

Date: 2026-06-03

Owner: A_MAIN

Status: Accepted. Project remains **NOT READY**.

## Scope

- Runtime surface: `MatchRecoveryValidator` validation for recovered snapshot, authoritative state and spectator replay-frame timing resolution-history lists.
- Closure target: server P1-004 recovery/replay determinism for retained battlefield/battle resolution-history newest-first tick order.
- Out of scope: command execution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

## Runtime Evidence

- `AppendBattlefieldResolutionEvents` prepends new battlefield-resolution history, groups by `ResolutionId`, and retains the first 12 entries.
- `AppendBattleResolutionEvents` prepends the latest battle resolution, groups by `ResolutionId`, and retains the first 12 entries.
- Therefore legal retained resolution history is newest-first by tick. Same-tick entries are legal, but later list entries cannot have a greater tick than earlier retained entries.

## Validation Added

- Recovered snapshot timing `battlefieldResolutions[]` and `battleResolutions[]` now reject retained tick-order drift when a later valid item has a greater tick than an earlier valid item.
- Authoritative state `BattlefieldResolutions` and `BattleResolutions` now reject the same newest-first drift while skipping ticks already invalid because they are negative or after the authoritative state tick.
- Spectator replay-frame timing `battlefieldResolutions[]` and `battleResolutions[]` now reject retained tick-order drift before broader count/key/value parity diagnostics continue.

## Tests

- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryRetainedTickOrderDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryRetainedTickOrderDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryRetainedTickOrderDrift`

## Validation

- Focused new retained tick-order tests: `3/3`.
- Focused `ResolutionHistory` filter: `84/84`.
- Focused recovery filter: `747/747`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1327/1327`.
- Backend full: `6692/6692`.
- Touched-file scoped whitespace format passed. `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.
