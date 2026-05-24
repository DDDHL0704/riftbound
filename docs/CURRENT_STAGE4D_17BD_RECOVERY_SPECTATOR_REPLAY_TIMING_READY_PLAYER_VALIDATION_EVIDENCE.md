# Stage 4D-17BD Recovery Spectator Replay Timing Ready-Player Validation Evidence

2026-05-24 Stage 4D-17BD evidence bundle for recovery spectator replay timing ready-player validation.

## Runtime/Test Delta

- `src/Riftbound.Engine/MatchRecovery.cs`
  - `MatchRecoveryValidator.ValidateSpectatorReplayFrame` rejects spectator replay snapshot `Timing["readyPlayerIds"]` values that disagree with authoritative final state `ReadyPlayerIds`.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added `RecoveryValidatorRejectsSpectatorReplayTimingReadyPlayersMismatch`.

## Validation Commands

- Focused recovery tests:
  - `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"`
  - Result: passed `82/82`.
- Adjacent recovery/opening regression:
  - `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecovery|FullyQualifiedName~PostgresMatchRecoveryStoreSmoke|FullyQualifiedName~OfficialOpening"`
  - Result: passed `663/663`.
- Backend full:
  - `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`
  - Result: passed `6028/6028`.

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
