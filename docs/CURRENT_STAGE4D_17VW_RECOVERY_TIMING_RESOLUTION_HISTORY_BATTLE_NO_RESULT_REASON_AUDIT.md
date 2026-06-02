# Stage 4D-17VW Recovery Timing Resolution-History Battle No-Result Reason Audit

Date: 2026-06-03

Owner: A_MAIN

Status: Accepted. Project remains **NOT READY**.

## Scope

- Runtime surface: `MatchRecoveryValidator` validation for recovered snapshot, authoritative state and spectator replay-frame timing battle resolution-history entries.
- Closure target: server P1-004 recovery/replay determinism for `NO_RESULT` battle-resolution reason compatibility.
- Out of scope: command execution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

## Runtime Evidence

- `BuildBattleNoResultEvent` emits `BATTLE_NO_RESULT` with a concrete payload `reason`: `ALL_PARTICIPANTS_DESTROYED` when no battle participants survive, otherwise `BOTH_SIDES_RETAIN_UNITS`.
- `AppendBattleResolutionEvents` copies the `BATTLE_NO_RESULT` payload reason into retained `BattleResolutionState.Reason`.
- Legal no-result retained history still requires the `BATTLE_NO_RESULT` related event kind, and may also include cleanup events such as `BATTLE_CLOSED`; the invalid value is only fallback `reason = BATTLE_NO_RESULT`.

## Validation Added

- Recovered snapshot timing `battleResolutions[]` rejects `kind = NO_RESULT` with `reason = BATTLE_NO_RESULT`.
- Authoritative state `BattleResolutions` rejects the same fallback reason for `NO_RESULT` retained entries.
- Spectator replay-frame timing `battleResolutions[]` rejects the same drift before broader keyed authoritative value diagnostics continue.

## Tests

- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryBattleNoResultFallbackReasonDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryBattleNoResultFallbackReasonDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryBattleNoResultFallbackReasonDrift`

## Validation

- Focused new no-result fallback reason tests: `3/3`.
- Focused `ResolutionHistory` filter: `87/87`.
- Focused recovery filter: `750/750`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1330/1330`.
- Backend full: `6695/6695`.
- Touched-file scoped whitespace format passed. `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.
