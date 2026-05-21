4D-03LQ-E payment-cost Rell keyword-unit targeting-stack blocker closure candidate.

Selected row:
- Selected functionalUnit: `FU-3792d700df`
- Selected card: `SFD·024/221` 芮尔
- Selected effect: `RELL_PLAY_KEYWORD_UNIT`
- Selected partition: `bd-engine-support-payment-cost`
- Selected matrix row query: `payment-cost`
- Selected secondary matrix row query: `payment-and-targeting-stack-timing`

Evidence:
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` records the server-authored `RELL_PLAY_KEYWORD_UNIT` behavior for `SFD·024/221`.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-rell-keyword-unit.fixture.json` covers the zero-target keyword-unit hand-play path.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` covers the fixture replay and direct target-rejection boundary for the zero-target unit play path.
- `docs/rules-evidence-index.md` and `docs/p2-rules-preflight.md` record rule-audited evidence for the current play path.

Matrix transition:
- `NEEDS_ENGINE_SUPPORT` functional units for `payment-cost`: `198 -> 197`.
- Primary `payment-cost` freeze-status residual: `137 -> 136`.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `385 -> 384`.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `124 -> 123`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` remains `328`.
- `NEEDS_FAQ_REVIEW` remains `92`.
- `fullOfficialTrue` remains `0`.
- `ready` remains `false`.

Non-closure:
- Rell automated evidence disposition remains open.
- Rell Bulwark damage-prevention breadth remains open.
- Rell layer / continuous-effect breadth remains open.
- Rell battle / spell-duel lifecycle breadth remains open.
- Complete PaymentEngine / PAY_COST matrix remains open.
- READY remains open.

Validation: validation passed for 4D-03LQ-E: matrix JSON valid (jq empty); 03LQ matrix/current-state guards 2/2; PaymentEngineCoverageAuditTests 603/603; Rell/keyword-unit focused regression 102/102; adjacent prompt/payment/target/stack/keyword/battle/spell-duel/layer regression 2836/2836; backend full test 5174/5174; git diff --check passed.
