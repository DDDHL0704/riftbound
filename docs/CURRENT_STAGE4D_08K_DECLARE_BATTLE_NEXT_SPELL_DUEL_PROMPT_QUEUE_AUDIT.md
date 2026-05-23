# Stage 4D-08K Declare Battle Next Spell Duel Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 declare-battle next-spell-duel prompt / queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens `ImmediateDeclareBattleAdvancesNextContestedBattlefieldTaskAfterCurrentBattleCloses` in `BattlefieldContestBattleTaskGuardTests`. It covers the transition where resolving the current BF-1 battle immediately advances to the next contested battlefield BF-2 and opens its spell-duel focus prompt.

Runtime behavior was not changed. The existing transition already advanced to BF-2; this slice makes the state queue, snapshot queue, battlefield-task snapshot and prompt invariants explicit.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertImmediateDeclareBattleNextSpellDuelPromptQueueAudit(...)` in `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`.
- Strengthened `ImmediateDeclareBattleAdvancesNextContestedBattlefieldTaskAfterCurrentBattleCloses`.
- The audit proves the result is `SPELL_DUEL_OPEN` with P1 focus, active queue `task:start-spell-duel:BF-2`, and deterministic BF-2 `BATTLEFIELD_CONTESTED`, `START_SPELL_DUEL`, `START_BATTLE` task order.
- It proves no BF-1 task remains in the state pending queue or battlefield-task list.
- It proves the P1 snapshot queue and battlefield-task snapshot expose BF-2 active spell-duel / waiting battle task metadata.
- It proves P1 is actionable on BF-2 spell-duel focus with `PASS_FOCUS`, while neither player sees a stale `DECLARE_BATTLE` action.

## Non-Closure

This narrows declare-battle to next-spell-duel prompt / queue transition drift risk only. It does not close full declare-battle legality breadth, full battle lifecycle breadth, full spell-duel lifecycle breadth, full prompt/reconnect breadth, full cleanup / replacement-duration breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, READY or goal completion.
