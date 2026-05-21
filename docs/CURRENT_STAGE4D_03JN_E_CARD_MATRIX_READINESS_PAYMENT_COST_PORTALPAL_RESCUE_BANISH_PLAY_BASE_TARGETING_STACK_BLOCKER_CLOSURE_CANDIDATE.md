# 4D-03JN-E Card Matrix Readiness Payment-Cost Portalpal Rescue Banish Play-Base Targeting-Stack Blocker Closure Candidate

## Scope

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-289695c5bf / OGN·102/298 / 传送门大营救 / PORTALPAL_RESCUE_BANISH_FRIENDLY_UNIT_PLAY_TO_BASE`.

It is a matrix, evidence and audit sync only. It does not change runtime, frontend, Chrome / browser scripts, protocol core fields, official catalog, fullOfficial status, final readiness status, or `riftbound-dotnet.sln`.

## Evidence

- Existing implementation: `src/Riftbound.Engine/CardBehaviorRegistry.cs` maps `PORTALPAL_RESCUE_BANISH_FRIENDLY_UNIT_PLAY_TO_BASE` as direct card behavior for `OGN·102/298`.
- Existing runtime surface: `src/Riftbound.Engine/CoreRuleEngine.cs` and `src/Riftbound.Engine/MatchSession.cs` handle the authoritative play prompt, payment, friendly unit target, stack resolution, banish, play-to-base, damage / until-end-of-turn cleanup, and server snapshot.
- Existing automated evidence: `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` covers `CoreRuleEnginePlaysPortalpalRescueBanishPlayBase`, `CoreRuleEngineRejectsPortalpalRescueAgainstEnemyUnit`, and `CoreRuleEnginePortalpalRescueResolutionSkipsOpponentControlledFriendlyZoneTarget`.
- Fixture evidence: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-portalpal-rescue-banish-play-base.fixture.json`.
- Rules/evidence index: `docs/rules-evidence-index.md` and `docs/p2-rules-preflight.md` record the audited friendly-unit banish then play-to-base path and enemy-target rejection.
- Current-state evidence: `docs/CURRENT_FRONTEND_REBUILD_PLAN.md` and `docs/CURRENT_SERVER_RULE_AUDIT.md` record the later Portalpal Rescue / Arcane Shift / Hunting Rhythm dirty-control guard regression.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT` payment-cost functional units: `253 -> 252`.
- Primary `NEEDS_ENGINE_SUPPORT` residual: `172 -> 171`.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `441 -> 440`.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `160 -> 159`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` residual remains `328`.
- `NEEDS_FAQ_REVIEW` residual remains `92`.
- Primary FAQ residual remains `61`.
- `fullOfficialTrue=0`.
- `ready=false`.

## Non-Closure

This candidate does not close Portalpal Rescue automated evidence disposition, complete banish / play-to-base official breadth, battle / spell-duel timing breadth, control-zone movement breadth, complete FEPR target / stack lifecycle matrix, full PaymentEngine / PAY_COST matrix, full official matrix, formal 18-step E2E, or READY.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- `PortalpalRescue|Banish|PlayCard` focused regression passed: `165/165`.
- `ActionPrompt|Prompt|PaymentResource|SpendPower|RunePool|PortalpalRescue|Banish|Target|Stack` adjacent regression passed: `1828/1828`.
- `PaymentEngineCoverageAuditTests` passed: `514/514`.
- Backend full test passed: `dotnet test Riftbound.slnx --no-restore`, `5085/5085`.
- `git diff --check` passed.
- Chrome smoke was not run for 03JN because this batch did not change frontend or browser-script files.
