# Stage 4D-08M Spell Duel Cleanup Next Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 spell-duel cleanup next-prompt / queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens `ClosingSpellDuelWithCleanupRemovedParticipantSkipsOnlyMatchingBattleAndAdvancesNextTask` in `SpellDuelBattleStateMachineTests`. It covers the transition where closing BF-A spell duel performs cleanup, removes the lethal BF-A participant, skips only the matching BF-A battle task, and advances BF-B into spell-duel focus.

Runtime behavior was not changed. The existing transition already advanced to BF-B; this slice makes the state queue, snapshot queue, battlefield-task snapshot and prompt invariants explicit.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Reworked `AssertSpellDuelCloseCleanupAudit(...)` into `AssertSpellDuelCloseCleanupPromptQueueAudit(...)` in `tests/Riftbound.ConformanceTests/SpellDuelBattleStateMachineTests.cs`.
- Strengthened `ClosingSpellDuelWithCleanupRemovedParticipantSkipsOnlyMatchingBattleAndAdvancesNextTask`.
- The audit proves BF-A spell duel close, lethal cleanup and BF-B contest / spell-duel-start event ordering remains stable.
- It proves the resulting state is `SPELL_DUEL_OPEN` with P1 focus, active BF-B spell-duel state and active queue `task:start-spell-duel:BF-B`.
- It proves all BF-A pending tasks and battlefield tasks are removed, while BF-B keeps deterministic `BATTLEFIELD_CONTESTED`, `START_SPELL_DUEL`, `START_BATTLE` queue order.
- It proves the P1 snapshot queue and battlefield-task snapshot expose BF-B active spell-duel / waiting battle task metadata.
- It proves P1 is actionable on BF-B spell-duel focus with `PASS_FOCUS`, P2 is non-actionable, and neither prompt exposes stale `DECLARE_BATTLE`; P1 prompt JSON does not retain BF-A participant or stale BF-A task / spell-duel ids.

## Non-Closure

This narrows spell-duel cleanup-to-next-prompt / queue transition drift risk only. It does not close full spell-duel lifecycle breadth, full battle lifecycle breadth, full prompt/reconnect breadth, full cleanup / replacement-duration breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, READY or goal completion.
