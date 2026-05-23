# Stage 4D-08H Active Start Battle Prompt Metadata Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 active start-battle prompt metadata / pending-queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens the active `START_BATTLE` prompt coverage in `BattlefieldContestBattleTaskGuardTests`. It covers the server prompt that lets the active player declare battle after a contested battlefield's spell-duel task is already completed.

Runtime behavior was not changed. The existing pending-task queue, battlefield-task snapshot and battle-declaration prompt metadata already satisfied the contract; this slice makes the contract explicit.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `ActiveStartBattlePromptOnlyExposesCurrentBattlefieldUnitsForActivePlayer` in `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`.
- Added `AssertActiveStartBattlePromptMetadataAudit(...)`.
- The audit proves the pending queue remains blocking in `BATTLE_TASKS` with deterministic `BATTLEFIELD_CONTESTED`, `START_SPELL_DUEL`, `START_BATTLE` task order for `BF-1`.
- It proves the battlefield task snapshot exposes completed `START_SPELL_DUEL`, pending `START_BATTLE`, battle id `battle:BF-1`, participant controller ids and participant object ids.
- It proves the P1 battle-declaration prompt is actionable with `DECLARE_BATTLE` / `SURRENDER`, scoped to `BF-1` / `battle:BF-1`, and exposes only the valid attacker, defender, battlefield and required `COMBAT_ASSIGNMENT` optional cost.
- It proves the P2 prompt remains non-actionable with `WAIT` / `SURRENDER`, and invalid / hidden standby ids are not present in the P1 prompt JSON.

## Non-Closure

This narrows active start-battle prompt metadata and hidden-invalid choice exclusion only. It does not close full battle lifecycle breadth, full spell-duel lifecycle breadth, full prompt/reconnect breadth, full cleanup / replacement-duration breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, READY or goal completion.
