4D-03LS-E payment-cost Sandcraft echo-token FAQ/targeting-stack blocker closure candidate.

Selected row:
- Selected functionalUnit: `FU-7bfb48b83e`
- Selected card: `SFD·031/221` 点沙成兵
- Selected effect: `SANDCRAFT_CREATE_SAND_SOLDIER_ECHO`
- Selected partition: `bd-engine-support-payment-cost`
- Selected matrix row query: `payment-cost`
- Selected secondary matrix row query: `payment-and-targeting-stack-timing`

Evidence:
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` records the server-authored `SANDCRAFT_CREATE_SAND_SOLDIER_ECHO` behavior for `SFD·031/221`.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-sandcraft-create-one-sand-soldier-base.fixture.json` covers the base one-token creation path.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-sandcraft-echo-create-two-sand-soldiers-base.fixture.json` covers the paid Echo repeat path that creates two Sand Soldier tokens.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` records the Sandcraft base and Echo replay evidence.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, `docs/CURRENT_P2_STATUS.md` and `docs/CURRENT_P4_STATUS.md` record rule-audited evidence for the current token creation / targeting-stack representative path.

Matrix transition:
- `NEEDS_ENGINE_SUPPORT` functional units for `payment-cost`: `196 -> 195`.
- Primary `payment-cost` freeze-status residual remains `136 -> 136`.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `383 -> 382`.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `123 -> 122`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` remains `328`.
- `NEEDS_FAQ_REVIEW` remains `92`.
- `fullOfficialTrue` remains `0`.
- `ready` remains `false`.

Non-closure:
- Sandcraft automated evidence disposition remains open.
- Sandcraft FAQ adjudication remains open.
- Sandcraft complete Echo optional-cost/repeat breadth remains open.
- Sandcraft complete token creation breadth remains open.
- Complete FEPR target / stack lifecycle matrix remains open.
- Complete PaymentEngine / PAY_COST matrix remains open.
- READY remains open.

Validation: validation passed for 4D-03LS-E: matrix JSON valid (jq empty); 03LS matrix/current-state guards 2/2; PaymentEngineCoverageAuditTests 607/607; Sandcraft focused regression 4/4; adjacent prompt/payment/echo/token/target/stack regression 1960/1960; backend full test 5178/5178; git diff --check passed.
