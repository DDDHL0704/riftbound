# 4D-03IR-E Card Matrix Readiness Payment-Cost Plucky Poro Keyword Unit FAQ Blocker Closure Candidate

4D-03IR-E records a single row-level E_CARD_MATRIX_READINESS blocker reduction for the FAQ-mentioned Plucky Poro keyword-unit representative row. The selected functional unit is `FU-600f595b31`; selected card is `OGN·013/298` 呸呸魄罗; selected effect is `PLUCKY_PORO_PLAY_KEYWORD_UNIT`.

This candidate uses existing service-authoritative evidence only:

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-plucky-poro-keyword-unit.fixture.json`
- `docs/CURRENT_P2_STATUS.md`
- `docs/p2-rules-preflight.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_STAGE4D_03IQ_E_CARD_MATRIX_READINESS_PAYMENT_COST_CAITLYN_SHARED_ORACLE_FAQ_BLOCKER_CLOSURE_CANDIDATE.md`
- `docs/CURRENT_STAGE4D_03IQ_E_CARD_MATRIX_READINESS_PAYMENT_COST_CAITLYN_SHARED_ORACLE_FAQ_BLOCKER_CLOSURE_CANDIDATE_AUDIT.md`

Prevalidation passed: `Plucky|Poro|KeywordUnit` focused regression 133/133 and `ActionPrompt|Prompt|Plucky|Poro|KeywordUnit` adjacent regression 336/336.

Final validation passed: jq empty passed; Plucky/Poro/KeywordUnit focused regression 136/136 passed; ActionPrompt/Prompt/Plucky/Poro/KeywordUnit adjacent regression 339/339 passed; PaymentEngineCoverageAuditTests 470/470 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5041/5041 passed; git diff --check passed.

Matrix impact:

- payment-cost functional units remain `360`.
- payment-cost snapshot entries remain `446`.
- payment-cost `NEEDS_ENGINE_SUPPORT` moves `275 -> 274`.
- primary payment-cost `NEEDS_ENGINE_SUPPORT` remains `177 -> 177`.
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` moves `463 -> 462`.
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` moves `176 -> 175`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` remains `328`.
- `NEEDS_FAQ_REVIEW` remains `92`.
- primary `NEEDS_FAQ_REVIEW` remains `61`.
- `fullOfficialTrue` remains `0`.
- `ready` remains `false`.

Non-closure:

- Plucky Poro automated evidence disposition remains open.
- Plucky Poro FAQ adjudication remains open.
- Complete Spellshield target tax matrix remains open.
- Complete Poro keyword family breadth remains open.
- Complete battle / spell-duel lifecycle matrix remains open.
- Complete FEPR target / stack lifecycle matrix remains open.
- Full PaymentEngine / PAY_COST matrix remains open.
- Card matrix and READY remain open.

This slice does not change runtime, frontend, Chrome scripts, formal 18-step scripts, official catalog data, protocol core fields, `fullOfficial`, final readiness, or `riftbound-dotnet.sln`.
