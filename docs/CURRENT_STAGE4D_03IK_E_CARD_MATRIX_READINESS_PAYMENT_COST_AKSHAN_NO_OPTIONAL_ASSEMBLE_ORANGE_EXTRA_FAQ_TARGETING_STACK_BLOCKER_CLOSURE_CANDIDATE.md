# 4D-03IK-E Card Matrix Readiness Payment-Cost Akshan No Optional Assemble Orange Extra FAQ Blocker Closure Candidate

4D-03IK-E records a single row-level E_CARD_MATRIX_READINESS blocker reduction for the FAQ-mentioned Akshan no-optional-assemble / orange-extra representative row. The selected functional unit is `FU-7419ee7d9d`; selected card is `SFD·109/221` 阿克尚; selected effect is `AKSHAN_NO_OPTIONAL_ASSEMBLE_NO_EXTRA_PLAY_UNIT`.

This candidate uses existing service-authoritative evidence only:

- `tests/Riftbound.ConformanceTests/AkshanGuardTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-akshan-no-optional-assemble-no-orange-extra.fixture.json`
- `docs/CURRENT_STAGE4C_BATCH44_AKSHAN_PLAY_GUARD_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH44_AKSHAN_PLAY_GUARD_EVIDENCE.md`
- `docs/CURRENT_STAGE4D_04F_AKSHAN_ORANGE_EXTRA_EQUIPMENT_STEAL_AUDIT.md`
- `docs/CURRENT_STAGE4D_04F_AKSHAN_ORANGE_EXTRA_EQUIPMENT_STEAL_EVIDENCE.md`
- `docs/p2-rules-preflight.md`
- `docs/rules-evidence-index.md`

Prevalidation passed: Akshan/PlayUnit/KeywordUnit/Assemble focused regression 246/246 and ActionPrompt/Prompt/HideCard/RevealCard/Akshan focused regression 310/310. Final validation passed: jq empty passed; Akshan/PlayUnit/KeywordUnit/Assemble focused regression 248/248 passed; ActionPrompt/Prompt/HideCard/RevealCard/Akshan focused regression 313/313 passed; PaymentEngineCoverageAuditTests 456/456 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5027/5027 passed; git diff --check passed.

Matrix impact:

- payment-cost functional units remain `360`.
- payment-cost snapshot entries remain `446`.
- payment-cost `NEEDS_ENGINE_SUPPORT` moves `282 -> 281`.
- primary payment-cost `NEEDS_ENGINE_SUPPORT` remains `177 -> 177`.
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` moves `470 -> 469`.
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` moves `180 -> 179`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` remains `328`.
- `NEEDS_FAQ_REVIEW` remains `92`.
- primary `NEEDS_FAQ_REVIEW` remains `61`.
- `fullOfficialTrue` remains `0`.
- `ready` remains `false`.

Non-closure:

- Akshan automated evidence disposition remains open.
- Akshan FAQ adjudication remains open.
- Complete optional assemble matrix remains open.
- Complete orange-extra enemy equipment move / control matrix remains open.
- Complete weapon attach and control-until-leaves cleanup matrix remains open.
- Complete LayerEngine / continuous effects matrix remains open.
- Complete movement / control-zone matrix remains open.
- Complete FEPR target / stack lifecycle matrix remains open.
- Full PaymentEngine / PAY_COST matrix remains open.
- Card matrix and READY remain open.

This slice does not change runtime, frontend, Chrome scripts, formal 18-step scripts, official catalog data, protocol core fields, `fullOfficial`, final readiness, or `riftbound-dotnet.sln`.
