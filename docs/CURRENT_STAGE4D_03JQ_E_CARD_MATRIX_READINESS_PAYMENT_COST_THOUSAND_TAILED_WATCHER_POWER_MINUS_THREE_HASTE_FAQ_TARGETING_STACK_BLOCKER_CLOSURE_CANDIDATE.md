# 4D-03JQ-E Card Matrix Readiness Payment-Cost Thousand Tailed Watcher Power-Minus-Three Haste FAQ Targeting Stack Blocker Closure Candidate

## Scope

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-500b9dad14 / OGN·116/298 / 千尾监视者 / THOUSAND_TAILED_WATCHER_PLAY_UNIT_ALL_ENEMY_UNITS_MINUS_3`.

It is a matrix, evidence and audit sync only. It does not change runtime, frontend, Chrome / browser scripts, protocol core fields, official catalog, fullOfficial status, final readiness status, or `riftbound-dotnet.sln`.

## Evidence

- Existing implementation: `src/Riftbound.Engine/CardBehaviorRegistry.cs` binds `OGN·116/298` to the all-enemy-unit power-minus-three play-unit behavior, and `src/Riftbound.Engine/CoreRuleEngine.cs` resolves the play-card path, optional haste-ready payment and temporary power modifier.
- Existing layer evidence: `src/Riftbound.Engine/MatchSession.cs` and the current LayerEngine audit docs keep the minimum-power floor and temporary modifier ordering covered for this representative effect family.
- Existing automated evidence: `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` covers ordinary play, all-enemy unit power reduction, invalid target rejection, optional `HASTE_READY` payment and payment-resource interaction around the same card row.
- Fixture evidence: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-thousand-tailed-watcher-all-enemy-units-minus-3.fixture.json` and `tests/Riftbound.ConformanceTests/Fixtures/p4-play-thousand-tailed-watcher-haste-ready.fixture.json`.
- Rules/evidence index: `docs/rules-evidence-index.md`, `docs/CURRENT_P4_STATUS.md`, `docs/CURRENT_STAGE4D_04M_LAYERENGINE_MINIMUM_POWER_LEDGER_BASELINE_EVIDENCE.md` and `docs/CURRENT_STAGE4D_04O_LAYERENGINE_POWER_MODIFIER_ORDERING_AUDIT.md` record the accepted representative evidence and remaining FAQ / full official breadth.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT` payment-cost functional units: `250 -> 249`.
- Primary `NEEDS_ENGINE_SUPPORT` residual remains `170`, because the selected row's primary `freezeStatus` remains `NEEDS_FAQ_REVIEW`.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `438 -> 437`.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `158 -> 157`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` residual remains `328`.
- `NEEDS_FAQ_REVIEW` residual remains `92`.
- Primary FAQ residual remains `61`.
- `fullOfficialTrue=0`.
- `ready=false`.

## Non-Closure

This candidate does not close Thousand Tailed Watcher automated evidence disposition, FAQ adjudication, complete all-enemy power modifier official breadth, complete haste-ready payment official breadth, complete cleanup / replacement / duration matrix, complete FEPR target / stack lifecycle matrix, full PaymentEngine / PAY_COST matrix, full official matrix, formal 18-step E2E, or READY.

## Validation Results

- Final validation passed: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed; Thousand Tailed focused regression 13/13 passed; adjacent prompt/payment/target/stack regression 1948/1948 passed; PaymentEngineCoverageAuditTests 520/520 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5091/5091 passed; `git diff --check` passed after final doc write.
