# Stage 4D-17VO Recovery Timing Resolution-History Battle Participant Object List Maximum Audit

Date: 2026-06-03

Owner: A_MAIN

Status: Accepted. Project remains **NOT READY**.

## Scope

- Runtime surface: `MatchRecoveryValidator` validation for recovered snapshot, authoritative state and spectator replay-frame timing `battleResolutions[]` payloads.
- Closure target: server P1-004 recovery/replay determinism for battle-resolution attacker/defender participant object-list maximum semantics.
- Out of scope: command execution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

## Runtime Evidence

- `TryBuildMinimalDeclareBattle` normalizes battle declaration attacker and defender object ids and rejects declarations unless each side contains 1 to 2 objects.
- The same runtime path rejects duplicate attacker/defender object ids and attacker/defender role overlap before a battle can resolve.
- `AppendBattleResolutionEvents` persists the accepted attacker and defender object lists into `BattleResolutionState`, so legal runtime battle-resolution history cannot contain more than 2 participant objects on either side.

## Validation Added

- Recovered snapshot `battleResolutions[]` payload validation now rejects `attackerObjectIds[]` and `defenderObjectIds[]` lists with more than 2 entries after existing required-list shape and string-value validation.
- Spectator replay-frame `battleResolutions[]` payload validation applies the same max-count participant-list diagnostics before broad authoritative parity checks.
- Authoritative state battle-resolution metadata validation now rejects `BattleResolutionState.AttackerObjectIds` and `BattleResolutionState.DefenderObjectIds` with more than 2 entries when those lists are present.
- The 17VN non-empty diagnostics remain unchanged; survivor and destroyed result lists remain allowed to be empty and are still validated by separate 17VI/17VJ membership and disjointness checks.

## Tests

- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryBattleParticipantObjectListMaximumDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryBattleParticipantObjectListMaximumDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryBattleParticipantObjectListMaximumDrift`

## Validation

- Focused new participant-list-maximum tests: `3/3`.
- Focused `ResolutionHistory` filter: `63/63`.
- Focused recovery filter: `726/726`.
- Adjacent recovery/opening/store-smoke broad filter: `1325/1325`.
- Backend full: `6671/6671`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, matrix JSON parse and touched-file scoped format verify passed.
- Full `dotnet format --verify-no-changes --no-restore` remains blocked by unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.
