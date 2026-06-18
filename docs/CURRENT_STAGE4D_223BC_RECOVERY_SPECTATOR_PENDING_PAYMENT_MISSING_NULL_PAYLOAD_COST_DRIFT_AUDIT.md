# Stage 4D-223BC Recovery Spectator Pending-Payment Missing/Null Payload Cost Drift Audit

Date: 2026-06-18 10:18 CST

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / code commit: `main` / `c5751b60`

## Scope

This shard closes a narrow recovery-validator gap for spectator replay-frame timing `pendingPayment`.

When authoritative state retains a pending payment, spectator timing payloads that omit `pendingPayment` or set it to `null` now report:

- the existing required-payload diagnostic;
- mana cost `0` versus the authoritative pending-payment mana cost when non-zero;
- power cost `0` versus the authoritative pending-payment power cost when non-zero;
- power-cost-trait count `0` versus the authoritative pending-payment power-cost-trait count when non-empty;
- payment-choice count `0` versus the authoritative pending-payment payment-choice count when non-empty; and
- resource-action count `0` versus the authoritative spectator pending-payment resource-action count when non-empty.

Missing empty-authoritative payloads still report only the required-payload diagnostic, and a `null` payload remains valid when authoritative state has no pending payment.

## Rule Source

The five root PDF rule files remained present. `/tmp/riftbound_rules_pdf_text/` was regenerated during this batch from the root PDFs before documenting the slice.

Relevant latest rule anchors re-checked:

- core rule 131 for cost identity;
- core rules 356-357 for total cost determination and payment;
- core rules 377 and 401-404 for active/triggered skill stack placement, choices and costs;
- core rule 742.1 for instruction-text costs;
- core rules 805, 818 and 820 for Quick Attack, Assemble and Echo payment surfaces;
- `裁判FAQ_251023.pdf` question 2.5 for triggered-skill costs; and
- `铸魂淬炼系列_官方FAQ_260114.pdf` optional / Spellshield cost clarifications.

## Runtime Change

`src/Riftbound.Engine/MatchRecovery.cs` now computes the authoritative pending payment before shape checks. Missing or null spectator payloads still emit the required-payload error; when authoritative state has a pending payment, validation also emits zeroed cost/count diagnostics against authoritative pending-payment dimensions.

This changes only recovery diagnostic reporting. It does not change payment creation, cost determination, payment legality, temporary-resource derivation, prompt rendering, hidden-source redaction, authoritative state serialization or valid replay behavior.

## Tests

`tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` now covers:

- missing `pendingPayment` with empty authoritative state: required-payload error and no cost/count mismatch;
- missing `pendingPayment` with non-empty authoritative state: required-payload error plus mana/power/cost-trait/choice/resource-action zeroed mismatch;
- `pendingPayment = null` with empty authoritative state: accepted with no pending-payment diagnostics; and
- `pendingPayment = null` with non-empty authoritative state: required-payload error plus mana/power/cost-trait/choice/resource-action zeroed mismatch.

Shared helpers build empty and retained pending-payment fixtures and mutate spectator replay timing payloads consistently.

## Validation

- Focused pending-payment filter: `30/30` passed.
- Changed-class `MatchRecoveryTests`: `1969/1969` passed.
- Adjacent PendingPayment/PaymentResource/PaymentCost/TemporaryPaymentResource/SpectatorReplayTiming/Recovery filter: `2502/2502` passed.
- Backend full via `Riftbound.slnx`: `8301/8301` passed.
- `git diff --check` passed before code commit.
- Runtime/test anchored conflict-marker scan passed before code commit.

## Coordination

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, 28 commits behind current `main` after code commit `c5751b60` and with no commits ahead of `main`. `codex/rule-audit-remaining-20260615` had no commits ahead of `main`.

Project remains **NOT READY**. FullOfficial, frontend build/Chrome/formal E2E, real DB-backed Postgres smoke, remaining recovery/authoritative/spectator nested payload breadth and final readiness remain open.
