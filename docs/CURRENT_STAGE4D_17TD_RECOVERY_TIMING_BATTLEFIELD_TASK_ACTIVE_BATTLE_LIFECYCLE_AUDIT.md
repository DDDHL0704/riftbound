2026-06-02 Stage 4D-17TD recovery timing battlefield-task active battle lifecycle audit

Scope:
- Runtime/server recovery validation only.
- Files changed: `src/Riftbound.Engine/MatchRecovery.cs`, `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`, current checkpoint/coordination docs and this audit note.
- Locked: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

Change:
- Added recovered snapshot same-payload lifecycle validation between active `timing.battle` and matching `battlefieldTasks[]` `START_BATTLE` items.
- When `battle.isActive=true` and `battle.battlefieldObjectId` is readable, the matching battlefield task must be `ACTIVE`.
- When the snapshot active player is readable, the matching battlefield task `actingPlayerId` must match it.
- The matching active battle task `stackItemIds[]` must be empty.
- When `battle.battleId` is readable, the matching active battle task `battleId` must match it.

Coverage:
- Added `RecoveryValidatorRejectsSnapshotTimingBattlefieldTaskActiveBattleLifecycleDrift`.
- The test builds a recovered player-view snapshot with an active battle at `battlefield-a`, then forges the matching `START_BATTLE` battlefield task to remain `PENDING`, act for `bob`, and carry a stack item.

Validation:
- Focused active battle lifecycle test: `1/1`.
- Focused BattlefieldTask filter: `57/57`.
- Focused recovery filter: `632/632`.
- Adjacent recovery/opening/store-smoke filter: `1212/1212`.
- Backend full: `6577/6577`.
- Mechanical checks: `git diff --check`; anchored conflict-marker scan over `docs`, `src`, `tests`; `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Status:
- Project remains **NOT READY**.
- This narrows P1-004 recovery/replay determinism only; broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
