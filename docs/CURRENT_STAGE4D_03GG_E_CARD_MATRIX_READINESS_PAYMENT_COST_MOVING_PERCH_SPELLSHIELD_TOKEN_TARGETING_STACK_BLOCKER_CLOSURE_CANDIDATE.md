# 4D-03GG-E Payment-Cost Moving Perch Spellshield Token Targeting-Stack Blocker Closure Candidate

日期：2026-05-18
状态：**CURRENT / ROW-LEVEL CANDIDATE / NOT READY**

`Post03GgCardMatrixReadinessPaymentCostMovingPerchSpellshieldTokenTargetingStackBlockerClosureCandidateManifest` records payment-cost Moving Perch spellshield token targeting-stack blocker closure candidate.

```txt
classification=post-03gf-e-card-matrix-readiness-payment-cost-moving-perch-spellshield-token-targeting-stack-blocker-closure-candidate
gate=E_CARD_MATRIX_READINESS_POST_03GF_PAYMENT_COST_MOVING_PERCH_SPELLSHIELD_TOKEN_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE
input previous closure candidate manifest=Post03GfCardMatrixReadinessPaymentCostPykeOptionalReadyPowerTargetingStackBlockerClosureCandidateManifest
selected functionalUnit=FU-7c997bad02
selected card=UNL-130/219 移动栖木
selected effect=MOVING_PERCH_SPELLSHIELD_TOKEN_STATIC
payment-cost functionalUnits=360
payment-cost snapshotEntries=446
snapshotEntries 1009 -> 1009
functionalUnits 811 -> 811
NEEDS_ENGINE_SUPPORT 337 -> 336
primary residual 193 -> 192
payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 525 -> 524
payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 234 -> 233
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
- `docs/CURRENT_P2_STATUS.md`
- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-moving-perch-spellshield-token-static.fixture.json`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03GG_E_CARD_MATRIX_READINESS_PAYMENT_COST_MOVING_PERCH_SPELLSHIELD_TOKEN_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03GF_E_CARD_MATRIX_READINESS_PAYMENT_COST_PYKE_OPTIONAL_READY_POWER_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE.md`

## Non-Closure

This candidate cannot proxy payment-cost blocker closure, `B/D_ENGINE_SUPPORT` closure, automated evidence closure, FAQ review closure, `E_CARD_MATRIX_READINESS` closure, card matrix closure, full official PaymentEngine matrix closure, `fullOfficial` upgrade or READY.
