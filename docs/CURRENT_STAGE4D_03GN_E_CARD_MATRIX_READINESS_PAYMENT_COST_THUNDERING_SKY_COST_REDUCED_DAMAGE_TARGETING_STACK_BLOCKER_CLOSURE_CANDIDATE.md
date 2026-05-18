# 4D-03GN-E Payment-Cost Thundering Sky cost-reduced damage Targeting-Stack Blocker Closure Candidate

Date: 2026-05-18

Status: **ACCEPTED FOR THIS ROW / NOT READY**

Scope: `E_CARD_MATRIX_READINESS_POST_03GM_PAYMENT_COST_THUNDERING_SKY_COST_REDUCED_DAMAGE_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE`

This batch records one isolated card-matrix row-level blocker reduction for `FU-76ec3a9587` / `OGN·014/298` / `霹天雳地` / `THUNDERING_SKY_DAMAGE_5`.

`Post03GnCardMatrixReadinessPaymentCostThunderingSkyCostReducedDamageTargetingStackBlockerClosureCandidateManifest` takes `Post03GmCardMatrixReadinessPaymentCostSinfulPleasureDiscardDamageTargetingStackBlockerClosureCandidateManifest` as the previous closure-candidate manifest. It keeps the selected partition `bd-engine-support-payment-cost`, selected matrix row query `payment-cost`, and selected secondary row query `payment-and-targeting-stack-timing`.

Matrix transition:

```txt
snapshotEntries 1009 -> 1009
functionalUnits 811 -> 811
payment-cost functionalUnits 360 -> 360
payment-cost snapshotEntries 446 -> 446
NEEDS_ENGINE_SUPPORT 330 -> 329
primary residual 186 -> 185
payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 518 -> 517
payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 227 -> 226
NEEDS_AUTOMATED_TEST_EVIDENCE 328 -> 328
NEEDS_FAQ_REVIEW 92 -> 92
primary FAQ residual 61 -> 61
fullOfficialTrue 0 -> 0
ready false -> false
```

The selected row changes `freezeStatus` from `NEEDS_ENGINE_SUPPORT` to `IMPLEMENTED_UNTESTED`, removes `NEEDS_ENGINE_SUPPORT` from `statusFlags`, and leaves `NEEDS_AUTOMATED_TEST_EVIDENCE` as the remaining full-official blocker.

Evidence anchors:

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-thundering-sky-cost-reduced-damage-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-thundering-sky-insufficient-reduced-cost-rejected.fixture.json`
- `docs/rules-evidence-index.md`
- `docs/p2-rules-preflight.md`
- `docs/CURRENT_P4_STATUS.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Non-closure:

```txt
payment-cost blocker closure remains partially open
B/D_ENGINE_SUPPORT payment-cost residual remains open
A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE payment-cost residual remains open
E_CARD_MATRIX_FAQ_REVIEW payment-cost residual remains open
E_CARD_MATRIX_READINESS remains open
card matrix remains open
READY remains open
```

Chrome smoke was not run because this batch does not change frontend code or browser scripts.
