# Stage 4D-223BW Recovery Spectator Pending Payment Mismatch Details Audit

Status: accepted on 2026-06-19 05:05 CST.

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / commit: `main` code commit `0534b3b1`; docs checkpoint follows.

## Scope

This slice narrows recovery spectator replay-frame timing diagnostics for `pendingPayment` authoritative mismatches. It keeps the existing broad diagnostic prefixes and appends stable expected/actual detail suffixes for payment id, window, player, mana cost, power cost, power cost traits, payment choices and payment resource actions.

Runtime changed: yes, narrow recovery validation diagnostic detail only.

Frontend changed: no.

## Rule Source

Rule source was rechecked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` extracted from the five root PDFs.

Relevant current anchors for this slice are latest core rules 131.2-131.3, 162.1-162.2, 165.1-165.2, 357, 403-404, 805, 809, 818 and 820, plus internal evidence index entries 4D-03J and 4D-03K for pending payment and temporary payment resource lifecycle/inline coverage.

## Runtime Delta

`MatchRecoveryValidator` now appends `expected ... but got ...` details to spectator replay-frame timing pending payment authoritative mismatch diagnostics while preserving the old diagnostic prefix. Missing keyed fields report `<missing>`, unreadable keyed values report `<unreadable>`, wrong readable scalars report the wrong value, int maps report canonical `{key: value}` details and string lists report canonical `[value, value]` details.

The changed validation remains diagnostic-only. It does not change valid recovery replay behavior, pending payment construction, payment quote/authorize/commit behavior, temporary payment resource creation or consumption, payment legality, trigger ordering, continuous effects, prompt rendering, random determinism, hidden-source redaction or authoritative state serialization.

## Test Coverage

Updated existing `MatchRecoveryTests` coverage:

- `RecoveryValidatorRejectsSpectatorReplayTimingPendingPaymentMismatch` now asserts detailed expected/actual suffixes for wrong readable pending payment scalar, int, int-map and payment choice values.
- `RecoveryValidatorRejectsSpectatorReplayTimingPendingPaymentIdMissingPayload` now asserts a detailed `<missing>` suffix for an absent authoritative payment id mismatch.
- `RecoveryValidatorRejectsSpectatorReplayTimingPendingPaymentResourceActionsMismatch` now asserts detailed expected/actual suffixes for manual, recycle-rune and temporary-payment-resource action drift.

## Validation

Passed:

- Focused pending-payment mismatch detail tests: `4/4`.
- Changed-class `MatchRecoveryTests`: `1974/1974`.
- Adjacent Recovery/SpectatorReplayTiming/PendingPayment/Payment/TemporaryPayment/PaymentResource/PendingTaskQueue/TriggerQueue filter: `3039/3039`.
- Backend full via `Riftbound.slnx`: `8307/8307`.
- `git diff --check`.
- Anchored conflict-marker scan over `docs`, `src` and `tests`.

## Coordination

No subagent was created; A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, 79 commits behind current `main` and `0` commits ahead at the pre-docs-sync divergence check. Exact divergence must be rechecked before integration.

`codex/rule-audit-remaining-20260615` had no commits ahead of `main`; root PDF text remained available.

Project remains **NOT READY**.
