# 4D-03JP-E Card Matrix Readiness Payment-Cost Malzahar Resource Skill FAQ Targeting Stack Blocker Closure Candidate

## Scope

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-0f7cbe26ce / OGN·113/298 / 玛尔扎哈 / OGN_MALZAHAR_TAP_RUNE_GAIN_PLAY_UNIT`.

It is a matrix, evidence and audit sync only. It does not change runtime, frontend, Chrome / browser scripts, protocol core fields, official catalog, fullOfficial status, final readiness status, or `riftbound-dotnet.sln`.

## Evidence

- Existing implementation: `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs` binds `OGN·113/298` to the Malzahar payment-only resource skill, and `src/Riftbound.Engine/CoreRuleEngine.cs` resolves the target-as-cost destruction, source exhaust, generated payment-only power and lifecycle cleanup.
- Existing prompt/runtime surface: `src/Riftbound.Engine/MatchSession.cs` exposes the authoritative open-main and spell-duel focus prompt metadata, eligible source and cost targets, generated power and resource restriction.
- Existing automated evidence: `tests/Riftbound.ConformanceTests/MalzaharResourceSkillTests.cs` covers prompt visibility, command resolution, temporary payment-only power, invalid source / target / timing rollback and payment misuse guards.
- Additional automated evidence: `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs` and `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` keep the Malzahar play-unit baseline and payment-engine bridge covered.
- Fixture evidence: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ogn-malzahar-tap-rune-static.fixture.json`.
- Rules/evidence index: `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, `docs/CURRENT_STAGE4D_03I_PAYMENT_ENGINE_RESOURCE_SKILL_AUDIT.md` and `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_AUDIT.md` record the accepted representative evidence and remaining FAQ/timing breadth.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT` payment-cost functional units: `251 -> 250`.
- Primary `NEEDS_ENGINE_SUPPORT` residual remains `170`, because the selected row's primary `freezeStatus` remains `NEEDS_FAQ_REVIEW`.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `439 -> 438`.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `159 -> 158`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` residual remains `328`.
- `NEEDS_FAQ_REVIEW` residual remains `92`.
- Primary FAQ residual remains `61`.
- `fullOfficialTrue=0`.
- `ready=false`.

## Non-Closure

This candidate does not close Malzahar automated evidence disposition, FAQ adjudication, full spell-duel / reaction timing official breadth, complete target-as-cost / generated payment-only resource matrix, complete cleanup / replacement / duration matrix, complete FEPR target / stack lifecycle matrix, full PaymentEngine / PAY_COST matrix, full official matrix, formal 18-step E2E, or READY.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MalzaharResourceSkillTests|FullyQualifiedName~Malzahar|FullyQualifiedName~PaymentEngineUnificationTests"` passed: 70/70.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~Malzahar|FullyQualifiedName~ActivateAbility|FullyQualifiedName~Target|FullyQualifiedName~Stack"` passed: 1872/1872.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"` passed: 518/518.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed: 5089/5089.
- `git diff --check` passed after final doc write.
- Chrome smoke was not run for 03JP because this candidate made no frontend or browser-script changes.
