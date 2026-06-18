# Stage 4D-223BX Recovery Spectator Pending Hand Choice Mismatch Details Audit

Status: accepted on 2026-06-19 05:19 CST.

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / commit: `main` code commit `4591c1b3`; docs checkpoint follows.

## Scope

This slice narrows recovery spectator replay-frame timing diagnostics for `pendingHandChoice` authoritative mismatches. It keeps the existing broad diagnostic prefixes and appends stable expected/actual detail suffixes for choice id, choice window, player, required count, max count, reason, source object, effect kind and spectator choice state.

Runtime changed: yes, narrow recovery validation diagnostic detail only.

Frontend changed: no.

## Rule Source

Rule source was rechecked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` extracted from the five root PDFs.

Relevant current anchors for this slice are latest core rules 107-129 for public/private/hidden information and hand privacy, 333-340 for prompt validation / no-mutation contract, and the hand-choice evidence anchors from 4C-20B: core rules 383.3.d-383.3.e and 422.4 plus the internal evidence index entries for `HAND_CHOICE` / `CHOOSE_HAND_CARDS` viewer redaction. This slice preserves the spectator hidden-info boundary: `legalObjectIds` remain redacted and are still rejected if present.

## Runtime Delta

`MatchRecoveryValidator` now appends `expected ... but got ...` details to spectator replay-frame timing pending hand choice authoritative mismatch diagnostics while preserving the old diagnostic prefix. Missing keyed fields report `<missing>`, unreadable keyed values report `<unreadable>`, and wrong readable scalar values report the wrong value.

The changed validation remains diagnostic-only. It does not change valid recovery replay behavior, hand-choice prompt construction, choice submission, discard/draw resolution, pending payment, pending task queues, prompt rendering, hidden-source redaction, legal object redaction, random determinism or authoritative state serialization.

## Test Coverage

Updated existing `MatchRecoveryTests` coverage:

- `RecoveryValidatorRejectsSpectatorReplayTimingPendingHandChoiceMismatch` now asserts detailed expected/actual suffixes for wrong readable pending hand choice scalar and state values.
- `RecoveryValidatorRejectsSpectatorReplayTimingPendingHandChoiceIdMissingPayload` now asserts a detailed `<missing>` suffix for absent authoritative choice id mismatch.
- `RecoveryValidatorRejectsSpectatorReplayTimingPendingHandChoiceRequiredCountMissingPayload` now asserts a detailed `<missing>` suffix for absent required count mismatch.
- `RecoveryValidatorRejectsSpectatorReplayTimingPendingHandChoiceStateMissingPayload` now asserts a detailed `<missing>` suffix for absent spectator choice state mismatch.
- `RecoveryValidatorRejectsSpectatorReplayTimingPendingHandChoiceSourceObjectMissingPayload` now asserts a detailed `<missing>` suffix for absent source object mismatch.

## Validation

Passed:

- Focused pending-hand-choice mismatch detail tests: `7/7`.
- Changed-class `MatchRecoveryTests`: `1974/1974`.
- Adjacent Recovery/SpectatorReplayTiming/PendingHandChoice/PendingPayment/Payment/TemporaryPayment/PendingTaskQueue/BattlefieldTask/TriggerQueue filter: `3049/3049`.
- Backend full via `Riftbound.slnx`: `8307/8307`.
- `git diff --check`.
- Anchored conflict-marker scan over `docs`, `src` and `tests`.

## Coordination

No subagent was created; A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, 84 commits behind current `main` and `0` commits ahead at the pre-docs-sync divergence check. Exact divergence must be rechecked before integration.

`codex/rule-audit-remaining-20260615` had no commits ahead of `main`; root PDF text remained available.

Project remains **NOT READY**.
