# Stage 4D-223BT Recovery Spectator Battlefield Task Mismatch Details Audit

Date: 2026-06-19

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / commit: `main`, code commit `d5abb496`

## Scope

A_MAIN accepted one direct `main`-branch runtime validation shard for recovery spectator replay timing diagnostics.

Runtime changed: yes, narrow recovery validation diagnostic detail only.

Frontend changed: no.

`MatchRecoveryValidator` now appends stable expected/actual details to spectator replay-frame timing battlefield task keyed authoritative mismatch diagnostics. The existing diagnostic prefix remains intact, while the suffix reports expected authoritative strings and string lists plus whether the spectator payload was `<missing>`, `<unreadable>`, or carrying the wrong readable value.

## Rule Source Check

Checked the Stage 4D PDF gate through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, and extracted root PDF text under `/tmp/riftbound_rules_pdf_text/`.

Relevant anchors:

- Latest core rules 316.4-316.5 for battle and spell-duel task creation context.
- Latest core rules 323.9-323.14 for pending battlefield battle/spell-duel task cleanup transitions.
- Latest core rules 334-335 for task processing and HOT/FEPR boundaries.
- Latest core rules 454-455 and 458 for battle start and battle-step context.

## Validation

Passed:

- Focused `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskKeyedRequiredFieldAbsenceWithCountMismatch`: `1/1`.
- Changed-class `MatchRecoveryTests`: `1974/1974`.
- Adjacent `Recovery|SpectatorReplayTiming|BattlefieldTask|BattlefieldTasks|ContinuousEffect|TriggerQueue|OrderTriggers|Trigger` filter: `2252/2252`.
- Backend full via `Riftbound.slnx`: `8307/8307`.
- `git diff --check`.
- Anchored conflict-marker scan over `docs`, `src`, and `tests`.

## Coordination

No subagent was created. A_MAIN continued directly on local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, behind current `main` and with no commits ahead of `main`; exact divergence must be rechecked before integration.

`codex/rule-audit-remaining-20260615` had no new commits ahead of `main`.

Root PDF text remained available.

## Non-Scope

This shard does not change valid recovery replay behavior, battlefield task creation, battle or spell-duel start, cleanup ordering, battle-step runtime behavior, trigger ordering, continuous effect evaluation, stack placement, payment, legality, prompt rendering, hidden-source redaction, source-object serialization, authoritative state serialization, random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005, or final readiness.

Project remains **NOT READY**.
