# Stage 4D-17BP Recovery Action-Log Replay Event-Kind Validation Evidence

2026-05-24 Stage 4D-17BP evidence bundle for recovery action-log replay event-kind validation.

## Runtime/Test Delta

- `src/Riftbound.Engine/MatchRecovery.cs`
  - `ValidateRecoveryFrameAsync` now passes `MatchRecoveryFrame.Events` into `VerifyFinalStateAsync`.
  - `VerifyFinalStateAsync` validates replayed event kinds against recovered event kinds for each recovered command span.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added `ActionLogReplayerReportsRecoveredEventKindMismatch`.
  - Added `ToRecoveredEvents` test helper and updated registry recovery audit tests to provide recovered events.

## Validation Commands

- Focused recovery tests:
  - `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"`
  - Result: passed `94/94`.
- Adjacent recovery/opening regression:
  - `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecovery|FullyQualifiedName~PostgresMatchRecoveryStoreSmoke|FullyQualifiedName~OfficialOpening"`
  - Result: passed `675/675`.
- Backend full:
  - `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`
  - Result: passed `6040/6040`.

## Mechanical Checks

Passed before commit:
- `git diff --check`
- `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Scope Guard

Runtime changed: yes, recovery action-log replay validation only.
Protocol/frontend/matrix/official catalog changed: no.
`tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` changed: no.
`riftbound-dotnet.sln` remains untracked and untouched.

Project remains **NOT READY**.
