# 4D-03IJ-E Card Matrix Readiness Payment-Cost Giant Arm Kato Keyword Unit FAQ Blocker Closure Candidate

4D-03IJ-E records a single row-level E_CARD_MATRIX_READINESS blocker reduction for the FAQ-mentioned Giant Arm Kato play-keyword-unit representative row. The selected functional unit is `FU-464ec8c275`; selected card is `SFD·112/221` 巨腕加藤; selected effect is `GIANT_ARM_KATO_PLAY_KEYWORD_UNIT`.

This candidate uses existing service-authoritative evidence only:

- `tests/Riftbound.ConformanceTests/GiantArmKatoGuardTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-giant-arm-kato-keyword-unit.fixture.json`
- `docs/CURRENT_STAGE4C_BATCH41_GIANT_ARM_KATO_PLAY_KEYWORD_GUARD_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH41_GIANT_ARM_KATO_PLAY_KEYWORD_GUARD_EVIDENCE.md`
- `docs/p2-rules-preflight.md`
- `docs/rules-evidence-index.md`

Validation passed: `jq empty`, GiantArmKato/Kato/KeywordUnit focused regression 93/93, ActionPrompt/Prompt/HideCard/RevealCard/GiantArmKato focused regression 294/294, `PaymentEngineCoverageAuditTests` 454/454, backend full `dotnet test Riftbound.slnx --no-restore` 5025/5025, and `git diff --check` passed.

Matrix impact:

- payment-cost functional units remain `360`.
- payment-cost snapshot entries remain `446`.
- payment-cost `NEEDS_ENGINE_SUPPORT` moves `283 -> 282`.
- primary payment-cost `NEEDS_ENGINE_SUPPORT` remains `177 -> 177`.
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` moves `471 -> 470`.
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` moves `181 -> 180`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` remains `328`.
- `NEEDS_FAQ_REVIEW` remains `92`.
- primary `NEEDS_FAQ_REVIEW` remains `61`.
- `fullOfficialTrue` remains `0`.
- `ready` remains `false`.

Non-closure:

- Giant Arm Kato automated evidence disposition remains open.
- Giant Arm Kato FAQ adjudication remains open.
- Complete Spellshield target tax matrix remains open.
- Complete move-to-battlefield trigger matrix remains open.
- Complete friendly-unit choice / prompt and keyword grant matrix remains open.
- Complete +power until EOT / LayerEngine / duration cleanup matrix remains open.
- Complete movement / control-zone matrix remains open.
- Complete FEPR target / stack lifecycle matrix remains open.
- Full PaymentEngine / PAY_COST matrix remains open.
- Card matrix and READY remain open.

This slice does not change runtime, frontend, Chrome scripts, formal 18-step scripts, official catalog data, protocol core fields, `fullOfficial`, final readiness, or `riftbound-dotnet.sln`.
