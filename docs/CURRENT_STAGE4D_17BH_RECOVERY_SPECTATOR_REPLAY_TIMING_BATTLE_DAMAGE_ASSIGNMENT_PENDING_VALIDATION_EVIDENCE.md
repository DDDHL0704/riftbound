# Stage 4D-17BH Recovery Spectator Replay Timing Battle Damage-Assignment Pending Validation Evidence

2026-05-24 Stage 4D-17BH evidence bundle for recovery spectator replay timing battle damage-assignment pending validation.

## Runtime/Test Delta

- `src/Riftbound.Engine/MatchRecovery.cs`
  - `MatchRecoveryValidator.ValidateSpectatorReplayFrame` rejects nested spectator replay snapshot `Timing["battle"]["damageAssignment"]["isPending"]` values that disagree with authoritative `ResolutionResult.HasOpenBattleDamageAssignmentWindow(state)`.
  - Added nested object lookup support for the spectator timing battle view.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentPendingMismatch`.

## Validation Commands

- Focused recovery tests:
  - `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"`
  - Result: passed `86/86`.
- Adjacent recovery/opening regression:
  - `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecovery|FullyQualifiedName~PostgresMatchRecoveryStoreSmoke|FullyQualifiedName~OfficialOpening"`
  - Result: passed `667/667`.
- Backend full:
  - `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`
  - Result: passed `6032/6032`.

## Mechanical Checks

Passed before commit:
- `git diff --check`
- `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Scope Guard

Runtime changed: yes, recovery frame validation only.
Protocol/frontend/matrix/official catalog changed: no.
`tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` changed: no.
`riftbound-dotnet.sln` remains untracked and untouched.

Project remains **NOT READY**.
