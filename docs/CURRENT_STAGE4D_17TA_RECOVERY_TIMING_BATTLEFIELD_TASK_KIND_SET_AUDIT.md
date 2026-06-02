2026-06-02 Stage 4D-17TA recovery timing battlefield-task kind-set audit

Scope: A_MAIN tightened `MatchRecoveryValidator` timing battlefield-task validation for recovered player-view snapshots and spectator replay frames.

Runtime validation changes:

- Recovered snapshot `battlefieldTasks[]` kind/battlefield pairs are now checked against readable `snapshot.Lanes["battlefields"]` contested battlefield states. Each contested battlefield requires one `START_SPELL_DUEL` task and one `START_BATTLE` task; task kinds for non-contested or missing battlefield contests now emit explicit same-payload diagnostics.
- Spectator replay-frame `battlefieldTasks[]` kind/battlefield pairs are now checked against authoritative `MatchState.BattlefieldTasks` before the existing count-equal parity comparison. This keeps same-payload missing/extra task-kind diagnostics active when spectator battlefield-task counts disagree and parity is skipped.
- Duplicate task kinds for the same battlefield object id now emit an explicit battlefield-task kind duplicate diagnostic.

Tests added:

- `RecoveryValidatorRejectsSnapshotTimingBattlefieldTaskKindSetInconsistentWithBattlefieldStateContest`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskKindSetWithCountMismatch`

Validation passed:

- Focused kind-set tests: `2/2`
- Focused BattlefieldTask filter: `54/54`
- Focused recovery filter: `629/629`
- Adjacent recovery/opening/store-smoke filter: `1209/1209`
- Backend full conformance: `6574/6574`
- Mechanical checks: `git diff --check`; anchored conflict-marker scan over `docs`, `src` and `tests`; `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Locked / unchanged:

- Protocol shape, frontend, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain unchanged.

Status: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
