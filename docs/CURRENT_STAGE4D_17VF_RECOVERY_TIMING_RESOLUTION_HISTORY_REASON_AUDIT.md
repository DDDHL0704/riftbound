# Stage 4D-17VF Recovery Timing Resolution-History Reason Audit

Status: accepted on 2026-06-02 by A_MAIN. Project remains **NOT READY**.

## Scope

This slice tightens P1-004 recovery/replay determinism for timing resolution-history `reason` values only. Runtime command resolution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain outside this scope.

## Runtime Change

- `MatchRecoveryValidator` now validates recovered snapshot and spectator replay-frame `battlefieldResolutions[].reason` values against the current battlefield resolution reason domain: `BATTLEFIELD_HELD`, `BATTLEFIELD_CONQUERED`, `BATTLEFIELD_CONTROL_RESOLVED`, `UNCONTROLLED`, `CONTROL_CHANGED`, `CONTROL_CONFIRMED`.
- `MatchRecoveryValidator` now validates recovered snapshot and spectator replay-frame `battleResolutions[].reason` values against the current battle resolution reason domain: `BATTLE_CLOSED`, `BATTLE_NO_RESULT`, `ALL_PARTICIPANTS_DESTROYED`, `BOTH_SIDES_RETAIN_UNITS`.
- Authoritative state resolution-history `Reason` values now apply the same domains alongside existing required, whitespace and redaction-sentinel checks.

## Coverage

New `MatchRecoveryTests` coverage:

- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryReasonValueDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryReasonValueDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryReasonValueDrift`

The tests prove unknown battlefield and battle resolution reasons emit explicit invalid diagnostics across recovered snapshot payloads, authoritative state and spectator replay-frame timing payloads.

## Validation

- Focused new reason tests: `3/3`.
- Focused `ResolutionHistory` filter: `36/36`.
- Focused `MatchRecoveryTests`: `698/698`.
- Adjacent recovery/opening/store-smoke broad filter: `1298/1298`.
- Backend full: `6644/6644`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `src`, `tests`, `docs`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Touched-file scoped format verify for `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`: passed.
- Full `dotnet format Riftbound.slnx --verify-no-changes --no-restore`: still exits `2` only on unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.

## Remaining Risk

This narrows resolution-history reason value-domain enforcement only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
