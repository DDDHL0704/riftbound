# Stage 4D-07P Spell Duel To Battle Declaration Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / spell-duel to battle-declaration audit slice. Project remains **NOT READY**.

## Scope

This slice covers the successful `PASS_FOCUS` path that closes a spell duel after both players have passed focus and promotes the same contested battlefield into its `START_BATTLE` declaration task.

The accepted coverage proves `FOCUS_PASSED` preserves passing player and active focus player, `SPELL_DUEL_CLOSED` records the turn player and completed battlefield, the completed `START_SPELL_DUEL` task keeps the expected task id, reason, battlefield, participants and empty stack metadata, and the pending `START_BATTLE` task keeps the expected task id, reason, battlefield, participants and no acting-player metadata before declaration.

The same audit proves the resulting active player prompt is a battle-declaration prompt for `BF-CONTEST` / `battle:BF-CONTEST`, with only `DECLARE_BATTLE` and `SURRENDER` exposed to P1 while P2 remains on `WAIT` / `SURRENDER`.

No runtime behavior change was required because the existing spell-duel close to battle-declaration handoff already emitted the authoritative audit payloads and task/prompt state.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `PassFocusClosesSpellDuelAndPromotesStartBattleWithParticipantData`.
- Added shared spell-duel to battle-declaration assertions for close payloads, completed/pending battlefield task metadata and prompt metadata.

## Non-Closure

This narrows spell-duel close to battle-declaration handoff audit parity only. It does not close full spell-duel lifecycle breadth, full battle lifecycle breadth, full PaymentEngine / PAY_COST breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
