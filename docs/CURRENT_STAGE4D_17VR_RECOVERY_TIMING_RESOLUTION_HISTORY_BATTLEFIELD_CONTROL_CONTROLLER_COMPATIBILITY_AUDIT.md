# Stage 4D-17VR Recovery Timing Resolution-History Battlefield Control Controller Compatibility Audit

Date: 2026-06-03

Owner: A_MAIN

Status: Accepted. Project remains **NOT READY**.

## Scope

- Runtime surface: `MatchRecoveryValidator` validation for recovered snapshot, authoritative state and spectator replay-frame timing `battlefieldResolutions[]` payloads.
- Closure target: server P1-004 recovery/replay determinism for battlefield control-resolution player/controller scalar compatibility.
- Out of scope: command execution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

## Runtime Evidence

- `ResolveBattlefieldControlAfterCombat` emits `BATTLEFIELD_CONTROL_RESOLVED` events with `resolution` set to `UNCONTROLLED`, `CONTROL_CHANGED` or `CONTROL_CONFIRMED`.
- The same runtime path sets both `playerId` and `controllerId` from `nextControllerId`, so controlled outcomes carry matching non-null values.
- When the battlefield becomes uncontrolled, `nextControllerId` is null, so both `playerId` and `controllerId` are absent/null while `previousControllerId` can still record the prior controller.
- `AppendBattlefieldResolutionEvents` persists `playerId`, `previousControllerId`, `controllerId` and `reason` into battlefield-resolution history.

## Validation Added

- Recovered snapshot `battlefieldResolutions[]` payload validation now rejects `CONTROL_RESOLVED` / `UNCONTROLLED` entries that carry `playerId` or `controllerId`.
- Recovered snapshot `battlefieldResolutions[]` payload validation now rejects `CONTROL_RESOLVED` / `CONTROL_CHANGED` or `CONTROL_CONFIRMED` entries that omit player/controller identity or carry different `playerId` and `controllerId` values.
- Authoritative state battlefield-resolution metadata and spectator replay-frame payload validation apply the same control controller-compatibility checks while preserving separate player-reference, object-reference, participant/source, kind/reason, related-event-kind and authoritative parity diagnostics.
- Legacy/event-kind reason `BATTLEFIELD_CONTROL_RESOLVED` remains outside this outcome-specific scalar check.

## Tests

- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryBattlefieldControlControllerCompatibilityDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryBattlefieldControlControllerCompatibilityDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryBattlefieldControlControllerCompatibilityDrift`

## Validation

- Focused new battlefield control controller compatibility tests: `3/3`.
- Focused `ResolutionHistory` filter: `72/72`.
- Focused recovery filter: `735/735`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1315/1315`.
- Backend full: `6680/6680`.
- `git diff --check`, anchored conflict-marker scan, matrix JSON parse and touched-file scoped format verify passed. Full `dotnet format --verify-no-changes --no-restore` exits 2 only on unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.
