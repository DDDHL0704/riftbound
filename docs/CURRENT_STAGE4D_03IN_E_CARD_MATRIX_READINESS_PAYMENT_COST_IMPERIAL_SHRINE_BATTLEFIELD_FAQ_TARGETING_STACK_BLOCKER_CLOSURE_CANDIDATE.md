# 4D-03IN-E Card Matrix Readiness Payment-Cost Imperial Shrine Battlefield FAQ Blocker Closure Candidate

4D-03IN-E records a single row-level E_CARD_MATRIX_READINESS blocker reduction for the FAQ-mentioned Imperial Shrine battlefield representative row. The selected functional unit is `FU-ec31812b00`; selected card is `SFD·207/221` 帝王神坛; selected effect is `BATTLEFIELD_RULE_DOMAIN`.

This candidate uses existing service-authoritative evidence only:

- `docs/CURRENT_STAGE4C_BATCH86_IMPERIAL_SHRINE_CONQUER_SAND_SOLDIER_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH86_IMPERIAL_SHRINE_CONQUER_SAND_SOLDIER_EVIDENCE.md`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
- `docs/rules-evidence-index.md`

Prevalidation passed: `P79BattlefieldConquerSandSoldier` focused regression 3/3 and `BattlefieldConquer` adjacent regression 48/48.

Final validation passed: jq empty passed; P79BattlefieldConquerSandSoldier focused regression 3/3 passed; BattlefieldConquer adjacent regression 48/48 passed; PaymentEngineCoverageAuditTests 462/462 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5033/5033 passed; git diff --check passed.

Matrix impact:

- payment-cost functional units remain `360`.
- payment-cost snapshot entries remain `446`.
- payment-cost `NEEDS_ENGINE_SUPPORT` moves `279 -> 278`.
- primary payment-cost `NEEDS_ENGINE_SUPPORT` remains `177 -> 177`.
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` moves `467 -> 466`.
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` moves `178 -> 177`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` remains `328`.
- `NEEDS_FAQ_REVIEW` remains `92`.
- primary `NEEDS_FAQ_REVIEW` remains `61`.
- `fullOfficialTrue` remains `0`.
- `ready` remains `false`.

Non-closure:

- Imperial Shrine automated evidence disposition remains open.
- Imperial Shrine FAQ adjudication remains open.
- Optional trigger prompt / decline remains open.
- Complete PaymentEngine quote / authorize / commit remains open.
- Complete battlefield / spell-duel / battle lifecycle remains open.
- Multi-battlefield APNAP ordering remains open.
- Hidden-info / redaction matrix remains open.
- Complete FEPR target / stack lifecycle matrix remains open.
- Full PaymentEngine / PAY_COST matrix remains open.
- Card matrix and READY remain open.

This slice does not change runtime, frontend, Chrome scripts, formal 18-step scripts, official catalog data, protocol core fields, `fullOfficial`, final readiness, or `riftbound-dotnet.sln`.
