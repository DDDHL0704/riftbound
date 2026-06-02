# Stage 4D-17VE Recovery Timing Resolution-History Kind Audit

Status: accepted on 2026-06-02 by A_MAIN. Project remains **NOT READY**.

## Scope

This slice tightens P1-004 recovery/replay determinism for timing resolution-history `kind` values only. Runtime command resolution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain outside this scope.

## Runtime Change

- `MatchRecoveryValidator` now validates recovered snapshot and spectator replay-frame `battlefieldResolutions[].kind` values against the current battlefield resolution kind domain: `HELD`, `CONQUERED`, `CONTROL_RESOLVED`.
- `MatchRecoveryValidator` now validates recovered snapshot and spectator replay-frame `battleResolutions[].kind` values against the current battle resolution kind domain: `CLOSED`, `NO_RESULT`.
- Authoritative state resolution-history `Kind` values now apply the same domains alongside existing required, whitespace and redaction-sentinel checks.
- Older snapshot resolution-history test fixtures that used the obsolete battle kind placeholder `RESOLVED` were aligned to the current `CLOSED` / `BATTLE_CLOSED` payload values so unrelated tests do not carry stale kind drift.

## Coverage

New `MatchRecoveryTests` coverage:

- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryKindValueDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryKindValueDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryKindValueDrift`

The tests prove unknown battlefield and battle resolution kinds emit explicit invalid diagnostics across recovered snapshot payloads, authoritative state and spectator replay-frame timing payloads.

## Validation

- Focused new kind tests: `3/3`.
- Focused `ResolutionHistory` filter: `33/33`.
- Focused `MatchRecoveryTests`: `695/695`.
- Adjacent recovery/opening/store-smoke broad filter: `1295/1295`.
- Backend full: `6641/6641`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `src`, `tests`, `docs`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Touched-file scoped format verify for `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`: passed.
- Full `dotnet format Riftbound.slnx --verify-no-changes --no-restore`: still exits `2` only on unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.

## Remaining Risk

This narrows resolution-history kind value-domain enforcement only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
