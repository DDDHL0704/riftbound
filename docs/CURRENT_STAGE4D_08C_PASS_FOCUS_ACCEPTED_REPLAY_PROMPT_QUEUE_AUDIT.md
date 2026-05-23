# Stage 4D-08C Pass Focus Accepted Replay Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / pass-focus accepted replay prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice covers replaying an already accepted `PASS_FOCUS` command after spell-duel focus has advanced from P1 to P2.

The accepted coverage proves the stale accepted-command replay rejects without events, tick drift or state-hash drift while preserving the active spell-duel queue on `task:start-spell-duel:BF-A`. It also proves focus remains with P2, P1 remains recorded in `PassedFocusPlayerIds`, the pending task queue keeps the same contest / spell-duel / battle task order, the P2 snapshot still exposes the active spell-duel task, P2 remains actionable with the `BF-A` / `spell-duel:BF-A` prompt, and P1 remains non-actionable without `PASS_FOCUS`.

No runtime behavior change was required because the existing focus guard already rejects accepted `PASS_FOCUS` replays without mutation and preserves prompt context.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `PassFocusRejectsAcceptedCommandReplayWithoutMutation`.
- Added assertions for stale replay queue identity, focus ownership and passed-focus preservation.
- Added assertions for P2 snapshot active task and P1/P2 prompt actionability.

## Non-Closure

This narrows `PASS_FOCUS` accepted-replay prompt-queue audit parity only. It does not close full spell-duel lifecycle breadth, full battle lifecycle breadth, full prompt/reconnect breadth, full cleanup / replacement-duration breadth, full hidden-info random-zone breadth, full PaymentEngine / PAY_COST breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial` or READY.
