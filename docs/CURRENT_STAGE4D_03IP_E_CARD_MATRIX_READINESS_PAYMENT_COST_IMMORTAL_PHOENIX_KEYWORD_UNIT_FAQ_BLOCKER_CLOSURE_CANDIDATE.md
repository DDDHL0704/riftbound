# 4D-03IP-E Card Matrix Readiness Payment-Cost Immortal Phoenix Keyword Unit FAQ Blocker Closure Candidate

4D-03IP-E records a single row-level E_CARD_MATRIX_READINESS blocker reduction for the FAQ-mentioned Immortal Phoenix keyword-unit representative row. The selected functional unit is `FU-fb6b5139e7`; selected card is `OGN·037/298` 不朽凤凰; selected effect is `IMMORTAL_PHOENIX_PLAY_KEYWORD_UNIT`.

This candidate uses existing service-authoritative evidence only:

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-immortal-phoenix-keyword-unit.fixture.json`
- `docs/CURRENT_P2_STATUS.md`
- `docs/p2-rules-preflight.md`
- `docs/rules-evidence-index.md`

Prevalidation passed: `ImmortalPhoenix|Phoenix|KeywordUnit` focused regression 86/86 and `ActionPrompt|Prompt|ImmortalPhoenix|Phoenix|KeywordUnit` adjacent regression 297/297.

Final validation passed: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed; `ImmortalPhoenix|Phoenix|KeywordUnit` focused regression 89/89 passed; `ActionPrompt|Prompt|ImmortalPhoenix|Phoenix|KeywordUnit` adjacent regression 300/300 passed; `PaymentEngineCoverageAuditTests` 466/466 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5037/5037 passed; `git diff --check` passed.

Matrix impact:

- payment-cost functional units remain `360`.
- payment-cost snapshot entries remain `446`.
- payment-cost `NEEDS_ENGINE_SUPPORT` moves `277 -> 276`.
- primary payment-cost `NEEDS_ENGINE_SUPPORT` remains `177 -> 177`.
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` moves `465 -> 464`.
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` moves `177 -> 176`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` remains `328`.
- `NEEDS_FAQ_REVIEW` remains `92`.
- primary `NEEDS_FAQ_REVIEW` remains `61`.
- `fullOfficialTrue` remains `0`.
- `ready` remains `false`.

Non-closure:

- Immortal Phoenix automated evidence disposition remains open.
- Immortal Phoenix FAQ adjudication remains open.
- Complete discard/recycle play-from-discard breadth remains open.
- Complete Assault battle damage modifier matrix remains open.
- Complete Spirit / hidden-info / random-zone visibility matrix remains open.
- Complete cleanup / replacement duration interactions remain open.
- Complete FEPR target / stack lifecycle matrix remains open.
- Full PaymentEngine / PAY_COST matrix remains open.
- Card matrix and READY remain open.

This slice does not change runtime, frontend, Chrome scripts, formal 18-step scripts, official catalog data, protocol core fields, `fullOfficial`, final readiness, or `riftbound-dotnet.sln`.
