4D-03LT-E payment-cost Doran's Shield equipment FAQ/layer/control blocker closure candidate.

Selected row:
- Selected functionalUnit: `FU-d7577883c8`
- Selected card: `SFD·033/221` 多兰之盾
- Selected effect: `DORANS_SHIELD_PLAY_EQUIPMENT`
- Selected partition: `bd-engine-support-payment-cost`
- Selected matrix row query: `payment-cost`
- Selected secondary matrix row query: `payment-cost-layer-continuous-control-zone`

Evidence:
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` records the server-authored `DORANS_SHIELD_PLAY_EQUIPMENT` behavior for `SFD·033/221`.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-dorans-shield-equipment.fixture.json` covers the zero-target equipment hand-play path into base.
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-dorans-shield-target-rejected.fixture.json` covers explicit-target rejection for the zero-target equipment play path.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` records the Doran's Shield play, target-rejection and ASSEMBLE_GREEN representative evidence.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, `docs/CURRENT_P2_STATUS.md`, `docs/CURRENT_P4_STATUS.md`, `docs/CURRENT_FRONTEND_REBUILD_PLAN.md` and `docs/CURRENT_SERVER_RULE_AUDIT.md` record rule-audited evidence for the current equipment play / assemble representative path.

Matrix transition:
- `NEEDS_ENGINE_SUPPORT` functional units for `payment-cost`: `195 -> 194`.
- Primary `payment-cost` freeze-status residual remains `136 -> 136`.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `382 -> 381`.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT` remains `122 -> 122`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` remains `328`.
- `NEEDS_FAQ_REVIEW` remains `92`.
- `fullOfficialTrue` remains `0`.
- `ready` remains `false`.

Non-closure:
- Doran's Shield automated evidence disposition remains open.
- Doran's Shield FAQ adjudication remains open.
- Doran's Shield complete equipment / attach lifecycle breadth remains open.
- Doran's Shield LayerEngine / continuous-effect breadth remains open.
- Doran's Shield ZoneOwnership / control-zone movement breadth remains open.
- Complete PaymentEngine / PAY_COST matrix remains open.
- READY remains open.

Validation: validation passed for 4D-03LT-E: matrix JSON valid (jq empty); 03LT matrix/current-state guards 2/2; PaymentEngineCoverageAuditTests 609/609; Doran's Shield focused regression 15/15; adjacent prompt/payment/equipment/assemble/layer/control regression 892/892; backend full test 5180/5180; git diff --check passed.
