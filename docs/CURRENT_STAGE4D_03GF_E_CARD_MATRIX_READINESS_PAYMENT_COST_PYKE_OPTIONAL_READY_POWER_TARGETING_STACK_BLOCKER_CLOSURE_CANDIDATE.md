# 4D-03GF-E Payment-Cost Pyke Optional Ready Power Targeting-Stack Blocker Closure Candidate

日期：2026-05-18
状态：**CURRENT / ROW-LEVEL CANDIDATE / NOT READY**

`Post03GfCardMatrixReadinessPaymentCostPykeOptionalReadyPowerTargetingStackBlockerClosureCandidateManifest` records payment-cost Pyke optional ready power targeting-stack blocker closure candidate.

```txt
classification=post-03ge-e-card-matrix-readiness-payment-cost-pyke-optional-ready-power-targeting-stack-blocker-closure-candidate
gate=E_CARD_MATRIX_READINESS_POST_03GE_PAYMENT_COST_PYKE_OPTIONAL_READY_POWER_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE
input previous closure candidate manifest=Post03GeCardMatrixReadinessPaymentCostRudePirateOptionalDiscardTargetingStackBlockerClosureCandidateManifest
selected functionalUnit=FU-39a5dace47
selected card=UNL-028/219 派克
selected effect=PYKE_PLAY_UNIT_OPTIONAL_READY_POWER
payment-cost functionalUnits=360
payment-cost snapshotEntries=446
snapshotEntries 1009 -> 1009
functionalUnits 811 -> 811
NEEDS_ENGINE_SUPPORT 338 -> 337
primary residual 194 -> 193
payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 526 -> 525
payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 235 -> 234
selected row freezeStatus NEEDS_ENGINE_SUPPORT -> IMPLEMENTED_UNTESTED
selected row fullOfficialBlockers NEEDS_ENGINE_SUPPORT; NEEDS_AUTOMATED_TEST_EVIDENCE -> NEEDS_AUTOMATED_TEST_EVIDENCE
fullOfficialTrue=0
ready=false
NEEDS_AUTOMATED_TEST_EVIDENCE residual=328
NEEDS_FAQ_REVIEW residual=92
primary NEEDS_FAQ_REVIEW residual=61
payment-cost blocker closure remains partially open
B/D_ENGINE_SUPPORT payment-cost residual remains open
A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE payment-cost residual remains open
E_CARD_MATRIX_FAQ_REVIEW payment-cost residual remains open
E_CARD_MATRIX_READINESS remains open
READY remains open
Chrome smoke not run because there were no frontend or browser-script changes
```

## Evidence

- `docs/rules-evidence-index.md`
- `docs/p2-rules-preflight.md`
- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-pyke-no-optional-ready-power.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-pyke-optional-ready-power.fixture.json`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03GF_E_CARD_MATRIX_READINESS_PAYMENT_COST_PYKE_OPTIONAL_READY_POWER_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03GE_E_CARD_MATRIX_READINESS_PAYMENT_COST_RUDE_PIRATE_OPTIONAL_DISCARD_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE.md`

## Non-Closure

This candidate cannot proxy payment-cost blocker closure, `B/D_ENGINE_SUPPORT` closure, automated evidence closure, FAQ review closure, `E_CARD_MATRIX_READINESS` closure, card matrix closure, full official PaymentEngine matrix closure, `fullOfficial` upgrade or READY.
