# Stage 4D-08E Stale Spell Duel Prompt Next Contest Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / stale spell-duel prompt next-contest audit slice. Project remains **NOT READY**.

## Scope

This slice covers replaying the stale BF-A spell-duel prompt after closing BF-A, resolving cleanup, and advancing to the next contested battlefield BF-B.

The accepted coverage proves the stale BF-A prompt replay rejects with `PROMPT_EXPIRED` without events or state-hash drift. It also proves the accepted post-state remains on BF-B / `spell-duel:BF-B`, keeps focus on P1, keeps the BF-A spell-duel-completed marker, keeps the pending queue in `SPELL_DUEL_TASKS` on `task:start-spell-duel:BF-B`, removes all BF-A pending tasks, preserves the BF-B contest / spell-duel / battle task order, keeps the P1 prompt actionable for BF-B, and keeps P2 non-actionable without `PASS_FOCUS`.

No runtime behavior change was required because the existing prompt-snapshot guard already rejects the stale BF-A prompt after the next contest starts.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `SpellDuelFocusStalePromptReplayAfterNextContestStartsRejectsWithoutMutation`.
- Added assertions for stale replay state preservation after BF-B advancement.
- Added assertions for BF-A task removal, BF-B queue order, P1 active prompt and P2 non-actionable prompt.

## Non-Closure

This narrows stale spell-duel prompt next-contest audit parity only. It does not close full spell-duel lifecycle breadth, full battle lifecycle breadth, full prompt/reconnect breadth, full cleanup / replacement-duration breadth, full hidden-info random-zone breadth, full PaymentEngine / PAY_COST breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial` or READY.
