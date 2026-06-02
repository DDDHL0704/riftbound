2026-06-02 Stage 4D-17TB recovery timing battlefield-task keyed value audit

Scope:
- Runtime/server recovery validation only.
- Files changed: `src/Riftbound.Engine/MatchRecovery.cs`, `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`, current checkpoint/coordination docs and this audit note.
- Locked: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

Change:
- Added spectator replay-frame battlefield-task keyed authoritative validation before the existing count-mismatch return.
- Authoritative tasks are indexed by `(battlefieldObjectId, kind)` from `MatchState.BattlefieldTasks`.
- Spectator `battlefieldTasks[]` items with matching authoritative keys now reject readable `status`, optional `actingPlayerId` and `stackItemIds[]` drift with explicit item-level diagnostics.
- Missing, extra and duplicated task kinds remain covered by Stage 4D-17TA kind-set validation.

Coverage:
- Added `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskKeyedValuesWithCountMismatch`.
- The test builds an active spell-duel battlefield task with authoritative `ACTIVE` status, `alice` acting player and a spell-duel stack item, then forges same-key spectator values while adding an extra task so broad list parity is skipped.

Validation:
- Focused keyed-value test: `1/1`.
- Focused BattlefieldTask filter: `55/55`.
- Focused recovery filter: `630/630`.
- Adjacent recovery/opening/store-smoke filter: `1210/1210`.
- Backend full: `6575/6575`.
- Mechanical checks: `git diff --check`; anchored conflict-marker scan over `docs`, `src`, `tests`; `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Status:
- Project remains **NOT READY**.
- This narrows P1-004 recovery/replay determinism only; broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
