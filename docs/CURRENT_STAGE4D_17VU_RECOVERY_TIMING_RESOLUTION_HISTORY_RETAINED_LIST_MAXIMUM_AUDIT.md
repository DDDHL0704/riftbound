# Stage 4D-17VU Recovery Timing Resolution-History Retained List Maximum Audit

Date: 2026-06-03

Owner: A_MAIN

Status: Accepted. Project remains **NOT READY**.

## Scope

- Runtime surface: `MatchRecoveryValidator` validation for recovered snapshot, authoritative state and spectator replay-frame timing resolution-history lists.
- Closure target: server P1-004 recovery/replay determinism for retained battlefield/battle resolution-history list length.
- Out of scope: command execution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

## Runtime Evidence

- `AppendBattlefieldResolutionEvents` prepends new battlefield-resolution history, groups by `ResolutionId`, and retains at most 12 entries via `Take(12)`.
- `AppendBattleResolutionEvents` applies the same retained-history cap for battle-resolution history via `Take(12)`.
- Therefore legal runtime state, recovered player snapshots and spectator replay frames cannot carry more than 12 `battlefieldResolutions[]` or more than 12 `battleResolutions[]` entries.

## Validation Added

- Recovered snapshot timing `battlefieldResolutions[]` and `battleResolutions[]` now reject lists with more than 12 entries.
- Authoritative state `BattlefieldResolutions` and `BattleResolutions` now reject lists with more than 12 entries.
- Spectator replay-frame timing `battlefieldResolutions[]` and `battleResolutions[]` now reject lists with more than 12 entries before broader count/key/value parity diagnostics continue.

## Tests

- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryRetainedListMaximumDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryRetainedListMaximumDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryRetainedListMaximumDrift`

## Validation

- Focused new retained-list maximum tests: `3/3`.
- Focused `ResolutionHistory` filter: `81/81`.
- Focused recovery filter: `744/744`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1324/1324`.
- Backend full: `6689/6689`.
- Touched-file scoped format verify passed. `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.
