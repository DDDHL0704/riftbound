# 4D-03IG-E Card Matrix Readiness Payment-Cost Ekko No-Optional-Haste Blocker Closure Candidate

4D-03IG-E records a single row-level E_CARD_MATRIX_READINESS blocker reduction for the Ekko no-optional-haste representative row. The selected functional unit is `FU-e72a65ab4c`; selected card is `OGN·110/298` 艾克; selected effect is `EKKO_PLAY_UNIT_NO_OPTIONAL_HASTE`.

This candidate uses existing service-authoritative evidence only:

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ekko-no-optional-haste.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-ekko-haste-ready.fixture.json`

Matrix impact:

- payment-cost functional units remain `360`.
- payment-cost snapshot entries remain `446`.
- payment-cost `NEEDS_ENGINE_SUPPORT` moves `286 -> 285`.
- primary payment-cost `NEEDS_ENGINE_SUPPORT` moves `180 -> 179`.
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` moves `474 -> 473`.
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` moves `184 -> 183`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` remains `328`.
- `NEEDS_FAQ_REVIEW` remains `92`.
- `fullOfficialTrue` remains `0`.
- `ready` remains `false`.

Non-closure:

- Ekko automated evidence disposition remains open.
- Complete Haste / ready-exhausted timing matrix remains open.
- Complete cleanup / replacement duration matrix remains open.
- Complete control / movement matrix remains open.
- Complete FEPR target / stack lifecycle matrix remains open.
- Full PaymentEngine / PAY_COST matrix remains open.
- Card matrix and READY remain open.

This slice does not change runtime, frontend, Chrome scripts, formal 18-step scripts, official catalog data, protocol core fields, `fullOfficial`, final readiness, or `riftbound-dotnet.sln`.
