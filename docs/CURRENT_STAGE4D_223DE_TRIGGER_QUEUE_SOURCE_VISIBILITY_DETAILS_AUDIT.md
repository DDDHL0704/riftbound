# Stage 4D-223DE Trigger Queue Source-Visibility Diagnostic Details Audit

Date: 2026-06-19

Status: accepted on `/Users/dinghaolin/IdeaProjects/riftbound` `main`. Project remains **NOT READY**.

## Scope

This slice tightens `triggerQueue` `sourceVisibility` invalid-value diagnostics in `MatchRecoveryValidator`. The valid source-visibility set is now shared between the known-value check and diagnostics, so invalid values retain the old prefix and append stable expected/actual detail, for example `expected [VISIBLE, HIDDEN] but got UNKNOWN`.

Runtime changed: yes, diagnostic detail only in `src/Riftbound.Engine/MatchRecovery.cs`.

Test coverage changed: yes, `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` now asserts detailed suffixes across recovered snapshot timing and spectator replay timing triggerQueue invalid source-visibility surfaces.

## Rule Sources Checked

Checked `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt` from the five root PDFs. Relevant anchors: latest core rules 128, 129.3, 157.3, 157.3.a, 303.2.a, 319-321, 323.4, 333-334, 382-383 and 808.1.d. This slice changes diagnostics only and does not change source visibility, hidden-information, trigger timing, stack, Last Breath, or replay behavior.

## Validation

- Focused source-visibility detail tests: `7/7` passed.
- Focused spectator replay timing triggerQueue shard: `525/525` passed.
- Changed-class `MatchRecoveryTests`: `1976/1976` passed.
- Adjacent Recovery/SpectatorReplay/Snapshot/Timing/ContinuousEffect/TriggerQueue/OrderTriggers/Trigger/Stack/Battle filter: `3569/3569` passed.
- Backend full via `Riftbound.slnx`: `8312/8312` passed.
- `git diff --check` passed.
- Anchored conflict-marker scan over `src`/`tests`/`docs` had no findings.

## Coordination

A_MAIN created no subagent and continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` `main` per user request. External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` remained clean at `01364ee2`; the pre-docs-sync recorded `main...codex/ui-followup-20260616` divergence after code commit `e2000662` was `228 0`. `codex/rule-audit-remaining-20260615` had no commits ahead of `main`; the pre-docs-sync recorded divergence was `301 0`. Exact divergence must be rechecked before integration.

## Non-Goals

This does not change valid recovery replay behavior, trigger construction, trigger ordering, source redaction behavior, stack placement, pending item resolution, priority/focus, payment, legality, battle, prompt rendering, authoritative state serialization, random determinism, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005, or final readiness.
