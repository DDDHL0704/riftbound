# Stage 4D-223DD Trigger Queue Event-Kind Diagnostic Details Audit

Date: 2026-06-19

Status: accepted on `/Users/dinghaolin/IdeaProjects/riftbound` `main`. Project remains **NOT READY**.

## Scope

This slice tightens `triggerQueue` `triggeredByEventKind` invalid-value diagnostics in `MatchRecoveryValidator`. The valid event-kind set is now shared between the known-value check and the diagnostic message, so invalid values retain the old prefix and append stable expected/actual detail, for example `expected [UNIT_PLAYED_TO_BASE, UNIT_DESTROYED, BATTLEFIELD_HELD, UNIT_MOVED_TO_BATTLEFIELD, UNIT_MOVED_TO_BASE, CARD_PLAYED, BATTLE_DECLARED, OBJECT_DESTROYED, UNIT_READY] but got FORGED_EVENT`.

Runtime changed: yes, diagnostic detail only in `src/Riftbound.Engine/MatchRecovery.cs`.

Test coverage changed: yes, `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` now asserts the detailed suffix across recovered snapshot timing, spectator replay timing, and authoritative-state trigger queue invalid-event-kind surfaces.

Post-code remote sync: after code commit `3ccf7afe`, A_MAIN inspected and merged remote `main` commit `c61ca0a8` (`增加服务端候选命令模板契约`), producing merge commit `33044d74`. Incoming work touched server candidate-command template contracts, `MatchSession`, DevUi candidate composition/protocol rendering, appshot artifacts, and `LocalPlayabilityRuleRegressionTests`; it did not touch the 223DD `MatchRecovery` files.

## Rule Sources Checked

Checked `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt` from the five root PDFs. Relevant anchors: latest core rules 128, 129.3, 157.3, 157.3.a, 303.2.a, 319-321, 323.4, 333-334, 382-383 and 808.1.d. This slice changes diagnostics only and does not change trigger timing, hidden-information, stack, Last Breath, or replay behavior.

## Validation

- Focused triggerQueue triggered-event-kind invalid diagnostics: `8/8` passed.
- Focused spectator replay timing triggerQueue shard: `525/525` passed.
- Changed-class `MatchRecoveryTests`: `1976/1976` passed.
- Adjacent Recovery/SpectatorReplay/Snapshot/Timing/ContinuousEffect/TriggerQueue/OrderTriggers/Trigger/Stack/Battle filter: `3569/3569` passed.
- Backend full before the remote merge: `8311/8311` passed.
- DevUi build after merging `c61ca0a8`: passed, including event-label, user-facing text, tabletop layout, wire-table layout, strict typecheck and Vite production build.
- Backend full after merge commit `33044d74`: `8312/8312` passed.
- `git diff --check` passed.
- Anchored conflict-marker scan over `src`/`tests`/`docs` had no findings.

## Coordination

A_MAIN created no subagent and continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` `main` per user request. External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` remained clean at `01364ee2`; current `main...codex/ui-followup-20260616` divergence after the code commit and remote merge was `225 0`. `codex/rule-audit-remaining-20260615` had no commits ahead of `main`; divergence was `298 0`.

## Non-Goals

This does not change valid recovery replay behavior, trigger construction, trigger ordering, hidden-source redaction behavior, stack placement, pending item resolution, priority/focus, payment, legality, battle, prompt rendering, authoritative state serialization, random determinism, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005, or final readiness.
