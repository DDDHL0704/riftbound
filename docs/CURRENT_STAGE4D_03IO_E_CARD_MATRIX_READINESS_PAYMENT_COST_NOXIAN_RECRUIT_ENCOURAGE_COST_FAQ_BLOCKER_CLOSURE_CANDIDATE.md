# 4D-03IO-E Card Matrix Readiness Payment-Cost Noxian Recruit Encourage Cost FAQ Blocker Closure Candidate

4D-03IO-E records a single row-level E_CARD_MATRIX_READINESS blocker reduction for the FAQ-mentioned Noxian Recruit encourage-cost representative row. The selected functional unit is `FU-5d9c4aaa91`; selected card is `OGN·012/298` 诺克萨斯新兵; selected effect is `NOXIAN_RECRUIT_NO_ENCOURAGE_TRIFARIAN_PLAY_UNIT`.

This candidate uses existing service-authoritative evidence only:

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-noxian-recruit-no-encourage-trifarian-unit.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-noxian-recruit-encourage-cost-reduction.fixture.json`
- `docs/CURRENT_P4_STATUS.md`
- `docs/p2-rules-preflight.md`
- `docs/rules-evidence-index.md`

Prevalidation passed: `NoxianRecruit|Encourage|CostReduction` focused regression 24/24 and `ActionPrompt|Prompt|NoxianRecruit|Encourage` adjacent regression 222/222.

Final validation passed: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed; `NoxianRecruit|Encourage|CostReduction` focused regression 24/24 passed; `ActionPrompt|Prompt|NoxianRecruit|Encourage` adjacent regression 222/222 passed; `PaymentEngineCoverageAuditTests` 464/464 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5035/5035 passed; `git diff --check` passed.

Matrix impact:

- payment-cost functional units remain `360`.
- payment-cost snapshot entries remain `446`.
- payment-cost `NEEDS_ENGINE_SUPPORT` moves `278 -> 277`.
- primary payment-cost `NEEDS_ENGINE_SUPPORT` remains `177 -> 177`.
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` moves `466 -> 465`.
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` remains `177 -> 177`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` remains `328`.
- `NEEDS_FAQ_REVIEW` remains `92`.
- primary `NEEDS_FAQ_REVIEW` remains `61`.
- `fullOfficialTrue` remains `0`.
- `ready` remains `false`.

Non-closure:

- Noxian Recruit automated evidence disposition remains open.
- Noxian Recruit FAQ adjudication remains open.
- Complete encourage-family breadth remains open.
- Cleanup / replacement duration interactions remain open.
- No-prior-card and turn-memory breadth remains open.
- Complete FEPR target / stack lifecycle matrix remains open.
- Full PaymentEngine / PAY_COST matrix remains open.
- Card matrix and READY remain open.

This slice does not change runtime, frontend, Chrome scripts, formal 18-step scripts, official catalog data, protocol core fields, `fullOfficial`, final readiness, or `riftbound-dotnet.sln`.
