# Stage 4D-17VM Recovery Timing Resolution-History Battle Attacking Player Required Audit

Date: 2026-06-03

Owner: A_MAIN

Status: Accepted. Project remains **NOT READY**.

## Scope

- Runtime surface: `MatchRecoveryValidator` validation for recovered snapshot, authoritative state and spectator replay-frame timing `battleResolutions[]` payloads.
- Closure target: server P1-004 recovery/replay determinism for the battle-resolution attacking-player required field.
- Out of scope: command execution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

## Runtime Evidence

- `AppendBattleResolutionEvents` receives `attackingPlayerId` as a non-null command-intent value and writes it into each `BattleResolutionState`.
- `BuildBattleResolutionSnapshotView` serializes `attackingPlayerId` for player and spectator snapshot views.
- Defending and winner player fields can be absent depending on battle outcome, but the attacking side is always the battle initiator and is required for deterministic result attribution.

## Validation Added

- Recovered snapshot and spectator replay-frame `battleResolutions[]` payload values now use `ValidateSnapshotPayloadRequiredStringValue` for `attackingPlayerId`.
- Authoritative state battle-resolution player validation now uses `ValidateAuthoritativeStateRequiredObjectPlayer` for `BattleResolutionState.AttackingPlayerId`.
- Existing defending/winner player validation remains optional, with 17VL compatibility checks still enforcing winner semantics for `CLOSED` and `NO_RESULT` results.

## Tests

- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryBattleAttackingPlayerRequiredDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryBattleAttackingPlayerRequiredDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryBattleAttackingPlayerRequiredDrift`

## Validation

- Focused new attacking-player-required tests: `3/3`.
- Focused `ResolutionHistory` filter: `57/57`.
- Focused recovery filter: `720/720`.
- Adjacent recovery/opening/store-smoke broad filter: `1319/1319`.
- Backend full: `6665/6665`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, matrix JSON parse and touched-file scoped format verify passed.
- Full `dotnet format --verify-no-changes --no-restore` remains blocked by unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.
