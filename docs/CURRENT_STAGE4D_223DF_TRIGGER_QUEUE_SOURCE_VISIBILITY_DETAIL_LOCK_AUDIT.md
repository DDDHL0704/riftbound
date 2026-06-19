# Stage 4D-223DF Trigger Queue Source-Visibility Detail Lock Audit

Date: 2026-06-19

Status: accepted on `/Users/dinghaolin/IdeaProjects/riftbound` `main`. Project remains **NOT READY**.

## Scope

This slice locks the `triggerQueue` `sourceVisibility` invalid-value detail path after 223DE. `MatchRecoveryValidator` now routes trigger-queue source-visibility required-string validation through a dedicated helper, so snapshot and spectator replay timing paths consistently share the `[VISIBLE, HIDDEN]` known-value set and expected/actual suffix. Residual spectator replay source-visibility invalid-value assertions now require the full detail suffix.

Runtime changed: yes, helper routing only in `src/Riftbound.Engine/MatchRecovery.cs`; valid recovery replay behavior is unchanged.

Test coverage changed: yes, `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` now asserts the detailed suffix for the remaining spectator replay timing source-visibility invalid-value surfaces and value-drift matrix rows.

## Rule Sources Checked

Checked `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt` from the five root PDFs. Relevant anchors: latest core rules 128, 129.3, 157.3, 157.3.a, 303.2.a, 319-321, 323.4, 333-334, 382-383 and 808.1.d. This slice changes diagnostics only and does not change source visibility, hidden-information, trigger timing, stack, Last Breath, or replay behavior.

## Validation

- Focused source-visibility detail lock tests: `7/7` passed.
- Focused spectator replay timing triggerQueue shard: `525/525` passed.
- Changed-class `MatchRecoveryTests`: `1976/1976` passed.
- Adjacent Recovery/SpectatorReplay/Snapshot/Timing/ContinuousEffect/TriggerQueue/OrderTriggers/Trigger/Stack/Battle filter: `3569/3569` passed.
- Backend full via `Riftbound.slnx`: `8312/8312` passed.
- `git diff --check` passed.
- Anchored conflict-marker scan over `src`/`tests`/`docs` had no findings.

## Coordination

A_MAIN created no subagent and continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` `main` per user request. External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` remained clean at `01364ee2`; the pre-docs-sync recorded `main...codex/ui-followup-20260616` divergence after code commit `7e6bb5b5` was `230 0`. `codex/rule-audit-remaining-20260615` had no commits ahead of `main`; the pre-docs-sync recorded divergence was `303 0`. Exact divergence must be rechecked before integration.

## Non-Goals

This does not change valid recovery replay behavior, trigger construction, trigger ordering, source redaction behavior, stack placement, pending item resolution, priority/focus, payment, legality, battle, prompt rendering, authoritative state serialization, random determinism, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005, or final readiness.
