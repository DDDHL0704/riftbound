4D-03LP-E payment-cost Assembly Rack equipment / hidden blocker closure candidate.

Scope:
- Selected functionalUnit: `FU-be16217ce7`
- Selected card: `SFD·019/221` 装配架
- Selected effect: `ASSEMBLY_RACK_PLAY_EQUIPMENT`
- Previous input: `Post03LoCardMatrixReadinessPaymentCostRecurveBowEquipmentLayerBlockerClosureCandidateManifest`
- Matrix gate: `E_CARD_MATRIX_READINESS_POST_03LO_PAYMENT_COST_ASSEMBLY_RACK_EQUIPMENT_HIDDEN_BLOCKER_CLOSURE_CANDIDATE`

Matrix transition:
- payment-cost functionalUnits 360 -> 360
- payment-cost snapshotEntries 446 -> 446
- NEEDS_ENGINE_SUPPORT 199 -> 198
- primary residual 138 -> 137
- payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 386 -> 385
- payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 124 -> 124
- NEEDS_AUTOMATED_TEST_EVIDENCE 328 -> 328
- NEEDS_FAQ_REVIEW 92 -> 92
- primary FAQ residual 61 -> 61
- fullOfficialTrue 0 -> 0
- ready false -> false

Non-closure:
- Assembly Rack automated evidence disposition remains open.
- Assembly Rack tap-to-create-robot activated-skill breadth remains open.
- Assembly Rack hidden-info / random-zone breadth remains open.
- complete PaymentEngine / PAY_COST matrix remains open.
- READY remains open.

Validation: passed for 4D-03LP-E: matrix JSON valid (jq empty); 03LP matrix/current-state guards 2/2; PaymentEngineCoverageAuditTests 601/601; Assembly Rack focused regression 5/5; adjacent prompt/payment/equipment/target/stack/hidden/visibility regression 2208/2208; backend full test 5172/5172; git diff --check passed.
