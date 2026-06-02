2026-06-03 Stage 4D-17VJ recovery timing resolution-history destroyed-object membership audit

Scope:
- A_MAIN tightened `MatchRecoveryValidator` battle-resolution result-list validation for recovered snapshot payloads, authoritative state and spectator replay-frame timing payloads.
- The new invariant is limited to `battleResolutions[]`: each `destroyedObjectIds[]` value must be present in `attackerObjectIds[]` or `defenderObjectIds[]`, and no destroyed object may also appear in `survivingAttackerObjectIds[]` or `survivingDefenderObjectIds[]`.
- Runtime command execution, protocol shape, frontend, matrix JSON, official catalog and final readiness status were not changed.

Runtime change:
- Recovered snapshot timing battle resolution list validation now captures normalized `destroyedObjectIds[]` values and passes them through the shared battle-resolution result-list membership helper.
- Authoritative state battle resolution metadata validation now applies the same destroyed-object membership/disjointness checks after existing list shape/value validation.
- Spectator replay-frame timing battle resolution list validation now applies the same checks before broad authoritative parity diagnostics continue.

Tests added:
- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryDestroyedObjectMembershipDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryDestroyedObjectMembershipDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryDestroyedObjectMembershipDrift`

Validation:
- Focused new destroyed membership tests: `3/3`
- Focused `ResolutionHistory` filter: `48/48`
- Focused `MatchRecoveryTests`: `710/710`
- Adjacent recovery/opening/store-smoke broad filter: `1310/1310`
- Backend full: `6656/6656`
- `git diff --check`: passed
- Anchored conflict-marker scan over `docs`, `tests`, `src`: no matches
- Matrix JSON parse: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed
- Touched-file scoped format verify: passed for `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Full `dotnet format Riftbound.slnx --verify-no-changes --no-restore`: still exits `2` only on unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.

Status:
- This closes the previously open destroyed-object result-list semantics gap for recovery timing resolution-history `battleResolutions[]`.
- Project remains **NOT READY**. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
