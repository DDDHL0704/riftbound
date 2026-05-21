# 4D-03IL-E Card Matrix Readiness Payment-Cost Time Gate Play Equipment FAQ Blocker Closure Candidate

4D-03IL-E records a single row-level E_CARD_MATRIX_READINESS blocker reduction for the FAQ-mentioned Time Gate play-equipment representative row. The selected functional unit is `FU-081d97eb3e`; selected card is `SFD·078/221` 预时之门; selected effect is `TIME_GATE_PLAY_EQUIPMENT`.

This candidate uses existing service-authoritative evidence only:

- `tests/Riftbound.ConformanceTests/TimeGateGuardTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-time-gate-equipment.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-time-gate-target-rejected.fixture.json`
- `docs/CURRENT_STAGE4C_BATCH42_TIME_GATE_PLAY_GUARD_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH42_TIME_GATE_PLAY_GUARD_EVIDENCE.md`
- `docs/p2-rules-preflight.md`
- `docs/rules-evidence-index.md`

Prevalidation passed: TimeGate/Equipment/PlayEquipment focused regression 354/354 and ActionPrompt/Prompt/HideCard/RevealCard/TimeGate focused regression 294/294.

Final validation passed: jq empty passed; TimeGate/Equipment/PlayEquipment focused regression 357/357 passed; ActionPrompt/Prompt/HideCard/RevealCard/TimeGate focused regression 297/297 passed; PaymentEngineCoverageAuditTests 458/458 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5029/5029 passed; git diff --check passed.

Matrix impact:

- payment-cost functional units remain `360`.
- payment-cost snapshot entries remain `446`.
- payment-cost `NEEDS_ENGINE_SUPPORT` moves `281 -> 280`.
- primary payment-cost `NEEDS_ENGINE_SUPPORT` remains `177 -> 177`.
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` moves `469 -> 468`.
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` moves `179 -> 178`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` remains `328`.
- `NEEDS_FAQ_REVIEW` remains `92`.
- primary `NEEDS_FAQ_REVIEW` remains `61`.
- `fullOfficialTrue` remains `0`.
- `ready` remains `false`.

Non-closure:

- Time Gate automated evidence disposition remains open.
- Time Gate FAQ adjudication remains open.
- Time Gate activated / tap ability remains open.
- Time Gate next spell Echo semantics remain open.
- Time Gate optional echo payment / repeat remains open.
- Time Gate duration cleanup remains open.
- Equipment exhaust / readiness lifecycle remains open.
- Complete FEPR target / stack lifecycle matrix remains open.
- Full PaymentEngine / PAY_COST matrix remains open.
- Card matrix and READY remain open.

This slice does not change runtime, frontend, Chrome scripts, formal 18-step scripts, official catalog data, protocol core fields, `fullOfficial`, final readiness, or `riftbound-dotnet.sln`.
