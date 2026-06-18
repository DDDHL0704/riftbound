# Stage 4D-223BP Recovery Spectator Timing Unreadable Queue Counts Audit

Date: 2026-06-19 03:37 CST

Owner: `A_MAIN`

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / commit: `main` code commit `40c3c8a4`; docs checkpoint follows.

## Scope

Stage 4D-223BP narrows one recovery spectator replay timing diagnostic gap for top-level queue payloads.

- Runtime changed: yes, narrow recovery validation diagnostic only.
- Frontend changed: no.
- `MatchRecoveryValidator` now reports count drift when spectator replay timing `continuousEffects` is present but not readable as a list while authoritative `ContinuousEffects` is non-empty.
- `MatchRecoveryValidator` now reports count drift when spectator replay timing `triggerQueue` is present but not readable as a list while authoritative `TriggerQueue` is non-empty.
- Existing payload-required diagnostics and valid recovery replay behavior are preserved.

## Rule Authority Checked

Rule source was checked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, and extracted text in `/tmp/riftbound_rules_pdf_text/` from the five root PDFs.

- Latest core rules 303.2.a, 333 and 382-383: simultaneous trigger ordering, stack creation and triggered-skill queue/ordering context.
- Latest core rules 401-405: active/triggered skill confirmation, choices, costs and legality context.
- Latest core rules 649-652, especially 651.3: removed-player inability to choose or otherwise affect the game.
- `裁判FAQ_251023.pdf` questions 2.2-2.5: simultaneous trigger ordering, battle initial stack ordering and triggered-cost handling.

This slice changes recovery validation diagnostics only; it does not change trigger ordering, continuous effect evaluation, stack placement, payment, legality or valid replay behavior.

## Validation

Passed:

- Focused `ContinuousEffectAndTriggerQueuePayloadShapeDrift` filter: `2/2`.
- Changed-class `MatchRecoveryTests`: `1974/1974`.
- Adjacent Recovery/SpectatorReplayTiming/ContinuousEffect/TriggerQueue/OrderTriggers/Trigger filter: `2240/2240`.
- Backend full via `Riftbound.slnx`: `8307/8307`.
- `git diff --check`.
- Anchored conflict-marker scan over `docs`, `src`, and `tests`.

## Coordination

No subagent was created; A_MAIN continued directly on local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, 57 commits behind current `main` after the code commit and with no commits ahead of `main`.

`codex/rule-audit-remaining-20260615` had no commits ahead of `main`.

Project remains **NOT READY**.
