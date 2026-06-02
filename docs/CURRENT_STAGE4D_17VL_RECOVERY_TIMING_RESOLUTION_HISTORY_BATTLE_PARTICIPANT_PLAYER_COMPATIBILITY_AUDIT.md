# Stage 4D-17VL Recovery Timing Resolution-History Battle Participant Player Compatibility Audit

Date: 2026-06-03

Owner: A_MAIN

Status: Accepted. Project remains **NOT READY**.

## Scope

- Runtime surface: `MatchRecoveryValidator` validation for recovered snapshot, authoritative state and spectator replay-frame timing `battleResolutions[]` payloads.
- Closure target: server P1-004 recovery/replay determinism for battle-resolution participant player compatibility.
- Out of scope: command execution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

## Runtime Evidence

- Runtime battle declaration rejects same-object attacker/defender overlap and derives battle player roles from command intent and defender locations.
- `TryResolveBattleWinnerPlayerId` can only emit the attacking player or the single defending player as the battle winner.
- `AppendBattleResolutionEvents` records the resolved battle winner into `BattleResolutionState.WinnerPlayerId`; when a `BATTLE_NO_RESULT` event is present, the resolution kind is `NO_RESULT` and the runtime does not carry a winner.

## Validation Added

- `ValidateBattleResolutionPlayerRoleCompatibility` now rejects:
  - `attackingPlayerId` also appearing as `defendingPlayerId`.
  - `CLOSED` battle resolutions with no winner.
  - `CLOSED` battle resolutions whose `winnerPlayerId` is neither the attacking nor defending player.
  - `NO_RESULT` battle resolutions carrying a non-empty `winnerPlayerId`.
- The helper is used for recovered snapshot payloads, authoritative state metadata and spectator replay-frame payloads after scalar/player-reference validation.

## Tests

- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryBattleParticipantPlayerCompatibilityDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryBattleParticipantPlayerCompatibilityDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryBattleParticipantPlayerCompatibilityDrift`

## Validation

- Focused new player-compatibility tests: `3/3`.
- Focused `ResolutionHistory` filter: `54/54`.
- Focused recovery filter: `716/716`.
- Adjacent recovery/opening/store-smoke broad filter: `1316/1316`.
- Backend full: `6662/6662`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, matrix JSON parse and touched-file scoped format verify passed.
- Full `dotnet format --verify-no-changes --no-restore` remains blocked by unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.
