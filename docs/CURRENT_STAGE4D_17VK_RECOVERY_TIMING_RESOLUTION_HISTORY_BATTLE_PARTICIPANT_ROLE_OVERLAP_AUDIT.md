2026-06-03 Stage 4D-17VK recovery timing resolution-history battle participant-role overlap audit

Scope:
- A_MAIN tightened `MatchRecoveryValidator` battle-resolution participant-list validation for recovered snapshot payloads, authoritative state and spectator replay-frame timing payloads.
- The new invariant is limited to `battleResolutions[]`: an object id in `attackerObjectIds[]` must not also appear in `defenderObjectIds[]`.
- Runtime command execution, protocol shape, frontend, matrix JSON, official catalog and final readiness status were not changed.

Runtime change:
- The shared battle-resolution result-list membership helper now emits an explicit diagnostic for attacker/defender cross-role overlap before survivor and destroyed-object result-list checks continue.
- Recovered snapshot, authoritative state and spectator replay-frame timing paths already route through this shared helper, so all three surfaces now enforce the same participant-role disjointness.

Tests added:
- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryBattleParticipantRoleOverlapDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryBattleParticipantRoleOverlapDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryBattleParticipantRoleOverlapDrift`

Validation:
- Focused new role-overlap tests: `3/3`
- Focused `ResolutionHistory` filter: `51/51`
- Focused `MatchRecoveryTests`: `713/713`
- Adjacent recovery/opening/store-smoke broad filter: `1313/1313`
- Backend full: `6659/6659`
- `git diff --check`: passed
- Anchored conflict-marker scan over `docs`, `tests`, `src`: no matches
- Matrix JSON parse: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed
- Touched-file scoped format verify: passed for `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Full `dotnet format Riftbound.slnx --verify-no-changes --no-restore`: still exits `2` only on unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.

Status:
- This closes the battle participant role-overlap gap for recovery timing resolution-history `battleResolutions[]`.
- Project remains **NOT READY**. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
