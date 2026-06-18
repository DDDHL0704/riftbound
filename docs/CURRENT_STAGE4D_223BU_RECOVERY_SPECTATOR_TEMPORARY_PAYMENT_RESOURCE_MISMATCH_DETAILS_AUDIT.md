# Stage 4D-223BU Recovery Spectator Temporary Payment Resource Mismatch Details Audit

Date: 2026-06-19

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / commit: `main`, code commit `7caba5a5`

## Scope

A_MAIN accepted one direct `main`-branch runtime validation shard for recovery spectator replay timing diagnostics.

Runtime changed: yes, narrow recovery validation diagnostic detail only.

Frontend changed: no.

`MatchRecoveryValidator` now appends stable expected/actual details to spectator replay-frame timing temporary payment resource keyed authoritative mismatch diagnostics. The existing diagnostic prefix remains intact, while scalar, bool, tick, allowed-payment-kind list, and generated/remaining power trait map suffixes identify whether the spectator payload was `<missing>`, `<unreadable>`, or carrying a wrong readable value. Recovery diagnostic formatting now renders long, bool, string-list, and string-int-map values with stable text.

## Rule Source Check

Checked the Stage 4D PDF gate through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, and extracted root PDF text under `/tmp/riftbound_rules_pdf_text/`.

Relevant anchors:

- Latest core rules 131.2-131.3 for mana and rune cost identity.
- Latest core rules 162.1-162.2 and 165.1-165.2 for mana/rune resources and the rune pool as payment storage.
- Latest core rules 357 and 403-404 for card/skill total-cost determination and payment.
- Latest core rules 805, 809, 818 and 820 for optional/additional costs and payment-bearing keyword surfaces.
- Internal evidence index entries 4D-03J and 4D-03K for temporary payment-only resource lifecycle and inline consumption coverage.

## Validation

Passed:

- Focused `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedRequiredFieldAbsenceWithCountMismatch`: `1/1`.
- Adjacent `Recovery|SpectatorReplayTiming|TemporaryPayment|PendingPayment|Payment|BattlefieldTask|TriggerQueue` filter: `3044/3044`.
- Backend full via `Riftbound.slnx`: `8307/8307`.
- `git diff --check`.
- Anchored conflict-marker scan over `docs`, `src`, and `tests`.

`MatchRecoveryTests` changed-class validation also passed before the final tidy: `1974/1974`; the final focused, adjacent and full gates above were re-run after the tidy and are authoritative for this checkpoint.

## Coordination

No subagent was created. A_MAIN continued directly on local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, behind current `main` with `0` commits ahead at the pre-docs-sync divergence check; exact divergence must be rechecked before integration.

`codex/rule-audit-remaining-20260615` had no new commits ahead of `main`.

Root PDF text remained available.

## Non-Scope

This shard does not change valid recovery replay behavior, temporary payment resource creation or consumption, payment legality, payment quote/authorize/commit behavior, trigger payment, pending payment, battlefield task creation, battle or spell-duel start, cleanup ordering, battle-step runtime behavior, trigger ordering, continuous effect evaluation, stack placement, prompt rendering, hidden-source redaction, source-object serialization, authoritative state serialization, random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005, or final readiness.

Project remains **NOT READY**.
