# Stage 4D-223CZ Spectator Trigger Queue Details Audit

Date: 2026-06-19

Status: accepted on local `main` as code commit `0fa15f55`; docs checkpoint follows. Project remains **NOT READY**.

## Scope

A_MAIN continued directly in `/Users/dinghaolin/IdeaProjects/riftbound` on `main`.

Runtime changed: yes, diagnostic detail only. Frontend changed: no.

`MatchRecoveryValidator` now appends stable expected/actual ordered-list details to spectator replay frame timing `triggerQueue` aggregate mismatch diagnostics while preserving the existing diagnostic prefixes. Covered aggregate fields are trigger ids, controller ids, source object ids, source visibilities, effect kinds and triggered event kinds.

Existing keyed trigger-queue diagnostics already reported field-level expected/actual details for individual trigger ids. This slice narrows the remaining ordered aggregate `disagree` diagnostics so a recovery failure shows the authoritative ordered values and the spectator payload values in the same message, including hidden-source spectator expectations.

## Rule Source

Rule source was checked through `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt` from the five root PDFs.

Relevant latest core-rule anchors: 157.3 and 157.3.a for resolving spells/skills before trigger and pending item processing, 303.2.a for ordering simultaneous actions/effects/triggers, 319-321 for cleanup and pending item behavior, 323.4 for Last Breath context, 333-340 for stack/HOT pending item and priority flow, 346.1 for focus behavior after triggered/gain skills, and 382-383 for triggered skills. This slice changes diagnostics only; it does not change trigger construction, ordering, stack placement, priority/focus, hidden-source spectator redaction or replay semantics.

## Validation

- Focused mismatch test: `1/1`.
- Focused spectator replay timing triggerQueue shard: `525/525`.
- Changed-class `MatchRecoveryTests`: `1976/1976`.
- Adjacent Recovery/SpectatorReplay/Snapshot/Timing/ContinuousEffect/TriggerQueue/OrderTriggers/Trigger/Stack/Battle filter: `3569/3569`.
- Backend full via `Riftbound.slnx`: `8310/8310`.
- `git diff --check` passed before docs sync.
- Anchored conflict-marker scan over `src`/`tests`/`docs` had no findings before docs sync.

## Coordination

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`; `main...codex/ui-followup-20260616` was `209 0` after the code commit. A_MAIN must inspect that worktree before integration and must not develop directly there.

`codex/rule-audit-remaining-20260615` had no commits ahead of `main`; divergence was `282 0` from local `main` after the code commit.

## Non-Goals

This does not close valid recovery replay behavior, trigger construction, trigger ordering, hidden-source redaction behavior, continuous-effect breadth, LayerEngine breadth, stack placement, pending item resolution, priority/focus, player identity normalization, seat validation, battlefield lane semantics, skill activation or trigger confirmation, payment, legality, battle creation, battle damage assignment computation, damage legality, battle cleanup, battlefield control, prompt rendering, source-object serialization, authoritative state serialization, random determinism, remaining recovered/spectator/authoritative nested payload breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final status.

## Next

Continue the next executable server slice directly on `/Users/dinghaolin/IdeaProjects/riftbound` `main` after re-reading the shared board, `AGENTS.md`, the PDF gate, the UI followup worktree status and `codex/rule-audit-remaining-20260615`.
