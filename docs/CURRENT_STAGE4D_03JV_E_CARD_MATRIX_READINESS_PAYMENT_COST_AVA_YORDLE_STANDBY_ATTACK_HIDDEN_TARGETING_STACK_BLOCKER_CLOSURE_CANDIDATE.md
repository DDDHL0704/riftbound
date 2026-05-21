# 4D-03JV-E Card Matrix Readiness Payment-Cost Ava Yordle Standby-Attack Hidden Targeting Stack Blocker Closure Candidate

## Scope

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-47beedf8a4 / OGN·107/298 / 斥候标兵 艾娃 / OGN_AVA_STANDBY_ATTACK_PLAY_UNIT`.

It is a matrix, evidence and audit sync only. It does not change runtime, frontend, Chrome / browser scripts, protocol core fields, official catalog, fullOfficial status, final readiness status, or `riftbound-dotnet.sln`.

## Evidence

- Existing implementation: `src/Riftbound.Engine/CardBehaviorRegistry.cs` binds `OGN·107/298` to `OGN_AVA_STANDBY_ATTACK_PLAY_UNIT` with fixed base-cost, source-to-base unit creation metadata and Yordle unit metadata.
- Existing automated evidence: `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` covers the static Ava representative that pays 5, requires no target selection, resolves the stack, and creates the 4-power Yordle unit in base.
- Fixture evidence: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ogn-ava-yordle-standby-static.fixture.json`.
- Rules/evidence index: `docs/rules-evidence-index.md` and `docs/p2-rules-preflight.md` record the accepted representative evidence while keeping attack-paid standby play and broader timing paths open.
- P2 status evidence: `docs/CURRENT_P2_STATUS.md` records adjacent P2 preflight unit-play evidence for this representative path.
- Stage 3D trigger evidence: `docs/CURRENT_CARD_EFFECT_STAGE3D_ORDER_TRIGGERS_EVIDENCE.md` keeps Ava's standby attack-trigger pressure visible for later ordering and timing breadth.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT` payment-cost functional units: `245 -> 244`.
- Primary `NEEDS_ENGINE_SUPPORT` residual: `166 -> 165`, because the selected row's primary `freezeStatus` moves to `IMPLEMENTED_UNTESTED`.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `433 -> 432`.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `153 -> 152`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` residual remains `328`.
- `NEEDS_FAQ_REVIEW` residual remains `92`.
- Primary FAQ residual remains `61`.
- `fullOfficialTrue=0`.
- `ready=false`.

## Non-Closure

This candidate does not close Ava automated evidence disposition, attack-paid standby play branch, standby / hidden-info visibility breadth, battle / spell-duel lifecycle breadth, complete FEPR target / stack lifecycle matrix, full PaymentEngine / PAY_COST matrix, full official matrix, formal 18-step E2E, or READY.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- Ava / Yordle / Standby / ConformanceFixtureRunnerTests focused regression passed: 3064/3064.
- ActionPrompt / Prompt / PaymentResource / SpendPower / RunePool / Ava / Yordle / Standby / Stack adjacent regression passed: 714/714.
- `PaymentEngineCoverageAuditTests` passed: 530/530.
- Backend full `dotnet test Riftbound.slnx --no-restore` passed: 5101/5101.
- `git diff --check` passed after final doc write.
