# 4D-03GC-E Payment-Cost Ship Monkey Optional-Boon Targeting-Stack Blocker Closure Candidate

日期：2026-05-18
状态：**CURRENT / ROW-LEVEL CANDIDATE / NOT READY**

`Post03GcCardMatrixReadinessPaymentCostShipMonkeyOptionalBoonTargetingStackBlockerClosureCandidateManifest` records payment-cost Ship Monkey optional-boon targeting-stack blocker closure candidate.

```txt
classification=post-03gb-e-card-matrix-readiness-payment-cost-ship-monkey-optional-boon-targeting-stack-blocker-closure-candidate
gate=E_CARD_MATRIX_READINESS_POST_03GB_PAYMENT_COST_SHIP_MONKEY_OPTIONAL_BOON_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE
input previous closure candidate manifest=Post03GbCardMatrixReadinessPaymentCostTinyGuardianOptionalDrawTargetingStackBlockerClosureCandidateManifest
selected functionalUnit=FU-18d1ef92c2
selected card=SFD·098/221 船猿
selected effect=SHIP_MONKEY_PLAY_UNIT_OPTIONAL_BOON
payment-cost functionalUnits=360
payment-cost snapshotEntries=446
snapshotEntries 1009 -> 1009
functionalUnits 811 -> 811
NEEDS_ENGINE_SUPPORT 341 -> 340
primary residual 197 -> 196
payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 529 -> 528
payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 238 -> 237
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
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ship-monkey-no-optional-boon.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ship-monkey-optional-boon.fixture.json`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03GC_E_CARD_MATRIX_READINESS_PAYMENT_COST_SHIP_MONKEY_OPTIONAL_BOON_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03GB_E_CARD_MATRIX_READINESS_PAYMENT_COST_TINY_GUARDIAN_OPTIONAL_DRAW_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE.md`

## Non-Closure

This candidate cannot proxy payment-cost blocker closure, `B/D_ENGINE_SUPPORT` closure, automated evidence closure, FAQ review closure, `E_CARD_MATRIX_READINESS` closure, card matrix closure, full official PaymentEngine matrix closure, `fullOfficial` upgrade or READY.
