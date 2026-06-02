# Stage 4D-17VP Recovery Timing Resolution-History Battlefield Combat Participant Source Audit

Date: 2026-06-03

Owner: A_MAIN

Status: Accepted. Project remains **NOT READY**.

## Scope

- Runtime surface: `MatchRecoveryValidator` validation for recovered snapshot, authoritative state and spectator replay-frame timing `battlefieldResolutions[]` payloads.
- Closure target: server P1-004 recovery/replay determinism for combat-derived battlefield-resolution participant/source semantics.
- Out of scope: command execution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

## Runtime Evidence

- `AppendBattlefieldResolutionEvents` records `HELD` battlefield resolutions from `BATTLEFIELD_HELD` events and builds `participantObjectIds[]` from `defenderObjectIds[]` plus `sourceObjectId`.
- The same routine records `CONQUERED` battlefield resolutions from `BATTLEFIELD_CONQUERED` events and builds `participantObjectIds[]` from `defeatedObjectIds[]` plus `sourceObjectId`.
- `CONTROL_RESOLVED` battlefield resolutions are generated from `BATTLEFIELD_CONTROL_RESOLVED` events whose payload can legitimately omit defender/defeated/source object fields, so those entries remain allowed to carry no participant/source object.

## Validation Added

- Recovered snapshot `battlefieldResolutions[]` payload validation now rejects `HELD` / `CONQUERED` entries with missing `sourceObjectId`.
- Recovered snapshot `battlefieldResolutions[]` payload validation now rejects `HELD` / `CONQUERED` entries with empty `participantObjectIds[]`.
- Recovered snapshot `battlefieldResolutions[]` payload validation now rejects `HELD` / `CONQUERED` entries whose non-empty source object id is absent from `participantObjectIds[]`.
- Spectator replay-frame and authoritative state battlefield-resolution metadata validation apply the same combat-derived participant/source checks while preserving separate list shape/value, object-registry, redaction, player-reference and authoritative parity diagnostics.
- `CONTROL_RESOLVED` remains exempt from these participant/source checks.

## Tests

- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryBattlefieldCombatParticipantAvailabilityDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryBattlefieldCombatParticipantAvailabilityDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryBattlefieldCombatParticipantAvailabilityDrift`

## Validation

- Focused new battlefield combat participant availability tests: `3/3`.
- Focused `ResolutionHistory` filter: `66/66`.
- Focused recovery filter: `729/729`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1309/1309`.
- Backend full: `6674/6674`.
- `git diff --check`, anchored conflict-marker scan, matrix JSON parse and touched-file scoped format verify passed. Full `dotnet format --verify-no-changes --no-restore` exits 2 only on unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.
