# 4D-03KD-E Card Matrix Readiness Payment-Cost Open Action Grant-Boon Layer Targeting-Stack Blocker Closure Candidate

## Scope

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-bcb9f7596a / OGN·153/298 / 公开行动 / OPEN_ACTION_GRANT_BOON_ALL_FRIENDLY_UNITS_NO_CONSUME`.

It is a matrix, evidence and audit sync only. It does not change runtime, frontend, Chrome / browser scripts, protocol core fields, official catalog, fullOfficial status, final readiness status, or `riftbound-dotnet.sln`.

## Evidence

- `ConformanceFixtureRunnerTests` proves the Open Action base spell path with 5-mana payment, zero targets, no pre-existing boon consumption, all-friendly-unit boon grant and permanent +1 base-power tagging.
- `p2-preflight-play-open-action-grant-all-boons.fixture.json` covers the representative Open Action play path.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md` and `docs/CURRENT_P2_STATUS.md` record the accepted representative evidence and deferred official breadth.
- `CardBehaviorRegistry` binds `OGN·153/298` to `OPEN_ACTION_GRANT_BOON_ALL_FRIENDLY_UNITS_NO_CONSUME`.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT`: 237 -> 236.
- Primary residual: 161 -> 160.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: 425 -> 424.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: 146 -> 145.
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: 328 -> 328.
- `NEEDS_FAQ_REVIEW`: 92 -> 92.
- Primary FAQ residual: 61 -> 61.
- `fullOfficialTrue`: 0 -> 0.
- `ready`: false -> false.

This candidate does not close Open Action automated evidence disposition, consume-existing-boon ready branch, full boon/layer breadth, battle / spell-duel lifecycle breadth, complete FEPR target / stack lifecycle matrix, complete PaymentEngine / PAY_COST matrix, formal 18-step E2E, or READY.

## Validation Results

- validation complete: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed; OpenAction/OPEN_ACTION/Boon/ConformanceFixtureRunnerTests focused regression 3051/3051 passed; ActionPrompt/Prompt/PaymentResource/SpendPower/RunePool/OpenAction/Boon/Layer/Stack adjacent regression 776/776 passed; PaymentEngineCoverageAuditTests 546/546 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5117/5117 passed; `git diff --check` passed after final doc write.
