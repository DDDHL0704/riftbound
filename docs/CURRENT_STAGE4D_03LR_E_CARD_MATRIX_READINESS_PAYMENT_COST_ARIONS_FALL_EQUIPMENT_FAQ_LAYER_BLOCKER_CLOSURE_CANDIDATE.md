4D-03LR-E payment-cost Arion's Fall equipment FAQ/layer blocker closure candidate.

Selected row:
- Selected functionalUnit: `FU-21d25fa80f`
- Selected card: `SFD·030/221` 阿瑞昂的陨落
- Selected effect: `ARIONS_FALL_PLAY_EQUIPMENT`
- Selected partition: `bd-engine-support-payment-cost`
- Selected matrix row query: `payment-cost`
- Selected secondary matrix row query: `payment-or-targeting-stack-timing`

Evidence:
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` records the server-authored `ARIONS_FALL_PLAY_EQUIPMENT` behavior for `SFD·030/221`.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-arions-fall-equipment.fixture.json` covers the zero-target equipment hand-play path.
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-arions-fall-target-rejected.fixture.json` covers explicit-target rejection for the zero-target equipment play path.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` records the Arion's Fall equipment replay, target-rejection and ASSEMBLE_RED attach-path representative evidence.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, `docs/CURRENT_P2_STATUS.md` and `docs/CURRENT_P4_STATUS.md` record rule-audited evidence for the current play / targeting path.

Matrix transition:
- `NEEDS_ENGINE_SUPPORT` functional units for `payment-cost`: `197 -> 196`.
- Primary `payment-cost` freeze-status residual remains `136 -> 136`.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `384 -> 383`.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT` remains `123 -> 123`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` remains `328`.
- `NEEDS_FAQ_REVIEW` remains `92`.
- `fullOfficialTrue` remains `0`.
- `ready` remains `false`.

Non-closure:
- Arion's Fall automated evidence disposition remains open.
- Arion's Fall FAQ adjudication remains open.
- Arion's Fall full equipment / attach lifecycle breadth remains open.
- Arion's Fall LayerEngine / continuous-effect breadth remains open.
- Complete PaymentEngine / PAY_COST matrix remains open.
- READY remains open.

Validation: validation passed for 4D-03LR-E: matrix JSON valid (jq empty); 03LR matrix/current-state guards 2/2; PaymentEngineCoverageAuditTests 605/605; Arion's Fall focused regression 82/82; adjacent prompt/payment/equipment/assemble/target/stack/layer regression 2258/2258; backend full test 5176/5176; git diff --check passed.
