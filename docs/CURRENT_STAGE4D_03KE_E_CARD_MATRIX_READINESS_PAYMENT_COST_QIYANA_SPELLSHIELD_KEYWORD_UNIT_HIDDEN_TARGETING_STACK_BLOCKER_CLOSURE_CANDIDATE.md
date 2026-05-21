# 4D-03KE-E Card Matrix Readiness Payment-Cost Qiyana Spellshield Keyword-Unit Hidden Targeting-Stack Blocker Closure Candidate

## Scope

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-ce711d0cb8 / OGN·155/298 / 奇亚娜 / QIYANA_SPELLSHIELD_PLAY_UNIT`.

It is a matrix, evidence and audit sync only. It does not change runtime, frontend, Chrome / browser scripts, protocol core fields, official catalog, fullOfficial status, final readiness status, or `riftbound-dotnet.sln`.

## Evidence

- `ConformanceFixtureRunnerTests` proves the Qiyana base unit play path with 4-mana payment, zero targets, stack play and resolution to base as a 4-power unit with the `法盾` tag.
- `p2-preflight-play-qiyana-spellshield-keyword-unit.fixture.json` covers the representative Qiyana play path.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md` and `docs/CURRENT_P2_STATUS.md` record the accepted representative evidence and deferred target-tax / conquest breadth.
- `CardBehaviorRegistry` binds `OGN·155/298` to `QIYANA_SPELLSHIELD_PLAY_UNIT`.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT`: 236 -> 235.
- Primary residual: 160 -> 159.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: 424 -> 423.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: 145 -> 144.
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: 328 -> 328.
- `NEEDS_FAQ_REVIEW`: 92 -> 92.
- Primary FAQ residual: 61 -> 61.
- `fullOfficialTrue`: 0 -> 0.
- `ready`: false -> false.

This candidate does not close Qiyana automated evidence disposition, Spellshield target-tax matrix, conquest draw-or-call-rune branch, hidden/random zone breadth, battle / spell-duel lifecycle breadth, complete FEPR target / stack lifecycle matrix, complete PaymentEngine / PAY_COST matrix, formal 18-step E2E, or READY.

## Validation Results

- validation complete: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed; Qiyana/QIYANA/Spellshield/ConformanceFixtureRunnerTests focused regression 3056/3056 passed; ActionPrompt/Prompt/PaymentResource/SpendPower/RunePool/Qiyana/Spellshield/Stack adjacent regression 692/692 passed; PaymentEngineCoverageAuditTests 548/548 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5119/5119 passed; `git diff --check` passed after final doc write.
