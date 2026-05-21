# 4D-03KC-E Card Matrix Readiness Payment-Cost Mistfall Bladeyard Equipment/Layer Targeting-Stack Blocker Closure Candidate

## Scope

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-c710134eb9 / OGN·152/298 / 雾临剑冢 / MISTFALL_BLADEYARD_PLAY_EQUIPMENT`.

It is a matrix, evidence and audit sync only. It does not change runtime, frontend, Chrome / browser scripts, protocol core fields, official catalog, fullOfficial status, final readiness status, or `riftbound-dotnet.sln`.

## Evidence

- `ConformanceFixtureRunnerTests` proves the Mistfall Bladeyard base equipment play path and explicit-target rejection boundary.
- `p2-preflight-play-mistfall-bladeyard-equipment.fixture.json` covers the 3-mana, zero-target equipment play path into controller base as an equipment object.
- `p4-play-mistfall-bladeyard-target-rejected.fixture.json` covers the target-count rejection path before payment, hand movement, equipment entry or stack creation.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, `docs/CURRENT_P2_STATUS.md` and `docs/CURRENT_P4_STATUS.md` record the accepted representative evidence and deferred official breadth.
- `CardBehaviorRegistry` binds `OGN·152/298` to `MISTFALL_BLADEYARD_PLAY_EQUIPMENT`.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT`: 238 -> 237.
- Primary residual: 162 -> 161.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: 426 -> 425.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: 147 -> 146.
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: 328 -> 328.
- `NEEDS_FAQ_REVIEW`: 92 -> 92.
- Primary FAQ residual: 61 -> 61.
- `fullOfficialTrue`: 0 -> 0.
- `ready`: false -> false.

This candidate does not close Mistfall Bladeyard automated evidence disposition, boon trigger, attached-equipment continuous modifier breadth, pay-to-rest branch, equipment lifecycle / layer breadth, complete FEPR target / stack lifecycle matrix, complete PaymentEngine / PAY_COST matrix, formal 18-step E2E, or READY.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed; Mistfall/MISTFALL_BLADEYARD/ConformanceFixtureRunnerTests focused regression 3022/3022 passed; ActionPrompt/Prompt/PaymentResource/SpendPower/RunePool/Mistfall/Equipment/Stack adjacent regression 967/967 passed; PaymentEngineCoverageAuditTests 544/544 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5115/5115 passed; `git diff --check` passed after final doc write.
