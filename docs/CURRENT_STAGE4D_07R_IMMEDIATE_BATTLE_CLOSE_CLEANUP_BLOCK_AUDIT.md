# Stage 4D-07R Immediate Battle Close Cleanup Block Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / immediate battle close cleanup-block audit slice. Project remains **NOT READY**.

## Scope

This slice covers the successful immediate `DECLARE_BATTLE` path that closes the current battle but must stop before advancing the next contested battlefield because a state-based cleanup task is pending.

The accepted coverage proves BF-1 battle close still emits stable `UNIT_DESTROYED`, `BATTLE_CLOSED` and `BATTLEFIELD_CONTROL_RESOLVED` payloads, including detached-equipment metadata on the destroyed defender. It also proves the pending queue stays in `STATE_BASED_CLEANUP` on `RECALL_UNATTACHED_EQUIPMENT`, the detached equipment remains on the battlefield as an unresolved cleanup task, and BF-2 spell-duel advancement remains queued instead of emitting `BATTLEFIELD_CONTESTED` or `SPELL_DUEL_STARTED` events.

No runtime behavior change was required because the existing cleanup gate already blocks next-contest advancement before the state-based cleanup task resolves.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `ImmediateDeclareBattleDoesNotAdvanceNextContestedBattlefieldWhenCleanupBlocks`.
- Added immediate battle-close cleanup-block assertions for unit destruction, detached equipment, battle close, control resolution, cleanup queue identity and absent BF-2 advancement events.

## Non-Closure

This narrows immediate battle close cleanup-block audit parity only. It does not close full battle lifecycle breadth, full spell-duel lifecycle breadth, full PaymentEngine / PAY_COST breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
