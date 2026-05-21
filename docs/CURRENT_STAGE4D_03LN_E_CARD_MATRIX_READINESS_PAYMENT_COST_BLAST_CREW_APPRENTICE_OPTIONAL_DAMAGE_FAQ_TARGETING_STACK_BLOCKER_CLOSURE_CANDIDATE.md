4D-03LN-E payment-cost Blast Crew Apprentice optional-damage FAQ / targeting-stack blocker closure candidate.

Scope:
- Selected functionalUnit: `FU-13a792549c`
- Selected card: `SFD·013/221` 爆破队学员
- Selected effect: `BLAST_CREW_APPRENTICE_PLAY_UNIT_OPTIONAL_DAMAGE`
- Previous input: `Post03LmCardMatrixReadinessPaymentCostSiegeRamTrifarianFaqCleanupBlockerClosureCandidateManifest`
- Matrix gate: `E_CARD_MATRIX_READINESS_POST_03LM_PAYMENT_COST_BLAST_CREW_APPRENTICE_OPTIONAL_DAMAGE_FAQ_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE`

Matrix transition:
- payment-cost functionalUnits 360 -> 360
- payment-cost snapshotEntries 446 -> 446
- NEEDS_ENGINE_SUPPORT 201 -> 200
- primary residual 139 -> 139
- payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 388 -> 387
- payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 125 -> 124
- NEEDS_AUTOMATED_TEST_EVIDENCE 328 -> 328
- NEEDS_FAQ_REVIEW 92 -> 92
- primary FAQ residual 61 -> 61
- fullOfficialTrue 0 -> 0
- ready false -> false

Non-closure:
- Blast Crew Apprentice automated evidence disposition remains open.
- Blast Crew Apprentice FAQ adjudication remains open.
- Blast Crew Apprentice optional additional-cost prompt breadth remains open.
- Blast Crew Apprentice red-power payment breadth remains open.
- Blast Crew Apprentice damage / targeting-stack breadth remains open.
- complete FEPR target / stack lifecycle matrix remains open.
- complete PaymentEngine / PAY_COST matrix remains open.
- READY remains open.

Validation: passed for 4D-03LN-E: matrix JSON valid (jq empty); 03LN matrix/current-state guards 2/2; PaymentEngineCoverageAuditTests 597/597; Blast Crew focused regression 39/39; adjacent prompt/payment/target/stack/damage regression 1985/1985; backend full test 5168/5168; git diff --check passed.
