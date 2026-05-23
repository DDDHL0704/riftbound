# Stage 4D-08D Spell Duel Stack Resolution Active Task Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / spell-duel stack resolution active-task audit slice. Project remains **NOT READY**.

## Scope

This slice covers resolving a stack item inside an open spell-duel and returning to the same active spell-duel task instead of completing the battlefield task early.

The accepted coverage proves resolving `STACK-SWIFT-A` emits the expected stack-resolution payload, empties the spell-duel stack, keeps timing in `SPELL_DUEL_OPEN`, advances focus to P2 with an empty passed-focus list, keeps `BF-A` / `spell-duel:BF-A` active, preserves pending queue phase `SPELL_DUEL_TASKS` and active task `task:start-spell-duel:BF-A`, and does not add a spell-duel-completed marker. It also proves the P2 snapshot still exposes the active `START_SPELL_DUEL` task with empty `stackItemIds`, P2 remains actionable with `PASS_FOCUS`, and P1 is non-actionable without `PASS_FOCUS`.

No runtime behavior change was required because the existing spell-duel stack resolution path already returns to the same active battlefield task.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `SpellDuelStackResolutionReturnsToSameActiveTaskUntilBothPlayersPassFocus`.
- Added assertions for stack-resolution payload, spell-duel state, pending queue identity and snapshot battlefield-task shape.
- Added assertions for P2 actionable focus prompt and P1 non-actionable prompt after stack resolution.

## Non-Closure

This narrows spell-duel stack-resolution active-task audit parity only. It does not close full spell-duel lifecycle breadth, full stack lifecycle breadth, full battle lifecycle breadth, full prompt/reconnect breadth, full cleanup / replacement-duration breadth, full hidden-info random-zone breadth, full PaymentEngine / PAY_COST breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial` or READY.
