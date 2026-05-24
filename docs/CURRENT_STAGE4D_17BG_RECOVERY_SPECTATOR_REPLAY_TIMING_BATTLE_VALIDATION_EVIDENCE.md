# Stage 4D-17BG Recovery Spectator Replay Timing Battle Validation Evidence

2026-05-24 Stage 4D-17BG evidence bundle for recovery spectator replay timing battle validation.

## Runtime/Test Delta

- `src/Riftbound.Engine/MatchRecovery.cs`
  - `MatchRecoveryValidator.ValidateSpectatorReplayFrame` rejects top-level spectator replay snapshot `Timing["battle"]` values that disagree with authoritative final state `BattleState`.
  - Added nested string-dictionary parsing support for `participantControllerIds`.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added `RecoveryValidatorRejectsSpectatorReplayTimingBattleMismatch`.

## Validation Commands

- Focused recovery tests:
  - `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"`
  - Result: passed `85/85`.
- Adjacent recovery/opening regression:
  - `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecovery|FullyQualifiedName~PostgresMatchRecoveryStoreSmoke|FullyQualifiedName~OfficialOpening"`
  - Result: passed `666/666`.
- Backend full:
  - `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`
  - Result: passed `6031/6031`.

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
