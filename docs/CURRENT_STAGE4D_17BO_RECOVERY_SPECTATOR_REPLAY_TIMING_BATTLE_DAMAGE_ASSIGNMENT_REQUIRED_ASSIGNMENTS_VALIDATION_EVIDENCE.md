# Stage 4D-17BO Recovery Spectator Replay Timing Battle Damage-Assignment Required-Assignments Validation Evidence

2026-05-24 Stage 4D-17BO evidence bundle for recovery spectator replay timing battle damage-assignment required-assignments validation.

## Runtime/Test Delta

- `src/Riftbound.Engine/MatchRecovery.cs`
  - `BattleDamageAssignmentMatches` now validates pending nested spectator replay snapshot `Timing["battle"]["damageAssignment"]["requiredAssignments"]` against authoritative `ResolutionResult.BattleRequiredAssignmentsFor(state, state.BattleState)`.
  - Added required-assignment parsing/comparison helpers for object and JSON replay shapes.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentRequiredAssignmentsMismatch`.
  - Reused the real pending battle damage-assignment authoritative state builder.

## Validation Commands

- Focused recovery tests:
  - `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"`
  - Result: passed `93/93`.
- Adjacent recovery/opening regression:
  - `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecovery|FullyQualifiedName~PostgresMatchRecoveryStoreSmoke|FullyQualifiedName~OfficialOpening"`
  - Result: passed `674/674`.
- Backend full:
  - `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`
  - Result: passed `6039/6039`.

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
