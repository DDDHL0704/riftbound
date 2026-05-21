# 4D-03JZ-E Card Matrix Readiness Payment-Cost Kaisa Roam/Conquer Shared-Oracle Targeting-Stack Blocker Closure Candidate

## Scope

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-3e9cb3904e / OGN·112/298 / 卡莎 / OGN_KAISA_ALT_A_ROAM_CONQUER_SPELL_PLAY_UNIT;OGN_KAISA_ROAM_CONQUER_SPELL_PLAY_UNIT`.

It is a matrix, evidence and audit sync only. It does not change runtime, frontend, Chrome / browser scripts, protocol core fields, official catalog, fullOfficial status, final readiness status, or `riftbound-dotnet.sln`.

## Evidence

- `ConformanceFixtureRunnerTests` proves the OGN KaiSa shared-oracle representative path for `OGN·112/298` and `OGN·112a/298`: paying 6, entering the stack, resolving, and creating a base unit object with the Roam keyword marker.
- `p2-preflight-play-ogn-kaisa-roam-conquer-static.fixture.json` and `p2-preflight-play-ogn-kaisa-alt-a-roam-conquer-static.fixture.json` provide the current automated representative fixtures.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, `docs/CURRENT_P2_STATUS.md` and `docs/CURRENT_P4_STATUS.md` record the representative evidence and deferred official breadth.
- `CardBehaviorRegistry` binds the selected functional unit to `OGN_KAISA_ALT_A_ROAM_CONQUER_SPELL_PLAY_UNIT` and `OGN_KAISA_ROAM_CONQUER_SPELL_PLAY_UNIT`.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT`: 241 -> 240.
- Primary residual: 162 -> 162 because this row remains `SHARED_ORACLE_IMPLEMENTATION`.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: 429 -> 428.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: 150 -> 149.
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: 328 -> 328.
- `NEEDS_FAQ_REVIEW`: 92 -> 92.
- Primary FAQ residual: 61 -> 61.
- `fullOfficialTrue`: 0 -> 0.
- `ready`: false -> false.

This candidate does not close Kaisa automated evidence disposition, complete roam / conquer spell timing, hidden-info, movement, battlefield-control breadth, battle / spell-duel adjacency, complete FEPR target / stack lifecycle matrix, complete PaymentEngine / PAY_COST matrix, full official matrix, formal 18-step E2E, or READY.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- Kaisa/KAISA/Roam/Conquer/ConformanceFixtureRunnerTests focused regression passed: 3072/3072.
- ActionPrompt/Prompt/PaymentResource/SpendPower/RunePool/Kaisa/Roam/Conquer/Stack adjacent regression passed: 742/742.
- PaymentEngineCoverageAuditTests passed: 538/538.
- Backend full `dotnet test Riftbound.slnx --no-restore` passed: 5109/5109.
- `git diff --check` passed after final doc write.
