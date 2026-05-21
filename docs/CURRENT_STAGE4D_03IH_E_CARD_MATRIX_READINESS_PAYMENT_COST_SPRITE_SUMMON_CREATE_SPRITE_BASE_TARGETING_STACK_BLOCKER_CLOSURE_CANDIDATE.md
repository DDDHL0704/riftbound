# 4D-03IH-E Card Matrix Readiness Payment-Cost Sprite Summon Create-Sprite-Base Blocker Closure Candidate

4D-03IH-E records a single row-level E_CARD_MATRIX_READINESS blocker reduction for the Sprite Summon create-sprite-base representative row. The selected functional unit is `FU-4387fcb7c3`; selected card is `OGN·094/298` 精灵召唤; selected effect is `SPRITE_SUMMON_CREATE_SPRITE_BASE`.

This candidate uses existing service-authoritative evidence only:

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-sprite-summon-create-sprite-base.fixture.json`
- `docs/p2-rules-preflight.md`
- `docs/rules-evidence-index.md`

Matrix impact:

- payment-cost functional units remain `360`.
- payment-cost snapshot entries remain `446`.
- payment-cost `NEEDS_ENGINE_SUPPORT` moves `285 -> 284`.
- primary payment-cost `NEEDS_ENGINE_SUPPORT` moves `179 -> 178`.
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` moves `473 -> 472`.
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` moves `183 -> 182`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` remains `328`.
- `NEEDS_FAQ_REVIEW` remains `92`.
- `fullOfficialTrue` remains `0`.
- `ready` remains `false`.

Non-closure:

- Sprite Summon automated evidence disposition remains open.
- Complete Ephemeral / cleanup replacement duration matrix remains open.
- Complete destination / control-zone movement matrix remains open.
- Complete hidden-info / random-zone visibility matrix remains open.
- Complete battle / spell-duel lifecycle matrix remains open.
- Complete FEPR target / stack lifecycle matrix remains open.
- Full PaymentEngine / PAY_COST matrix remains open.
- Card matrix and READY remain open.

This slice does not change runtime, frontend, Chrome scripts, formal 18-step scripts, official catalog data, protocol core fields, `fullOfficial`, final readiness, or `riftbound-dotnet.sln`.
