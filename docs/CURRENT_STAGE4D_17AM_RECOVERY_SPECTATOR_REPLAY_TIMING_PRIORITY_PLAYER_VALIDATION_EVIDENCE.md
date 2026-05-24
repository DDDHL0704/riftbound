# Stage 4D-17AM Recovery Spectator Replay Timing Priority-Player Validation Evidence

2026-05-24 Stage 4D-17AM evidence bundle for recovery spectator replay timing priority-player validation.

## Runtime/Test Delta

- `src/Riftbound.Engine/MatchRecovery.cs`
  - `MatchRecoveryValidator.ValidateSpectatorReplayFrame` rejects spectator replay snapshot timing `priorityPlayerId` values that disagree with the authoritative final state `PriorityPlayerId`.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added `RecoveryValidatorRejectsSpectatorReplayTimingPriorityPlayerMismatch`.

## Validation Commands

- Focused recovery tests:
  - `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"`
  - Result: passed `65/65`.
- Adjacent recovery/opening regression:
  - `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecovery|FullyQualifiedName~PostgresMatchRecoveryStoreSmoke|FullyQualifiedName~OfficialOpening"`
  - Result: passed `646/646`.
- Backend full:
  - `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`
  - Result: passed `6011/6011`.

## Mechanical Checks

Passed before commit:
- `git diff --check`
- `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Scope Guard

Runtime changed: yes, recovery frame validation only.
Protocol/frontend/matrix/official catalog changed: no.
`tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` changed: no.
`riftbound-dotnet.sln` remains untracked and untouched.

Project remains **NOT READY**.
