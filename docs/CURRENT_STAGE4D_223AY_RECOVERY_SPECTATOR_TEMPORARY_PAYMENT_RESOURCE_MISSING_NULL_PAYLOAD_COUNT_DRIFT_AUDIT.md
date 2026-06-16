2026-06-16 18:21 CST

Stage 4D-223AY recovery spectator temporary-payment-resource missing/null-payload count-drift validation accepted.

Scope:
- A_MAIN worked directly on local `main` in `/Users/dinghaolin/IdeaProjects/riftbound`.
- Runtime changed: yes, narrow recovery validation diagnostic only.
- Frontend changed: no.
- Touched code: `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Touched coordination docs: current completion audit, P0/P1 closure plan, dispatch/write locks, shared coordination board and this audit file.

What changed:
- `ValidateSpectatorTemporaryPaymentResourcePayloads` now reports spectator replay-frame timing temporary-payment-resource count `0` versus non-empty authoritative temporary payment resources when `temporaryPaymentResources` is missing or null, while preserving the required-payload error.
- Added/renamed paired tests:
  - `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourcesMissingPayloadWithoutCountMismatch`
  - `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourcesMissingPayloadWithCountMismatch`
  - `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourcesNullPayloadWithoutCountMismatch`
  - `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourcesNullPayloadWithCountMismatch`
- Empty-authoritative missing/null payloads still omit count mismatch; non-empty authoritative missing/null payloads now emit both required-payload and count-mismatch diagnostics.

Rule source checked:
- `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`
- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- Latest core rules 131 for cost identity.
- Latest core rules 356-357 for determining and paying card costs, including reaction resource acquisition while paying costs.
- Latest core rules 377 and 401-404 for active/triggered skill cost placement, choices and payment.
- Latest core rules 742.1, 805, 818 and 820 for instruction-text costs, haste, equip and echo payment surfaces.

Coordination:
- No subagent was created.
- A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` `main` per user request.
- External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was checked before docs sync; it was clean at `01364ee2`, 19 commits behind current `main` after the code commit and with no commits ahead of `main`.
- `rule-audit-remaining-20260615` had no new commits ahead of `main` before code commit or docs sync.
- Root PDF rule files remained present.

Validation passed:
- Focused missing/null payload pair: `4/4`.
- Changed-class `MatchRecoveryTests`: `1956/1956`.
- Adjacent TemporaryPaymentResources/TemporaryPaymentResource/PendingPayment/SpectatorReplayTiming/Recovery filter: `2023/2023`.
- Backend full via `Riftbound.slnx`: `8286/8286`.
- `git diff --check` passed before code commit.
- Runtime/test anchored conflict-marker scan passed before code commit.

Code commit:
- `f7d2f75c fix: report recovery temporary payment resource payload count drift`

Non-goals:
- Does not change valid recovery replay behavior.
- Does not change temporary payment resource creation, payment legality, cost determination, reaction resource acquisition, prompt rendering, hidden-source redaction, source-object serialization or authoritative state serialization.
- Does not close random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final readiness.

Project remains **NOT READY**.
