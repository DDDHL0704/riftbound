# 4D-03JZ-E Audit: Payment-Cost Kaisa Roam/Conquer Shared-Oracle Targeting-Stack Blocker Closure Candidate

## Decision

`FU-3e9cb3904e / OGN·112/298 / 卡莎` may lose the row-level `NEEDS_ENGINE_SUPPORT` blocker for the payment-cost matrix lane because existing runtime and automated evidence already prove the narrow shared-oracle representative play path for `OGN_KAISA_ALT_A_ROAM_CONQUER_SPELL_PLAY_UNIT` and `OGN_KAISA_ROAM_CONQUER_SPELL_PLAY_UNIT`.

This is not a full-official closure. The row remains `SHARED_ORACLE_IMPLEMENTATION` and still keeps `NEEDS_AUTOMATED_TEST_EVIDENCE` as a full official blocker.

## Accepted Evidence

- `ConformanceFixtureRunnerTests` covers both `OGN·112/298` and `OGN·112a/298` shared-oracle fixtures through pay-cost, stack entry, stack resolution and base unit materialization.
- `p2-preflight-play-ogn-kaisa-roam-conquer-static.fixture.json` and `p2-preflight-play-ogn-kaisa-alt-a-roam-conquer-static.fixture.json` prove the current representative path.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, `docs/CURRENT_P2_STATUS.md` and `docs/CURRENT_P4_STATUS.md` record the evidence and the deferred official breadth.
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` binds the two OGN KaiSa card numbers to the selected shared-oracle effect kinds.

## Write Locks

Runtime, frontend, Chrome / browser scripts, formal 18-step scripts, protocol core fields, official catalog, fullOfficial status, final readiness status and `riftbound-dotnet.sln` remain locked.

## Non-Closure

Chrome smoke is not required for this candidate unless frontend or browser-script files change.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- Kaisa/KAISA/Roam/Conquer/ConformanceFixtureRunnerTests focused regression passed: 3072/3072.
- ActionPrompt/Prompt/PaymentResource/SpendPower/RunePool/Kaisa/Roam/Conquer/Stack adjacent regression passed: 742/742.
- PaymentEngineCoverageAuditTests passed: 538/538.
- Backend full `dotnet test Riftbound.slnx --no-restore` passed: 5109/5109.
- `git diff --check` passed after final doc write.

## Remaining Blockers

Payment-cost blocker closure remains partially open. `B/D_ENGINE_SUPPORT`, `A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE`, `E_CARD_MATRIX_FAQ_REVIEW`, P0-005, P0-004 adjacency audit-sensitive, P1, Kaisa automated evidence disposition, complete roam / conquer spell timing, hidden-info, movement, battlefield-control breadth, full official PaymentEngine matrix closure, formal 18-step E2E, card matrix readiness, and READY all remain open.
