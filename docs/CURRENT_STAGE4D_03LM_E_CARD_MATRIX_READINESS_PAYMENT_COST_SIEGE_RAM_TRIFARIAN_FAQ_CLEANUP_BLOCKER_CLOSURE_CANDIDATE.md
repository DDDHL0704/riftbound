4D-03LM-E payment-cost Siege Ram Trifarian FAQ / cleanup blocker closure candidate.

Scope:
- Selected functional unit: `FU-b3542f5648`
- Selected card: `SFD·012/221` 攻城锤
- Selected effect: `SIEGE_RAM_TRIFARIAN_PLAY_UNIT`
- Matrix row query: `payment-cost`
- Secondary row query: `payment-or-targeting-stack-timing`
- Gate: `E_CARD_MATRIX_READINESS_POST_03LL_PAYMENT_COST_SIEGE_RAM_TRIFARIAN_FAQ_CLEANUP_BLOCKER_CLOSURE_CANDIDATE`
- Classification: `post-03ll-e-card-matrix-readiness-payment-cost-siege-ram-trifarian-faq-cleanup-blocker-closure-candidate`

Evidence accepted for this row-level blocker reduction:
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` maps 攻城锤 to the server-authored `SIEGE_RAM_TRIFARIAN_PLAY_UNIT` direct-card behavior.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-siege-ram-trifarian-unit.fixture.json` covers hand-play base payment, zero-target stack resolution and base placement with the `崔法利` tag.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` binds the fixture to the official-unit play regression path.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md` and `docs/CURRENT_P2_STATUS.md` record the rules evidence and remaining deferred branches.

Matrix impact:
- payment-cost functionalUnits: 360 -> 360
- payment-cost snapshotEntries: 446 -> 446
- NEEDS_ENGINE_SUPPORT: 202 -> 201
- primary NEEDS_ENGINE_SUPPORT residual: 139 -> 139
- payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 389 -> 388
- payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 125 -> 125
- NEEDS_AUTOMATED_TEST_EVIDENCE: 328 -> 328
- NEEDS_FAQ_REVIEW: 92 -> 92
- primary NEEDS_FAQ_REVIEW residual: 61 -> 61
- fullOfficialTrue: 0 -> 0
- ready: false -> false

Selected row transition:
- `freezeStatus` remains `NEEDS_FAQ_REVIEW`.
- `statusFlags` changes from `IMPLEMENTED_UNTESTED;NEEDS_ENGINE_SUPPORT;NEEDS_FAQ_REVIEW` to `IMPLEMENTED_UNTESTED;NEEDS_FAQ_REVIEW`.
- `fullOfficialBlockers` changes from `NEEDS_ENGINE_SUPPORT;NEEDS_FAQ_REVIEW;NEEDS_AUTOMATED_TEST_EVIDENCE` to `NEEDS_FAQ_REVIEW;NEEDS_AUTOMATED_TEST_EVIDENCE`.
- `fullOfficial` remains false.

Non-closure:
- Siege Ram automated evidence disposition remains open.
- Siege Ram FAQ adjudication remains open.
- Siege Ram played-card count cost-reduction branch remains open.
- Siege Ram cost floor branch remains open.
- Siege Ram cleanup / replacement-duration breadth remains open.
- Complete PaymentEngine / PAY_COST matrix remains open.
- Full official readiness remains open.
- READY remains open.

Validation:
- Passed for 4D-03LM-E: matrix JSON valid; 03LM matrix and current-state guards 2/2; PaymentEngineCoverageAuditTests 595/595; keyword-only unit play/target-rejection regression 459/459; adjacent prompt/payment/cleanup/replacement/unit-source regression 1151/1151; backend full test 5166/5166; git diff --check passed.
