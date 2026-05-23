# Stage 4D-08I Invalid Declare Battle Rejection Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 invalid `DECLARE_BATTLE` rejection prompt / queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens invalid active start-battle `DECLARE_BATTLE` rejection coverage in `BattlefieldContestBattleTaskGuardTests`. It covers wrong battlefield, invalid attacker, base / stale / face-down / non-unit attacker, invalid defender, base / stale / face-down / non-unit defender, and other invalid battlefield-unit combinations.

Runtime behavior was not changed. The existing reject-without-mutation path already preserved state, pending task queue and prompts; this slice makes the prompt / queue preservation explicit for every invalid `DECLARE_BATTLE` theory row.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `AssertRejectedWithoutMutation(...)` in `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`.
- Reused `AssertActiveStartBattlePromptMetadataAudit(...)` for rejection results.
- The audit now proves every invalid active start-battle `DECLARE_BATTLE` rejection preserves tick, empty events, exact state hash, blocking `BATTLE_TASKS` queue identity, deterministic pending-task order, battlefield-task snapshot metadata, P1 actionable battle-declaration prompt scope and valid candidate choices.
- It also proves P2 remains non-actionable and invalid / hidden standby ids remain absent from the P1 prompt JSON after each rejection.

## Non-Closure

This narrows invalid `DECLARE_BATTLE` rejection prompt / queue drift risk only. It does not close full declare-battle legality breadth, full battle lifecycle breadth, full spell-duel lifecycle breadth, full prompt/reconnect breadth, full cleanup / replacement-duration breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, READY or goal completion.
