# Stage 4D-17VS Recovery Timing Resolution-History Battlefield Control Previous Controller Compatibility Audit

Date: 2026-06-03

Owner: A_MAIN

Status: Accepted. Project remains **NOT READY**.

## Scope

- Runtime surface: `MatchRecoveryValidator` validation for recovered snapshot, authoritative state and spectator replay-frame timing `battlefieldResolutions[]` payloads.
- Closure target: server P1-004 recovery/replay determinism for battlefield control-resolution previous-controller compatibility.
- Out of scope: command execution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

## Runtime Evidence

- `ResolveBattlefieldControlAfterCombat` captures `previousControllerId` from the battlefield state before computing `nextControllerId`.
- The runtime emits `CONTROL_CONFIRMED` only when `nextControllerId` is non-null and unchanged, so `previousControllerId`, `controllerId` and `playerId` all describe the same controller.
- The runtime emits `CONTROL_CHANGED` when `nextControllerId` is non-null and differs from the previous controller; a missing previous controller remains legal for an uncontrolled-to-controlled transition.
- `UNCONTROLLED` can still record a previous controller while carrying no current controller/player identity; the current-controller absence rule was closed in 17VR.
- `AppendBattlefieldResolutionEvents` persists `previousControllerId`, `controllerId`, `playerId` and `reason` into battlefield-resolution history.

## Validation Added

- Recovered snapshot `battlefieldResolutions[]` payload validation now rejects `CONTROL_RESOLVED` / `CONTROL_CONFIRMED` entries that omit `previousControllerId`.
- Recovered snapshot `battlefieldResolutions[]` payload validation now rejects `CONTROL_RESOLVED` / `CONTROL_CONFIRMED` entries whose `previousControllerId` differs from `controllerId`.
- Recovered snapshot `battlefieldResolutions[]` payload validation now rejects `CONTROL_RESOLVED` / `CONTROL_CHANGED` entries whose present `previousControllerId` equals `controllerId`.
- Authoritative state battlefield-resolution metadata and spectator replay-frame payload validation apply the same previous-controller compatibility checks while preserving separate player/controller, player-reference, object-reference, participant/source, kind/reason, related-event-kind and authoritative parity diagnostics.
- Legacy/event-kind reason `BATTLEFIELD_CONTROL_RESOLVED` remains outside this outcome-specific previous-controller check.

## Tests

- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryBattlefieldControlPreviousControllerCompatibilityDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryBattlefieldControlPreviousControllerCompatibilityDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryBattlefieldControlPreviousControllerCompatibilityDrift`

## Validation

- Focused new battlefield control previous-controller compatibility tests: `3/3`.
- Focused `ResolutionHistory` filter: `75/75`.
- Focused recovery filter: `738/738`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1318/1318`.
- Backend full: `6683/6683`.
- Touched-file scoped format verify passed. `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.
- Full `dotnet format --verify-no-changes --no-restore` exits 2 only on unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.
