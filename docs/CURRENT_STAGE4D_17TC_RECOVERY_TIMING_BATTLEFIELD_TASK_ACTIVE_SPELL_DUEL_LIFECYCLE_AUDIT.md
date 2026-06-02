2026-06-02 Stage 4D-17TC recovery timing battlefield-task active spell-duel lifecycle audit

Scope:
- Runtime/server recovery validation only.
- Files changed: `src/Riftbound.Engine/MatchRecovery.cs`, `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`, current checkpoint/coordination docs and this audit note.
- Locked: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

Change:
- Added recovered snapshot same-payload lifecycle validation between active `timing.spellDuel` and matching `battlefieldTasks[]` `START_SPELL_DUEL` items.
- When `spellDuel.isActive=true` and `spellDuel.battlefieldObjectId` is readable, the matching battlefield task must be `ACTIVE`.
- When `spellDuel.focusPlayerId` is readable, the matching battlefield task `actingPlayerId` must match it.
- When `spellDuel.stackItemIds[]` is readable, the matching battlefield task `stackItemIds[]` must match it.

Coverage:
- Added `RecoveryValidatorRejectsSnapshotTimingBattlefieldTaskActiveSpellDuelLifecycleDrift`.
- The test builds a recovered player-view snapshot with an active spell duel at `battlefield-a`, then forges the matching `START_SPELL_DUEL` battlefield task to remain `PENDING`, act for `bob`, and omit the spell-duel stack item.

Validation:
- Focused active spell-duel lifecycle test: `1/1`.
- Focused BattlefieldTask filter: `56/56`.
- Focused recovery filter: `631/631`.
- Adjacent recovery/opening/store-smoke filter: `1211/1211`.
- Backend full: `6576/6576`.
- Mechanical checks: `git diff --check`; anchored conflict-marker scan over `docs`, `src`, `tests`; `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Status:
- Project remains **NOT READY**.
- This narrows P1-004 recovery/replay determinism only; broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
