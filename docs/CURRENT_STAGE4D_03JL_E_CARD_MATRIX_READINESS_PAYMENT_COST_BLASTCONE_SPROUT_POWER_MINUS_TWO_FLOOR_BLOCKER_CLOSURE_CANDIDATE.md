# 4D-03JL-E Card Matrix Readiness Payment-Cost Blastcone Sprout Power-Minus-Two Floor Blocker Closure Candidate

## Scope

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-3fd9d79377 / OGN·097/298 / 爆裂球果仙灵 / BLASTCONE_SPROUT_PLAY_UNIT_POWER_MINUS_2_FLOOR`.

It is a matrix, evidence and audit sync only. It does not change runtime, frontend, Chrome / browser scripts, protocol core fields, official catalog, fullOfficial status, final readiness status, or `riftbound-dotnet.sln`.

## Evidence

- Existing implementation: `src/Riftbound.Engine/CardBehaviorRegistry.cs` maps `BLASTCONE_SPROUT_PLAY_UNIT_POWER_MINUS_2_FLOOR` as direct card behavior with `PowerModifierAmount=-2` and `MinimumPowerAfterModifier=1`.
- Existing runtime surface: `src/Riftbound.Engine/CoreRuleEngine.cs` and `src/Riftbound.Engine/MatchSession.cs` handle the authoritative play prompt, target legality, stack resolution, continuous effect layer, requested/applied power delta, and snapshot metadata.
- Existing automated evidence: `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` covers `CoreRuleEnginePlaysBlastconeSproutPowerMinusTwoFloor` and `CoreRuleEngineRejectsBlastconeSproutWhenTargetIsNotUnit`.
- Fixture evidence: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-blastcone-sprout-power-minus-2-floor.fixture.json`.
- Layer evidence: `docs/CURRENT_STAGE4D_04M_LAYERENGINE_MINIMUM_POWER_LEDGER_EVIDENCE.md`, `docs/CURRENT_STAGE4D_04M_LAYERENGINE_MINIMUM_POWER_LEDGER_HANDOFF.md`, and `docs/CURRENT_STAGE4D_04P_LAYERENGINE_MINIMUM_POWER_ORDERING_BASELINE_EVIDENCE.md`.
- Rules reference retained from matrix: `CORE-260330 p97`.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT` payment-cost functional units: `255 -> 254`.
- Primary `NEEDS_ENGINE_SUPPORT` residual: `174 -> 173`.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `443 -> 442`.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `161 -> 160`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` residual remains `328`.
- `NEEDS_FAQ_REVIEW` residual remains `92`.
- Primary FAQ residual remains `61`.
- `fullOfficialTrue=0`.
- `ready=false`.

## Non-Closure

This candidate does not close Blastcone Sprout automated evidence disposition, cleanup / replacement / duration breadth, hidden-info / random-zone breadth, complete minimum-power floor ordering matrix, complete FEPR target / stack lifecycle matrix, full PaymentEngine / PAY_COST matrix, full official matrix, formal 18-step E2E, or READY.
