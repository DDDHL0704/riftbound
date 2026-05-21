# 4D-03JU-E Card Matrix Readiness Payment-Cost Wildclaw Shaman No-Boon-Consume Layer Targeting Stack Blocker Closure Candidate

## Scope

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-b55baa6b03 / OGN·147/298 / 野爪萨满 / WILDCLAW_SHAMAN_NO_BOON_CONSUME_PLAY_UNIT`.

It is a matrix, evidence and audit sync only. It does not change runtime, frontend, Chrome / browser scripts, protocol core fields, official catalog, fullOfficial status, final readiness status, or `riftbound-dotnet.sln`.

## Evidence

- Existing implementation: `src/Riftbound.Engine/CardBehaviorRegistry.cs` binds `OGN·147/298` to `WILDCLAW_SHAMAN_NO_BOON_CONSUME_PLAY_UNIT` with the fixed base-cost and source-to-base unit creation metadata.
- Existing automated evidence: `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` covers the no-boon-consume baseline path that pays 4, requires no target selection, resolves the stack, and creates the 3-power unit in base.
- Fixture evidence: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-wildclaw-shaman-no-boon-consume-static.fixture.json`.
- Rules/evidence index: `docs/rules-evidence-index.md` and `docs/p2-rules-preflight.md` record the accepted representative evidence and keep the optional boon-consume self-buff / ready branch open.
- P2 status evidence: `docs/CURRENT_P2_STATUS.md` records adjacent P2 preflight unit-play evidence for this representative path.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT` payment-cost functional units: `246 -> 245`.
- Primary `NEEDS_ENGINE_SUPPORT` residual: `167 -> 166`, because the selected row's primary `freezeStatus` moves to `IMPLEMENTED_UNTESTED`.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `434 -> 433`.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `154 -> 153`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` residual remains `328`.
- `NEEDS_FAQ_REVIEW` residual remains `92`.
- Primary FAQ residual remains `61`.
- `fullOfficialTrue=0`.
- `ready=false`.

## Non-Closure

This candidate does not close Wildclaw Shaman automated evidence disposition, optional boon-consume self-buff / ready branch, complete layer / continuous-effect breadth, complete FEPR target / stack lifecycle matrix, full PaymentEngine / PAY_COST matrix, full official matrix, formal 18-step E2E, or READY.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- Wildclaw / NoBoonConsume / PlayUnit / ConformanceFixtureRunnerTests focused regression passed: 3042/3042.
- ActionPrompt / Prompt / PaymentResource / SpendPower / RunePool / Wildclaw / PlayCard / Stack adjacent regression passed: 740/740.
- `PaymentEngineCoverageAuditTests` passed: 528/528.
- Backend full `dotnet test Riftbound.slnx --no-restore` passed: 5099/5099.
- `git diff --check` passed after final doc write.
