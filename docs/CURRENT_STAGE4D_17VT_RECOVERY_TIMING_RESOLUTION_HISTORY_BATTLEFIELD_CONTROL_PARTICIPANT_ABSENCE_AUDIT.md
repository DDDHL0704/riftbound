# Stage 4D-17VT Recovery Timing Resolution-History Battlefield Control Participant Absence Audit

Date: 2026-06-03

Owner: A_MAIN

Status: Accepted. Project remains **NOT READY**.

## Scope

- Runtime surface: `MatchRecoveryValidator` validation for recovered snapshot, authoritative state and spectator replay-frame timing `battlefieldResolutions[]` payloads.
- Closure target: server P1-004 recovery/replay determinism for battlefield control-resolution source/participant absence.
- Out of scope: command execution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

## Runtime Evidence

- `ResolveBattlefieldControlAfterCombat` emits `BATTLEFIELD_CONTROL_RESOLVED` with controller fields, previous controller fields, resolution outcome and occupant controller ids, but no combat source object or defender/defeated participant object lists.
- `AppendBattlefieldResolutionEvents` derives `sourceObjectId` only from event payload `sourceObjectId`, and derives `participantObjectIds[]` only from `defenderObjectIds[]`, `defeatedObjectIds[]` and that source object.
- Therefore current `CONTROL_RESOLVED` battlefield-resolution history cannot legally carry `sourceObjectId` or non-empty `participantObjectIds[]`; those fields are combat-derived `HELD` / `CONQUERED` data.

## Validation Added

- Recovered snapshot `battlefieldResolutions[]` payload validation now rejects `CONTROL_RESOLVED` entries with present `sourceObjectId`.
- Recovered snapshot `battlefieldResolutions[]` payload validation now rejects `CONTROL_RESOLVED` entries with non-empty `participantObjectIds[]`.
- Authoritative state battlefield-resolution metadata and spectator replay-frame payload validation apply the same source/participant absence checks while preserving separate player/controller, previous-controller, player-reference, object-reference, kind/reason, related-event-kind and authoritative parity diagnostics.

## Tests

- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryBattlefieldControlParticipantAbsenceDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryBattlefieldControlParticipantAbsenceDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryBattlefieldControlParticipantAbsenceDrift`

## Validation

- Focused new battlefield control participant-absence tests: `3/3`.
- Focused `ResolutionHistory` filter: `78/78`.
- Focused recovery filter: `741/741`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1321/1321`.
- Backend full: `6686/6686`.
- Touched-file scoped format verify passed. `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.
- Full `dotnet format --verify-no-changes --no-restore` exits 2 only on unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.
