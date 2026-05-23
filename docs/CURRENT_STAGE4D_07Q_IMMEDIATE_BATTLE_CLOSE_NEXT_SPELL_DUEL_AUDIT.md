# Stage 4D-07Q Immediate Battle Close Next Spell Duel Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / immediate battle close next-spell-duel audit slice. Project remains **NOT READY**.

## Scope

This slice covers the successful immediate `DECLARE_BATTLE` path that closes the current battle, resolves battlefield control and advances to the next contested battlefield's spell-duel task.

The accepted coverage proves `BATTLE_CLOSED` records battlefield, participant, cleared and removed object metadata; `BATTLEFIELD_CONTROL_RESOLVED` records player, battlefield, previous / next controller, changed flag, resolution, battle winner and occupant controllers; and the next-contest handoff emits `BATTLEFIELD_CONTESTED` before `SPELL_DUEL_STARTED` with stable battlefield, focus, caused-by and participant payloads.

No runtime behavior change was required because the existing immediate battle close and next-contest advancement paths already emitted the authoritative audit payloads and ordering.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `ImmediateDeclareBattleAdvancesNextContestedBattlefieldTaskAfterCurrentBattleCloses`.
- Added immediate battle-close next-contest assertions for battle close, control resolution, next-contest handoff and event ordering.

## Non-Closure

This narrows immediate battle close to next-spell-duel handoff audit parity only. It does not close full battle lifecycle breadth, full spell-duel lifecycle breadth, full PaymentEngine / PAY_COST breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
