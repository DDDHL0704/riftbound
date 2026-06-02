# Stage 4D-17VQ Recovery Timing Resolution-History Battlefield Combat Player Required Audit

Date: 2026-06-03

Owner: A_MAIN

Status: Accepted. Project remains **NOT READY**.

## Scope

- Runtime surface: `MatchRecoveryValidator` validation for recovered snapshot, authoritative state and spectator replay-frame timing `battlefieldResolutions[]` payloads.
- Closure target: server P1-004 recovery/replay determinism for combat-derived battlefield-resolution player identity semantics.
- Out of scope: command execution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

## Runtime Evidence

- `AddBattlefieldHeldEventIfNeeded` emits `BATTLEFIELD_HELD` events with `playerId`, `battlefieldId`, `sourceObjectId` and `defenderObjectIds[]`.
- `BATTLEFIELD_CONQUERED` event creation emits `playerId`, `battlefieldId`, `sourceObjectId` and `defeatedObjectIds[]`.
- `AppendBattlefieldResolutionEvents` persists combat-derived `HELD` / `CONQUERED` event `playerId` values into battlefield-resolution history.
- `BATTLEFIELD_CONTROL_RESOLVED` events can carry no current controller/player when a battlefield is uncontrolled, so `CONTROL_RESOLVED` remains exempt from the combat player-required check.

## Validation Added

- Recovered snapshot `battlefieldResolutions[]` payload validation now rejects `HELD` / `CONQUERED` entries with missing, null or blank `playerId`.
- Authoritative state battlefield-resolution metadata validation now rejects `HELD` / `CONQUERED` entries with missing, null or blank `PlayerId`.
- Spectator replay-frame battlefield-resolution payload validation applies the same combat player-required check while preserving separate player-reference, object-reference, participant/source, kind/reason, related-event-kind and authoritative parity diagnostics.
- `CONTROL_RESOLVED` remains exempt from this combat player-required check.

## Tests

- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryBattlefieldCombatPlayerRequiredDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryBattlefieldCombatPlayerRequiredDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryBattlefieldCombatPlayerRequiredDrift`

## Validation

- Focused new battlefield combat player required tests: `3/3`.
- Focused `ResolutionHistory` filter: `69/69`.
- Focused recovery filter: `732/732`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1312/1312`.
- Backend full: `6677/6677`.
- `git diff --check`, anchored conflict-marker scan, matrix JSON parse and touched-file scoped format verify passed. Full `dotnet format --verify-no-changes --no-restore` exits 2 only on unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.
