# Stage 4D-223BS Recovery Spectator Trigger Queue Mismatch Details Audit

Date: 2026-06-19

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / commit: `main`, code commit `e04ab198`

## Scope

A_MAIN accepted one direct `main`-branch runtime validation shard for recovery spectator replay timing diagnostics.

Runtime changed: yes, narrow recovery validation diagnostic detail only.

Frontend changed: no.

`MatchRecoveryValidator` now appends stable expected/actual details to spectator replay-frame timing trigger queue keyed authoritative mismatch diagnostics. The existing diagnostic prefix remains intact, while the suffix reports expected authoritative values and whether the spectator payload was `<missing>`, `<unreadable>`, or carrying a wrong string.

## Rule Source Check

Checked the Stage 4D PDF gate through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, and extracted root PDF text under `/tmp/riftbound_rules_pdf_text/`.

Relevant anchors:

- Latest core rules 303.2.a, 333, 382-383, and 401-405 for simultaneous trigger ordering, stack placement, triggered skills, choices, costs, and legality.
- Latest core rules 649-652, especially 651.3, for removed players no longer making choices or affecting the game.
- `裁判FAQ_251023.pdf` questions 2.2-2.5 for triggered skill ordering and optional triggered cost handling.

## Validation

Passed:

- Focused `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedPropertyNameWithoutCountMismatch`: `1/1`.
- Changed-class `MatchRecoveryTests`: `1974/1974`.
- Adjacent `Recovery|SpectatorReplayTiming|ContinuousEffect|TriggerQueue|OrderTriggers|Trigger` filter: `2240/2240`.
- Backend full via `Riftbound.slnx`: `8307/8307`.
- `git diff --check`.
- Anchored conflict-marker scan over `docs`, `src`, and `tests`.

## Coordination

No subagent was created. A_MAIN continued directly on local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, behind current `main` and with no commits ahead of `main`; exact divergence must be rechecked before integration.

`codex/rule-audit-remaining-20260615` had no new commits ahead of `main`.

Root PDF text remained available.

## Non-Scope

This shard does not change valid recovery replay behavior, trigger ordering, trigger queue construction, continuous effect evaluation, stack placement, payment, legality, prompt rendering, hidden-source redaction, source-object serialization, authoritative state serialization, random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005, or final readiness.

Project remains **NOT READY**.
