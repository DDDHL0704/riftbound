# 4D-03MQ-E Card Matrix Readiness Candidate

4D-03MQ-E payment-cost Hextech Anomaly resource-conversion FAQ/targeting-stack blocker closure candidate 已建立：E_CARD_MATRIX_READINESS 已把 4D-03MP-E 后的第一百八十九枚 row-level blocker-count reduction 落入 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 顶层 `stage4D03MqPaymentCostHextechAnomalyResourceConversionFaqTargetingStackBlockerClosureCandidate`。`Post03MqCardMatrixReadinessPaymentCostHextechAnomalyResourceConversionFaqTargetingStackBlockerClosureCandidateManifest` records selected functionalUnit=FU-7bb1b2d84a；selected card=SFD·083/221 海克斯异常体；selected effect=HEXTECH_ANOMALY_PLAY_EQUIPMENT；payment-cost functionalUnits=360；payment-cost snapshotEntries=446；NEEDS_ENGINE_SUPPORT 172 -> 171；primary residual 129 -> 129；payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 359 -> 358；payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 108 -> 107；NEEDS_AUTOMATED_TEST_EVIDENCE residual=328；NEEDS_FAQ_REVIEW residual=92；primary FAQ residual=61；fullOfficialTrue 0 -> 0；ready false -> false；项目仍 **NOT READY**。本批不改 runtime、frontend、Chrome/browser script、official catalog、protocol core fields、fullOfficial status、FAQ status 或 final readiness flags；Hextech Anomaly automated evidence disposition remains open；Hextech Anomaly FAQ adjudication remains open；complete equipment attach/follow lifecycle breadth remains open；complete resource conversion family breadth remains open；complete FEPR target / stack lifecycle breadth remains open；complete PaymentEngine / PAY_COST matrix remains open；READY remains open；Chrome smoke not run because there were no frontend or browser-script changes；validation passed for 4D-03MQ-E: jq matrix JSON valid; git diff --check passed; PaymentEngineCoverageAuditTests 655/655 passed; Hextech Anomaly focused regression 6/6 passed; adjacent prompt/payment/equipment/resource-conversion/targeting-stack regression 2278/2278 passed; backend full test 5226/5226 passed.

## Selected Row
- functionalUnitId: FU-7bb1b2d84a
- card: SFD·083/221 海克斯异常体
- effectKind: HEXTECH_ANOMALY_PLAY_EQUIPMENT
- categories: faq-mentioned; payment-cost; targeting-stack-timing

## Evidence
- Runtime evidence: src/Riftbound.Engine/CardBehaviorRegistry.cs contains a direct card behavior entry for SFD·083/221 海克斯异常体 with HEXTECH_ANOMALY_PLAY_EQUIPMENT.
- Fixture evidence: p2-preflight-play-hextech-anomaly-equipment and p4-play-hextech-anomaly-target-rejected keep the zero-target equipment play path and target rejection boundary replayable.
- Resource conversion evidence: docs/CURRENT_STAGE4D_03U_PAYMENT_ENGINE_RESOURCE_CONVERSION_EQUIPMENT_AUDIT.md and docs/CURRENT_STAGE4D_03U_PAYMENT_ENGINE_RESOURCE_CONVERSION_EQUIPMENT_EVIDENCE.md keep the server-authored conversion choices, ordinary generic power to mana command path and no-mutation guards.
- Rules/FAQ evidence: docs/rules-evidence-index.md and docs/p2-rules-preflight.md keep CORE-260330 references plus matrix FAQ ref SOUL-JFAQ-260114 p8.

## Non-Closure
Hextech Anomaly automated evidence disposition remains open. Hextech Anomaly FAQ adjudication remains open. Complete equipment attach/follow lifecycle breadth remains open. Complete resource conversion family breadth remains open. Complete FEPR target / stack lifecycle breadth remains open. Complete PaymentEngine / PAY_COST matrix remains open. READY remains open.

validation passed for 4D-03MQ-E: jq matrix JSON valid; git diff --check passed; PaymentEngineCoverageAuditTests 655/655 passed; Hextech Anomaly focused regression 6/6 passed; adjacent prompt/payment/equipment/resource-conversion/targeting-stack regression 2278/2278 passed; backend full test 5226/5226 passed.
