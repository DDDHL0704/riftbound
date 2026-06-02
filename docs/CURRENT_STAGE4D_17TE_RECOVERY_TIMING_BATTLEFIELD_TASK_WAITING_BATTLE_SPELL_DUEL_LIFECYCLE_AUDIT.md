2026-06-02 Stage 4D-17TE recovery timing battlefield-task waiting battle spell-duel lifecycle audit

Scope:
- Runtime/server recovery validation only.
- Files changed: `src/Riftbound.Engine/MatchRecovery.cs`, `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`, current checkpoint/coordination docs and this audit note.
- Locked: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

Change:
- Added recovered snapshot same-payload lifecycle validation between active `timing.spellDuel` and matching `battlefieldTasks[]` `START_BATTLE` items before battle opens.
- When `spellDuel.isActive=true`, `spellDuel.battlefieldObjectId` is readable and no readable active battle exists, the matching battle task must be `WAITING_FOR_SPELL_DUEL`.
- The matching waiting battle task `actingPlayerId` must be empty.
- The matching waiting battle task `stackItemIds[]` must be empty.

Coverage:
- Added `RecoveryValidatorRejectsSnapshotTimingBattlefieldTaskWaitingBattleSpellDuelLifecycleDrift`.
- The test builds a recovered player-view snapshot with an active spell duel at `battlefield-a`, then forges the matching `START_BATTLE` battlefield task to remain `PENDING`, act for `bob`, and carry a stack item.

Validation:
- Focused waiting battle spell-duel lifecycle test: `1/1`.
- Focused BattlefieldTask filter: `58/58`.
- Focused recovery filter: `633/633`.
- Adjacent recovery/opening/store-smoke filter: `1213/1213`.
- Backend full: `6578/6578`.
- Mechanical checks: `git diff --check`; anchored conflict-marker scan over `docs`, `src`, `tests`; `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Status:
- Project remains **NOT READY**.
- This narrows P1-004 recovery/replay determinism only; broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
