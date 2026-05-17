# 4D-03GB-E Payment-Cost Tiny Guardian Optional-Draw Targeting-Stack Blocker Closure Candidate

日期：2026-05-18
状态：**CURRENT / ROW-LEVEL CANDIDATE / NOT READY**

`Post03GbCardMatrixReadinessPaymentCostTinyGuardianOptionalDrawTargetingStackBlockerClosureCandidateManifest` records payment-cost Tiny Guardian optional-draw targeting-stack blocker closure candidate.

```txt
classification=post-03ga-e-card-matrix-readiness-payment-cost-tiny-guardian-optional-draw-targeting-stack-blocker-closure-candidate
gate=E_CARD_MATRIX_READINESS_POST_03GA_PAYMENT_COST_TINY_GUARDIAN_OPTIONAL_DRAW_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE
input previous closure candidate manifest=Post03GaCardMatrixReadinessPaymentCostBaitMoveEnemyUnitTargetingStackBlockerClosureCandidateManifest
selected functionalUnit=FU-722b4c8570
selected card=OGN·044/298 小小守护者
selected effect=TINY_GUARDIAN_PLAY_UNIT_OPTIONAL_DRAW
payment-cost functionalUnits=360
payment-cost snapshotEntries=446
snapshotEntries 1009 -> 1009
functionalUnits 811 -> 811
NEEDS_ENGINE_SUPPORT 342 -> 341
primary residual 198 -> 197
payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 530 -> 529
payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 239 -> 238
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
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-tiny-guardian-no-optional-draw.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-tiny-guardian-optional-draw.fixture.json`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03GB_E_CARD_MATRIX_READINESS_PAYMENT_COST_TINY_GUARDIAN_OPTIONAL_DRAW_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03GA_E_CARD_MATRIX_READINESS_PAYMENT_COST_BAIT_MOVE_ENEMY_UNIT_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE.md`

## Non-Closure

This candidate cannot proxy payment-cost blocker closure, `B/D_ENGINE_SUPPORT` closure, automated evidence closure, FAQ review closure, `E_CARD_MATRIX_READINESS` closure, card matrix closure, full official PaymentEngine matrix closure, `fullOfficial` upgrade or READY.
