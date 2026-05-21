# 4D-03MD-E Card Matrix Readiness Candidate

4D-03MD-E payment-cost Irelia spellshield keyword-unit FAQ/cleanup/targeting-stack blocker closure candidate 已建立：E_CARD_MATRIX_READINESS 已把 4D-03MC-E 后的第一百七十六枚 row-level blocker-count reduction 落入 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 顶层 `stage4D03MdPaymentCostIreliaSpellshieldKeywordUnitFaqCleanupTargetingStackBlockerClosureCandidate`。`Post03MdCardMatrixReadinessPaymentCostIreliaSpellshieldKeywordUnitFaqCleanupTargetingStackBlockerClosureCandidateManifest` records selected functionalUnit=FU-94e8bc911a；selected card=SFD·057/221 / SFD·057a/221 / SFD·225/221 / SFD·225*/221 艾瑞莉娅；selected effect=SFD_IRELIA_ALT_A_SPELLSHIELD_PLAY_UNIT;SFD_IRELIA_PLAY_KEYWORD_UNIT;SFD_IRELIA_PROMO_PLAY_KEYWORD_UNIT;SFD_IRELIA_SPELLSHIELD_PLAY_UNIT；payment-cost functionalUnits=360；payment-cost snapshotEntries=446；NEEDS_ENGINE_SUPPORT 185 -> 184；primary residual 134 -> 134；payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 372 -> 371；payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 119 -> 118；NEEDS_AUTOMATED_TEST_EVIDENCE residual=328；NEEDS_FAQ_REVIEW residual=92；primary FAQ residual=61；fullOfficialTrue 0 -> 0；ready false -> false；项目仍 **NOT READY**。本批不改 runtime、frontend、Chrome/browser script、official catalog、protocol core fields、fullOfficial status、FAQ status 或 final readiness flags；Irelia automated evidence disposition remains open；Irelia FAQ adjudication remains open；Spellshield target-tax breadth remains open；selected/ready power-modifier duration remains open；cleanup/replacement-duration breadth remains open；complete FEPR target/stack lifecycle breadth remains open；complete PaymentEngine / PAY_COST matrix remains open；payment-cost blocker closure remains partially open；B/D_ENGINE_SUPPORT payment-cost residual remains open；A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE payment-cost residual remains open；E_CARD_MATRIX_FAQ_REVIEW residual remains open；E_CARD_MATRIX_READINESS remains open；card matrix remains open；READY remains open；Chrome smoke not run because there were no frontend or browser-script changes；validation passed for 4D-03MD-E: jq matrix JSON valid; PaymentEngineCoverageAuditTests 629/629 passed; Irelia focused regression 8/8 passed; adjacent prompt/payment/spellshield/keyword/cleanup/targeting-stack regression 2442/2442 passed; backend full test 5200/5200 passed; git diff --check passed.

## Scope
- Selected functional unit: FU-94e8bc911a
- Selected cards: SFD·057/221 / SFD·057a/221 / SFD·225/221 / SFD·225*/221 艾瑞莉娅
- Selected effect: SFD_IRELIA_ALT_A_SPELLSHIELD_PLAY_UNIT;SFD_IRELIA_PLAY_KEYWORD_UNIT;SFD_IRELIA_PROMO_PLAY_KEYWORD_UNIT;SFD_IRELIA_SPELLSHIELD_PLAY_UNIT
- Previous manifest: Post03McCardMatrixReadinessPaymentCostSteraksGageEquipmentFaqLayerControlTargetingStackBlockerClosureCandidateManifest
- Matrix key: stage4D03MdPaymentCostIreliaSpellshieldKeywordUnitFaqCleanupTargetingStackBlockerClosureCandidate

## Evidence
- Server registry: src/Riftbound.Engine/CardBehaviorRegistry.cs binds SFD·057/221, SFD·057a/221, SFD·225/221 and SFD·225*/221 to the shared Irelia play-unit behaviours.
- Runtime tests: p2 preflight fixtures cover all four shared Irelia entries entering as 5-cost, zero-target, Spellshield-tagged unit objects.
- Rule evidence index: docs/rules-evidence-index.md records all four Irelia play-unit representative evidence rows with catalog and CORE-260330 references.

## Non-Closure
- Irelia automated evidence disposition remains open
- Irelia FAQ adjudication remains open
- Spellshield target-tax breadth remains open
- selected/ready power-modifier duration remains open
- cleanup/replacement-duration breadth remains open
- complete FEPR target/stack lifecycle breadth remains open
- complete PaymentEngine / PAY_COST matrix remains open
- payment-cost blocker closure remains partially open
- B/D_ENGINE_SUPPORT payment-cost residual remains open
- A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE payment-cost residual remains open
- E_CARD_MATRIX_FAQ_REVIEW residual remains open
- E_CARD_MATRIX_READINESS remains open
- card matrix remains open
- READY remains open

## Validation
- validation passed for 4D-03MD-E: jq matrix JSON valid; PaymentEngineCoverageAuditTests 629/629 passed; Irelia focused regression 8/8 passed; adjacent prompt/payment/spellshield/keyword/cleanup/targeting-stack regression 2442/2442 passed; backend full test 5200/5200 passed; git diff --check passed.
