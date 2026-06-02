# Stage 4D-17VY Recovery Timing Resolution-History Battle No-Result Survivor Reason Audit

Date: 2026-06-03

Owner: A_MAIN

Status: Accepted. Project remains **NOT READY**.

## Scope

- Runtime surface: `MatchRecoveryValidator` validation for recovered snapshot, authoritative state and spectator replay-frame timing battle resolution-history entries.
- Closure target: server P1-004 recovery/replay determinism for `NO_RESULT` battle-resolution survivor/reason compatibility.
- Out of scope: command execution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

## Runtime Evidence

- `BuildBattleNoResultEvent` computes surviving attacker and defender unit object lists through `SurvivingBattleUnitObjectIds`.
- Runtime emits `reason = ALL_PARTICIPANTS_DESTROYED` only when both surviving lists are empty.
- `TryResolveBattleWinnerPlayerId` returns a winner when exactly one side survives, so no-result `reason = BOTH_SIDES_RETAIN_UNITS` can only represent both sides still retaining at least one surviving unit.
- `AppendBattleResolutionEvents` copies the no-result event survivor lists and concrete reason into retained `BattleResolutionState`.

## Validation Added

- Recovered snapshot timing `battleResolutions[]` rejects `NO_RESULT` entries where `ALL_PARTICIPANTS_DESTROYED` carries any surviving attacker or defender object ids.
- Recovered snapshot timing `battleResolutions[]` rejects `NO_RESULT` entries where `BOTH_SIDES_RETAIN_UNITS` omits either surviving side.
- Authoritative state and spectator replay-frame timing battle-resolution history apply the same survivor/reason compatibility diagnostics.

## Tests

- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryBattleNoResultSurvivorReasonCompatibilityDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryBattleNoResultSurvivorReasonCompatibilityDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryBattleNoResultSurvivorReasonCompatibilityDrift`

## Validation

- Focused new no-result survivor/reason compatibility tests: `3/3`.
- Focused `ResolutionHistory` filter: `93/93`.
- Focused recovery filter: `756/756`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1336/1336`.
- Backend full: `6701/6701`.
- Touched-file scoped whitespace format passed. `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.
