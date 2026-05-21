4D-03LO-E payment-cost Recurve Bow equipment / layer blocker closure candidate.

Scope:
- Selected functionalUnit: `FU-5bbd056a24`
- Selected card: `SFD·016/221` 反曲之弓
- Selected effect: `RECURVE_BOW_PLAY_EQUIPMENT`
- Previous input: `Post03LnCardMatrixReadinessPaymentCostBlastCrewApprenticeOptionalDamageFaqTargetingStackBlockerClosureCandidateManifest`
- Matrix gate: `E_CARD_MATRIX_READINESS_POST_03LN_PAYMENT_COST_RECURVE_BOW_EQUIPMENT_LAYER_BLOCKER_CLOSURE_CANDIDATE`

Matrix transition:
- payment-cost functionalUnits 360 -> 360
- payment-cost snapshotEntries 446 -> 446
- NEEDS_ENGINE_SUPPORT 200 -> 199
- primary residual 139 -> 138
- payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 387 -> 386
- payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 124 -> 124
- NEEDS_AUTOMATED_TEST_EVIDENCE 328 -> 328
- NEEDS_FAQ_REVIEW 92 -> 92
- primary FAQ residual 61 -> 61
- fullOfficialTrue 0 -> 0
- ready false -> false

Non-closure:
- Recurve Bow automated evidence disposition remains open.
- Recurve Bow full equipment / attach lifecycle breadth remains open.
- Recurve Bow LayerEngine / continuous-effect breadth remains open.
- complete PaymentEngine / PAY_COST matrix remains open.
- READY remains open.

Validation: passed for 4D-03LO-E: matrix JSON valid (jq empty); 03LO matrix/current-state guards 2/2; PaymentEngineCoverageAuditTests 599/599; Recurve Bow focused regression 6/6; adjacent prompt/payment/equipment/assemble/target/stack/layer regression 2293/2293; backend full test 5170/5170; git diff --check passed.
