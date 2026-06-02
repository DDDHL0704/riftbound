# Stage 4D-17VI Recovery Timing Resolution-History Survivor Membership Audit

Status: accepted on 2026-06-02 by A_MAIN. Project remains **NOT READY**.

## Scope

This slice tightens P1-004 recovery/replay determinism for battle resolution-history survivor result-list membership only. Runtime command resolution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain outside this scope.

## Runtime Change

- `MatchRecoveryValidator` now rejects recovered snapshot `battleResolutions[]` payloads when `survivingAttackerObjectIds[]` contains an object id not present in `attackerObjectIds[]`.
- `MatchRecoveryValidator` now rejects recovered snapshot `battleResolutions[]` payloads when `survivingDefenderObjectIds[]` contains an object id not present in `defenderObjectIds[]`.
- Authoritative state battle resolution-history and spectator replay-frame battle resolution-history apply the same survivor-role membership checks.
- List shape/value diagnostics and object-registry diagnostics remain separate; survivor membership runs only after the relevant lists are readable.
- Destroyed-object result-list semantics remain a separate open surface and were not changed in this slice.

## Coverage

New `MatchRecoveryTests` coverage:

- `RecoveryValidatorRejectsSnapshotTimingResolutionHistorySurvivorObjectMembershipDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistorySurvivorObjectMembershipDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistorySurvivorObjectMembershipDrift`

The tests prove legal object ids placed in the wrong survivor role emit explicit missing-from-attacker/defender-list diagnostics across recovered snapshot payloads, authoritative state and spectator replay-frame timing payloads.

## Validation

- Focused new survivor membership tests: `3/3`.
- Focused `ResolutionHistory` filter: `45/45`.
- Focused `MatchRecoveryTests`: `707/707`.
- Adjacent recovery/opening/store-smoke broad filter: `1307/1307`.
- Backend full: `6653/6653`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `src`, `tests`, `docs`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Touched-file scoped format verify for `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`: passed.
- Full `dotnet format Riftbound.slnx --verify-no-changes --no-restore`: still exits `2` only on unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.

## Remaining Risk

This narrows battle resolution-history survivor-role membership enforcement only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, destroyed-object result-list semantics, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
