# Stage 4D-17BJ Recovery Spectator Replay Timing Battle Damage-Assignment Identity Validation Evidence

2026-05-24 Stage 4D-17BJ evidence bundle for recovery spectator replay timing battle damage-assignment identity validation.

## Runtime/Test Delta

- `src/Riftbound.Engine/MatchRecovery.cs`
  - Renamed the nested battle damage-assignment helper to `BattleDamageAssignmentMatches`.
  - `MatchRecoveryValidator.ValidateSpectatorReplayFrame` rejects pending nested spectator replay snapshot `Timing["battle"]["damageAssignment"]` battle id, battlefield id and assigning player id values that drift from authoritative state.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentIdentityMismatch`.
  - Reused the real pending battle damage-assignment authoritative state builder added in 17BI.

## Validation Commands

- Focused recovery tests:
  - `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"`
  - Result: passed `88/88`.
- Adjacent recovery/opening regression:
  - `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecovery|FullyQualifiedName~PostgresMatchRecoveryStoreSmoke|FullyQualifiedName~OfficialOpening"`
  - Result: passed `669/669`.
- Backend full:
  - `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`
  - Result: passed `6034/6034`.

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
