# Stage 4D-17VG Recovery Timing Resolution-History Kind/Reason Compatibility Audit

Status: accepted on 2026-06-02 by A_MAIN. Project remains **NOT READY**.

## Scope

This slice tightens P1-004 recovery/replay determinism for timing resolution-history `kind` and `reason` compatibility only. Runtime command resolution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain outside this scope.

## Runtime Change

- `MatchRecoveryValidator` now rejects recovered snapshot and spectator replay-frame `battlefieldResolutions[]` payloads when known `reason` values are incompatible with known `kind` values:
  - `HELD` requires `BATTLEFIELD_HELD`.
  - `CONQUERED` requires `BATTLEFIELD_CONQUERED`.
  - `CONTROL_RESOLVED` allows `BATTLEFIELD_CONTROL_RESOLVED`, `UNCONTROLLED`, `CONTROL_CHANGED` or `CONTROL_CONFIRMED`.
- `MatchRecoveryValidator` now rejects recovered snapshot and spectator replay-frame `battleResolutions[]` payloads when known `reason` values are incompatible with known `kind` values:
  - `CLOSED` requires `BATTLE_CLOSED`.
  - `NO_RESULT` allows `BATTLE_NO_RESULT`, `ALL_PARTICIPANTS_DESTROYED` or `BOTH_SIDES_RETAIN_UNITS`.
- Authoritative state resolution-history `Kind`/`Reason` pairs now apply the same compatibility checks after existing required, whitespace, redaction-sentinel and value-domain checks.
- Unknown kind/reason diagnostics remain separate; compatibility is checked only after both values are known.

## Coverage

New `MatchRecoveryTests` coverage:

- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryKindReasonCompatibilityDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryKindReasonCompatibilityDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryKindReasonCompatibilityDrift`

The tests prove legal-but-mismatched battlefield and battle kind/reason pairs emit explicit invalid-for-kind diagnostics across recovered snapshot payloads, authoritative state and spectator replay-frame timing payloads.

## Validation

- Focused new compatibility tests: `3/3`.
- Focused `ResolutionHistory` filter: `39/39`.
- Focused `MatchRecoveryTests`: `701/701`.
- Adjacent recovery/opening/store-smoke broad filter: `1301/1301`.
- Backend full: `6647/6647`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `src`, `tests`, `docs`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Touched-file scoped format verify for `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`: passed.
- Full `dotnet format Riftbound.slnx --verify-no-changes --no-restore`: still exits `2` only on unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.

## Remaining Risk

This narrows resolution-history kind/reason compatibility enforcement only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
