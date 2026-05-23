# Stage 4D-08N Spell Duel To Battle Declaration Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 spell-duel to battle-declaration prompt / queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens `PassFocusClosesSpellDuelAndPromotesStartBattleWithParticipantData` in `BoardTaskQueueFoundationTests`. It covers the transition where a contested battlefield spell duel closes and promotes the matching `START_BATTLE` task into the active battle-declaration window.

Runtime behavior was not changed. The existing transition already promoted the start-battle task; this slice makes the state queue, snapshot queue, battlefield-task snapshot and prompt invariants explicit.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `AssertSpellDuelPromotesBattleDeclarationAudit(...)` in `tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs`.
- The audit proves closing BF-CONTEST spell duel emits `FOCUS_PASSED` before `SPELL_DUEL_CLOSED` and marks BF-CONTEST spell duel completed.
- It proves state queue remains blocking `BATTLE_TASKS` with active task `task:start-battle:BF-CONTEST` and deterministic `BATTLEFIELD_CONTESTED`, `START_SPELL_DUEL`, `START_BATTLE` order.
- It proves the P1 snapshot queue exposes the same task order and queue metadata.
- It proves snapshot battlefield tasks expose completed `START_SPELL_DUEL` and pending `START_BATTLE` metadata, including battle id and participant controller / object ids.
- It proves P1 is actionable on `BATTLE_DECLARATION` with `DECLARE_BATTLE`, P2 is non-actionable with `WAIT`, and both prompts are scoped to `BF-CONTEST` / `battle:BF-CONTEST`.
- It proves stale spell-duel prompt residue does not remain in P1 prompt JSON: no `spell-duel:BF-CONTEST`, no `task:start-spell-duel:BF-CONTEST` and no `PASS_FOCUS`.

## Non-Closure

This narrows spell-duel-to-battle-declaration prompt / queue transition drift risk only. It does not close full spell-duel lifecycle breadth, full battle lifecycle breadth, full prompt/reconnect breadth, full cleanup / replacement-duration breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, READY or goal completion.
