# 4D-03GA-E Payment-Cost Bait Move-Enemy-Unit Targeting-Stack Blocker Closure Candidate

日期：2026-05-18
状态：**CURRENT / ROW-LEVEL CANDIDATE / NOT READY**

`Post03GaCardMatrixReadinessPaymentCostBaitMoveEnemyUnitTargetingStackBlockerClosureCandidateManifest` records payment-cost Bait move-enemy-unit targeting-stack blocker closure candidate.

```txt
classification=post-03fz-e-card-matrix-readiness-payment-cost-bait-move-enemy-unit-targeting-stack-blocker-closure-candidate
gate=E_CARD_MATRIX_READINESS_POST_03FZ_PAYMENT_COST_BAIT_MOVE_ENEMY_UNIT_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE
input previous closure candidate manifest=Post03FzCardMatrixReadinessPaymentCostScarletRoseEquipmentReadyUnitTargetingStackBlockerClosureCandidateManifest
selected functionalUnit=FU-6bcef271ca
selected card=SFD·129/221 诱饵
selected effect=BAIT_MOVE_ENEMY_UNIT_TO_ANOTHER_ENEMY_UNIT_LOCATION_NO_ECHO
payment-cost functionalUnits=360
payment-cost snapshotEntries=446
snapshotEntries 1009 -> 1009
functionalUnits 811 -> 811
NEEDS_ENGINE_SUPPORT 343 -> 342
primary residual 199 -> 198
payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 531 -> 530
payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 240 -> 239
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
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-bait-move-enemy-unit-to-another-location.fixture.json`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03GA_E_CARD_MATRIX_READINESS_PAYMENT_COST_BAIT_MOVE_ENEMY_UNIT_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03FZ_E_CARD_MATRIX_READINESS_PAYMENT_COST_SCARLET_ROSE_EQUIPMENT_READY_UNIT_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE.md`

## Non-Closure

This candidate cannot proxy payment-cost blocker closure, `B/D_ENGINE_SUPPORT` closure, automated evidence closure, FAQ review closure, `E_CARD_MATRIX_READINESS` closure, card matrix closure, full official PaymentEngine matrix closure, `fullOfficial` upgrade or READY.
