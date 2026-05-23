# Stage 4D-08B Pass Focus Rejection Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / pass-focus rejection prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice covers rejected `PASS_FOCUS` attempts for a non-focus player during spell-duel focus, and for a focus command submitted during neutral timing.

The accepted coverage proves a non-focus `PASS_FOCUS` rejection emits no events, preserves tick and state hash, preserves the active multi-contest spell-duel pending queue and P1 active prompt, keeps P2 in the same spell-duel focus context, and exposes no actionable `PASS_FOCUS` action to P2. It also proves a wrong-timing neutral `PASS_FOCUS` rejection leaves timing neutral, focus players empty, pending task queue idle, battlefield tasks empty, and no `PASS_FOCUS` action exposed to either player.

No runtime behavior change was required because the existing phase/focus guards already reject both cases without mutation while preserving prompt context.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `PassFocusByNonFocusPlayerOrWrongTimingRejectsWithoutMutation`.
- Added assertions for non-focus rejection queue / snapshot / prompt preservation.
- Added assertions for wrong-timing neutral idle queue and absent `PASS_FOCUS` actions.

## Non-Closure

This narrows `PASS_FOCUS` rejection prompt-queue audit parity only. It does not close full spell-duel lifecycle breadth, full battle lifecycle breadth, full prompt/reconnect breadth, full cleanup / replacement-duration breadth, full hidden-info random-zone breadth, full PaymentEngine / PAY_COST breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial` or READY.
