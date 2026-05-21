# 4D-03ID-E Card Matrix Readiness Payment-Cost Jinx Discard-Two-Hand Blocker Closure Candidate

4D-03ID-E records a single row-level E_CARD_MATRIX_READINESS blocker reduction for the Jinx discard-two-hand shared-oracle representative row. The selected functional unit is `FU-61ed447290`; selected cards are `OGN·030/298` and `OGN·030a/298` 金克丝; selected effects are `JINX_PLAY_UNIT_DISCARD_TWO_HAND` and `JINX_ALT_A_PLAY_UNIT_DISCARD_TWO_HAND`.

This candidate uses existing service-authoritative evidence only:

- `docs/rules-evidence-index.md`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-jinx-discard-two-hand.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-jinx-alt-a-discard-two-hand.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-jinx-haste-ready.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-jinx-alt-a-haste-ready.fixture.json`

Matrix impact:

- payment-cost functional units remain `360`.
- payment-cost snapshot entries remain `446`.
- payment-cost `NEEDS_ENGINE_SUPPORT` moves `289 -> 288`.
- primary payment-cost `NEEDS_ENGINE_SUPPORT` remains `181`.
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` moves `477 -> 476`.
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` moves `187 -> 186`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` remains `328`.
- `NEEDS_FAQ_REVIEW` remains `92`.
- `fullOfficialTrue` remains `0`.
- `ready` remains `false`.

Non-closure:

- Jinx automated evidence disposition remains open.
- Complete hidden-info / random-zone visibility matrix remains open.
- Complete discard / draw replacement / deck exhaustion matrix remains open.
- Complete FEPR target / stack lifecycle matrix remains open.
- Complete battle / spell-duel lifecycle matrix remains open.
- Full PaymentEngine / PAY_COST matrix remains open.
- Card matrix and READY remain open.

This slice does not change runtime, frontend, Chrome scripts, formal 18-step scripts, official catalog data, protocol core fields, `fullOfficial`, final readiness, or `riftbound-dotnet.sln`.
