# Stage 4D-08F Reconnect Spell Duel Task Metadata Hidden Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / reconnect spell-duel task metadata hidden-info audit slice. Project remains **NOT READY**.

## Scope

This slice covers reconnecting during an active spell-duel task while the opponent has hidden standby information on another battlefield.

The accepted coverage proves reconnect preserves the P1 session identity and reconnect token, keeps the snapshot focus on P1, keeps pending queue phase `SPELL_DUEL_TASKS` and active task `task:start-spell-duel:BF-A`, preserves deterministic pending queue task order, exposes the active `START_SPELL_DUEL` battlefield task with reason, acting player, participants and empty stack ids, and keeps the P1 prompt actionable with `PASS_FOCUS` / `SURRENDER`. It also proves the hidden P2 standby object does not leak through opponent battlefield zones or pending task object ids.

No runtime behavior change was required because the existing reconnect snapshot / hidden-info paths already preserve task metadata and redaction.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `ReconnectDuringSpellDuelTasksPreservesTaskMetadataAndHiddenRedaction`.
- Added assertions for reconnect identity, pending queue order, active task metadata and prompt actions.
- Added assertions that hidden standby ids do not leak through pending task object ids.

## Non-Closure

This narrows reconnect spell-duel task metadata / hidden-info audit parity only. It does not close full reconnect breadth, full spell-duel lifecycle breadth, full battle lifecycle breadth, full prompt breadth, full cleanup / replacement-duration breadth, full hidden-info random-zone breadth, full PaymentEngine / PAY_COST breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial` or READY.
