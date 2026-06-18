# Stage 4D-223BZ Recovery Spectator Timing Queue Count Mismatch Details Audit

Status: accepted on 2026-06-19 05:42 CST.

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / commit: `main` code commit `1c3f4c0d`; docs checkpoint follows.

## Scope

This slice narrows recovery spectator replay-frame timing diagnostics for top-level `continuousEffects` and `triggerQueue` count mismatches. It keeps the existing diagnostic prefixes and appends stable `expected ... but got ...` detail suffixes so missing, null, unreadable and list-count drift explicitly identifies authoritative count versus spectator count.

Runtime changed: yes, narrow recovery validation diagnostic detail only.

Frontend changed: no.

## Rule Source

Rule source was rechecked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` extracted from the five root PDFs.

Relevant current anchors for this slice are latest core rules 135-137, 143.2, 317.2.c, 333, 382-383, 401-405, 472.3, and 649-652 especially 651.3, plus `裁判FAQ_251023.pdf` questions 2.2-2.5 for trigger ordering and pending triggered-skill behavior. This slice only makes recovery diagnostics more actionable; it does not reinterpret continuous-effect or trigger-queue legality.

## Runtime Delta

`MatchRecoveryValidator` now routes spectator replay-frame timing continuous-effect and trigger-queue count mismatches through dedicated helpers that preserve the old count diagnostic text and append `expected {authoritativeCount} but got {spectatorCount}`.

The changed validation remains diagnostic-only. It does not change valid recovery replay behavior, continuous effect derivation, trigger queue construction, trigger ordering, pending effect resolution, hidden-source redaction, source-object serialization, authoritative state serialization or random determinism.

## Test Coverage

Updated existing `MatchRecoveryTests` coverage:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectsMissingPayloadWithCountMismatch` now asserts `expected 1 but got 0`.
- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectPayloadShapeWithCountMismatch` now asserts `expected 1 but got 2`.
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueMissingPayloadWithCountMismatch` now asserts `expected 1 but got 0`.
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueMismatchWithCountMismatch` now asserts `expected 2 but got 3`.

## Validation

Passed:

- Focused timing queue count mismatch detail tests: `4/4`.
- Changed-class `MatchRecoveryTests`: `1974/1974`.
- Adjacent Recovery/SpectatorReplayTiming/ContinuousEffect/TriggerQueue/OrderTriggers/Trigger/BattleDamageAssignment filter: `2288/2288`.
- Backend full via `Riftbound.slnx`: `8307/8307`.
- `git diff --check`.
- Anchored conflict-marker scan over `docs`, `src` and `tests`.

## Coordination

No subagent was created; A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, 90 commits behind current `main` and `0` commits ahead at the pre-docs-sync divergence check. Exact divergence must be rechecked before integration.

`codex/rule-audit-remaining-20260615` had no commits ahead of `main`; root PDF text remained available.

Project remains **NOT READY**.
