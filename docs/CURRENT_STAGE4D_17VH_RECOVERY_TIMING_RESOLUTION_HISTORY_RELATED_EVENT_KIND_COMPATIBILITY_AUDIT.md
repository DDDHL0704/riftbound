# Stage 4D-17VH Recovery Timing Resolution-History Related-Event-Kind Compatibility Audit

Status: accepted on 2026-06-02 by A_MAIN. Project remains **NOT READY**.

## Scope

This slice tightens P1-004 recovery/replay determinism for timing resolution-history `relatedEventKinds[]` compatibility only. Runtime command resolution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain outside this scope.

## Runtime Change

- `MatchRecoveryValidator` now rejects recovered snapshot and spectator replay-frame `battlefieldResolutions[]` payloads when known `relatedEventKinds[]` values are incompatible with known `kind` values:
  - `HELD` requires and only allows `BATTLEFIELD_HELD`.
  - `CONQUERED` requires and only allows `BATTLEFIELD_CONQUERED`.
  - `CONTROL_RESOLVED` requires and only allows `BATTLEFIELD_CONTROL_RESOLVED`.
- `MatchRecoveryValidator` now rejects recovered snapshot and spectator replay-frame `battleResolutions[]` payloads when known result-marker event values are incompatible with known `kind` / `reason` values:
  - `CLOSED` / `BATTLE_CLOSED` requires `BATTLE_CLOSED` and rejects `BATTLE_NO_RESULT`.
  - `NO_RESULT` reasons require `BATTLE_NO_RESULT` while still allowing shared battle cleanup event kinds such as `BATTLE_CLOSED`, damage, recall and damage-removal events.
- Authoritative state resolution-history related-event-kind lists now apply the same compatibility checks after existing required, whitespace, redaction-sentinel, duplicate and value-domain checks.
- Unknown related-event-kind diagnostics remain separate; compatibility is checked only when the relevant kind/reason and related-event-kind values are all in known domains.

## Coverage

New `MatchRecoveryTests` coverage:

- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryRelatedEventKindCompatibilityDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryRelatedEventKindCompatibilityDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryRelatedEventKindCompatibilityDrift`

The tests prove legal-but-mismatched battlefield and battle related-event-kind payloads emit explicit invalid-for-kind diagnostics across recovered snapshot payloads, authoritative state and spectator replay-frame timing payloads.

## Validation

- Focused new related-event-kind compatibility tests: `3/3`.
- Focused `ResolutionHistory` filter: `42/42`.
- Focused `MatchRecoveryTests`: `704/704`.
- Adjacent recovery/opening/store-smoke broad filter: `1304/1304`.
- Backend full: `6650/6650`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `src`, `tests`, `docs`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Touched-file scoped format verify for `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`: passed.
- Full `dotnet format Riftbound.slnx --verify-no-changes --no-restore`: still exits `2` only on unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.

## Remaining Risk

This narrows resolution-history related-event-kind compatibility enforcement only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
