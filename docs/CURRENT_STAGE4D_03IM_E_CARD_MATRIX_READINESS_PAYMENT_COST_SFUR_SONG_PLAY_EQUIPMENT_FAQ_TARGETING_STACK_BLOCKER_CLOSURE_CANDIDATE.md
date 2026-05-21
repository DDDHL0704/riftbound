# 4D-03IM-E Card Matrix Readiness Payment-Cost Sfur Song Play Equipment FAQ Blocker Closure Candidate

4D-03IM-E records a single row-level E_CARD_MATRIX_READINESS blocker reduction for the FAQ-mentioned Sfur Song play-equipment representative row. The selected functional unit is `FU-9a623b3185`; selected card is `SFD·059/221` 斯弗尔尚歌; selected effect is `SFUR_SONG_PLAY_EQUIPMENT`.

This candidate uses existing service-authoritative evidence only:

- `tests/Riftbound.ConformanceTests/SfurSongGuardTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-sfur-song-equipment.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-sfur-song-target-rejected.fixture.json`
- `docs/CURRENT_STAGE4C_BATCH43_SFUR_SONG_PLAY_GUARD_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH43_SFUR_SONG_PLAY_GUARD_EVIDENCE.md`
- `docs/p2-rules-preflight.md`
- `docs/rules-evidence-index.md`

Prevalidation passed: SfurSong/Equipment/PlayEquipment focused regression 357/357 and ActionPrompt/Prompt/HideCard/RevealCard/SfurSong focused regression 294/294.

Final validation passed: jq empty passed; SfurSong/Equipment/PlayEquipment focused regression 359/359 passed; ActionPrompt/Prompt/HideCard/RevealCard/SfurSong focused regression 297/297 passed; PaymentEngineCoverageAuditTests 460/460 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5031/5031 passed; git diff --check passed.

Matrix impact:

- payment-cost functional units remain `360`.
- payment-cost snapshot entries remain `446`.
- payment-cost `NEEDS_ENGINE_SUPPORT` moves `280 -> 279`.
- primary payment-cost `NEEDS_ENGINE_SUPPORT` remains `177 -> 177`.
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` moves `468 -> 467`.
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` remains `178 -> 178`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` remains `328`.
- `NEEDS_FAQ_REVIEW` remains `92`.
- primary `NEEDS_FAQ_REVIEW` remains `61`.
- `fullOfficialTrue` remains `0`.
- `ready` remains `false`.

Non-closure:

- Sfur Song automated evidence disposition remains open.
- Sfur Song FAQ adjudication remains open.
- Copied host skill text remains open.
- Continuous text / LayerEngine semantics remain open.
- Complete assemble / equipment attach lifecycle remains open.
- Equipment control / zone movement matrix remains open.
- Complete FEPR target / stack lifecycle matrix remains open.
- Full PaymentEngine / PAY_COST matrix remains open.
- Card matrix and READY remain open.

This slice does not change runtime, frontend, Chrome scripts, formal 18-step scripts, official catalog data, protocol core fields, `fullOfficial`, final readiness, or `riftbound-dotnet.sln`.
