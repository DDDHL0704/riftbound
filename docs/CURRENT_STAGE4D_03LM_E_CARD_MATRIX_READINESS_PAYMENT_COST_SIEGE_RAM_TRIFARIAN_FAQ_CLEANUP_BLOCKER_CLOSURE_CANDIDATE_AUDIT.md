4D-03LM-E audit: payment-cost Siege Ram Trifarian FAQ / cleanup blocker closure candidate.

Audit conclusion:
- This is a row-level `NEEDS_ENGINE_SUPPORT` blocker-count reduction only.
- It does not close FAQ review, automated evidence disposition, cleanup / replacement-duration breadth, cost-reduction breadth, `fullOfficial`, P0/P1, formal E2E or READY.
- It does not change runtime, frontend, Chrome / browser scripts, protocol core fields, official catalog data, final readiness flags or `riftbound-dotnet.sln`.

Evidence anchors:
- `docs/CURRENT_STAGE4D_03LM_E_CARD_MATRIX_READINESS_PAYMENT_COST_SIEGE_RAM_TRIFARIAN_FAQ_CLEANUP_BLOCKER_CLOSURE_CANDIDATE.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-siege-ram-trifarian-unit.fixture.json`
- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
- `data/official/card-catalog.zh-CN.json`
- `docs/rules-evidence-index.md`
- `docs/p2-rules-preflight.md`
- `docs/CURRENT_P2_STATUS.md`

Required follow-up:
- Resolve Siege Ram FAQ adjudication from `SOUL-JFAQ-260114 p16`.
- Add FU-level automated-test evidence disposition when the matrix accepts it.
- Prove played-card count cost-reduction and cost floor branches before claiming broader official coverage.
- Prove cleanup / replacement-duration breadth before any full official claim.
- Continue PaymentEngine / PAY_COST residual closure and formal 18-step E2E before final readiness.

Validation:
- Passed for 4D-03LM-E: matrix JSON valid; 03LM matrix and current-state guards 2/2; PaymentEngineCoverageAuditTests 595/595; keyword-only unit play/target-rejection regression 459/459; adjacent prompt/payment/cleanup/replacement/unit-source regression 1151/1151; backend full test 5166/5166; git diff --check passed.
