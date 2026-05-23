# Stage 4D-08J Declare Battle Accepted Replay Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 accepted `DECLARE_BATTLE` close / replay prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens active start-battle accepted declaration and accepted-command replay coverage in `BattlefieldContestBattleTaskGuardTests`. It covers a valid `DECLARE_BATTLE` resolving the current `START_BATTLE` task, and the same command being replayed after the battle is already closed.

Runtime behavior was not changed. The existing accepted battle declaration and replay rejection paths already cleared the BF-1 battle task and avoided prompt forks; this slice makes those queue / prompt invariants explicit.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertBattleClosedDeclareBattlePromptQueueAudit(...)` in `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`.
- Strengthened `ActiveStartBattleDeclareBattleClearsTaskAndPreservesRepresentativeEvents`.
- Strengthened `ActiveStartBattleDeclareBattleRejectsAcceptedCommandReplayWithoutMutation`.
- The audit proves accepted battle declaration and replay rejection do not leave `START_BATTLE` or `START_SPELL_DUEL` tasks for `BF-1` in the state queue, P1 snapshot queue or battlefield-task snapshot.
- It proves battle state is inactive, P1/P2 prompts no longer expose `DECLARE_BATTLE`, neither prompt remains `BATTLE_DECLARATION`, and P1 prompt JSON does not retain the destroyed defender id, `task:start-battle:BF-1` or `battle:BF-1`.

## Non-Closure

This narrows accepted `DECLARE_BATTLE` close / replay prompt-queue drift risk only. It does not close full declare-battle legality breadth, full battle lifecycle breadth, full spell-duel lifecycle breadth, full prompt/reconnect breadth, full cleanup / replacement-duration breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, READY or goal completion.
