# Stage 4D-08G Reconnect Battle Task Metadata Hidden Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / reconnect battle-task metadata hidden-info audit slice. Project remains **NOT READY**.

## Scope

This slice covers reconnecting while the pending queue is on a battle declaration task after spell-duel completion, with opponent hidden standby information still present elsewhere.

The accepted coverage proves reconnect preserves P1 session identity and reconnect token, keeps pending queue phase `BATTLE_TASKS`, active task `task:start-battle:BF-A`, full deterministic contest / spell-duel / battle queue order, active `START_BATTLE` battlefield-task metadata, battle id `battle:BF-A`, participant ids, battle-declaration prompt identity and prompt actions. It also proves the hidden P2 standby id does not leak through opponent-visible battlefield zones or pending task object ids.

No runtime behavior change was required because the existing reconnect snapshot / battle-task and hidden-info paths already preserve metadata and redaction.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `ReconnectDuringBattleTasksPreservesBattleMetadataAndHiddenRedaction`.
- Added assertions for reconnect identity, pending queue order, active battle task metadata and battle-declaration prompt actions.
- Added assertions that hidden standby ids do not leak through pending task object ids.

## Non-Closure

This narrows reconnect battle-task metadata / hidden-info audit parity only. It does not close full reconnect breadth, full battle lifecycle breadth, full spell-duel lifecycle breadth, full prompt breadth, full cleanup / replacement-duration breadth, full hidden-info random-zone breadth, full PaymentEngine / PAY_COST breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial` or READY.
