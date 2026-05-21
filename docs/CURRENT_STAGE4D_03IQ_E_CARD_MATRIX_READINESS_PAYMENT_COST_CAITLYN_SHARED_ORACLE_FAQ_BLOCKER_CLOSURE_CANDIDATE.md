# 4D-03IQ-E Card Matrix Readiness Payment-Cost Caitlyn Shared Oracle FAQ Blocker Closure Candidate

4D-03IQ-E records a single row-level E_CARD_MATRIX_READINESS blocker reduction for the FAQ-mentioned Caitlyn shared-oracle representative row. The selected functional unit is `FU-0c888a9e66`; selected cards are `ARC-002/006` and `OGN·068/298` 凯特琳; selected effects are `ARC_CAITLYN_DAMAGE_ORDER_STATIC_PLAY_UNIT;OGN_CAITLYN_BACK_ROW_DAMAGE_SKILL_PLAY_UNIT`.

This candidate uses existing service-authoritative evidence only:

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-arc-caitlyn-damage-order-static.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ogn-caitlyn-back-row-static.fixture.json`
- `docs/CURRENT_P2_STATUS.md`
- `docs/p2-rules-preflight.md`
- `docs/rules-evidence-index.md`

Prevalidation passed: `Caitlyn|BackRow|DamageOrder` focused regression 4/4 and `ActionPrompt|Prompt|Caitlyn|BackRow|DamageOrder` adjacent regression 215/215.

Final validation passed: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed; `Caitlyn|BackRow|DamageOrder` focused regression 7/7 passed; `ActionPrompt|Prompt|Caitlyn|BackRow|DamageOrder` adjacent regression 218/218 passed; `PaymentEngineCoverageAuditTests` 468/468 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5039/5039 passed; `git diff --check` passed.

Matrix impact:

- payment-cost functional units remain `360`.
- payment-cost snapshot entries remain `446`.
- payment-cost `NEEDS_ENGINE_SUPPORT` moves `276 -> 275`.
- primary payment-cost `NEEDS_ENGINE_SUPPORT` remains `177 -> 177`.
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` moves `464 -> 463`.
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` remains `176 -> 176`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` remains `328`.
- `NEEDS_FAQ_REVIEW` remains `92`.
- primary `NEEDS_FAQ_REVIEW` remains `61`.
- `fullOfficialTrue` remains `0`.
- `ready` remains `false`.

Non-closure:

- Caitlyn automated evidence disposition remains open.
- Caitlyn FAQ adjudication remains open.
- Complete combat damage ordering remains open.
- Complete Back Row damage assignment / protection matrix remains open.
- Complete tap damage skill lifecycle remains open.
- Complete battle / spell-duel lifecycle matrix remains open.
- Complete LayerEngine / continuous-effect matrix remains open.
- Complete FEPR target / stack lifecycle matrix remains open.
- Full PaymentEngine / PAY_COST matrix remains open.
- Card matrix and READY remain open.

This slice does not change runtime, frontend, Chrome scripts, formal 18-step scripts, official catalog data, protocol core fields, `fullOfficial`, final readiness, or `riftbound-dotnet.sln`.
