# 4D-03SC..03SG Candidate Audit

Status: audit prepared on `DOC_MATRIX_CURRENT`; project remains **NOT READY**.

## Authorization And Write Scope

This batch stays under the A_MAIN no-idle implemented-tested evidence scope and the current user continuation after the 03RX-03SB pause. It edits only matrix/current docs and `PaymentEngineCoverageAuditTests.cs` baseline synchronization.

Allowed writes used by this audit:

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- this candidate / audit document pair
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` baseline synchronization only

No server runtime, frontend runtime, protocol core fields, official catalog data, browser/Chrome scripts, formal E2E scripts, `fullOfficial`, final readiness flags or `riftbound-dotnet.sln` are modified.

## Evidence Audit

| Row | Existing implementation / evidence | Audit note |
|---|---|---|
| `FU-64a7f67581` / `OGN·262/298` / 天顶之刃 | `ZENITH_BLADE_STUN_ENEMY_BATTLEFIELD_UNIT_NO_MOVE`; fixtures `p2-preflight-play-zenith-blade-stun-enemy-battlefield-unit`, `p4-play-zenith-blade-base-unit-target-rejected`, `p4-play-zenith-blade-friendly-target-rejected`; `rules-evidence-index.md`; `CURRENT_SERVER_RULE_AUDIT.md`. | Can remove only `NEEDS_ENGINE_SUPPORT`; stun, optional move, battle, cleanup, control and target-stack breadth remain open. |
| `FU-6c99fc0e2e` / `OGN·277/298` / 后巷酒吧 | `BATTLEFIELD_RULE_DOMAIN`; runtime constants and Core path already exist; tests `P79BattlefieldMovedUnitGainsTemporaryPower`, `P79BattlefieldMovedUnitPowerSkipsOpponentControlledSource`, `P79BattlefieldMovePowerSeedMovesUnitAndAppliesBonus`; `CURRENT_P7_9_STATUS.md`; `CURRENT_SERVER_RULE_AUDIT.md`; Stage3B evidence doc. | Can remove only `NEEDS_ENGINE_SUPPORT`; `NEEDS_FAQ_REVIEW` and battlefield official breadth remain open. |
| `FU-90673ef9fd` / `OGN·285/298` / 劫掠船巷 | `BATTLEFIELD_RULE_DOMAIN`; Stage 4C-73 Plunder Alley battlefield route in `CURRENT_SERVER_RULE_AUDIT.md` and `rules-evidence-index.md`; focused and adjacent battlefield regressions recorded in the evidence index. | Can remove only `NEEDS_ENGINE_SUPPORT`; `NEEDS_FAQ_REVIEW`, battle/control and target-stack breadth remain open. |
| `FU-fda6183f9d` / `OGS·007/024` / 盖伦 | `GAREN_PLAY_KEYWORD_UNIT`; fixtures `p2-preflight-play-garen-keyword-unit` and `p4-declare-battle-single-combatants`; `rules-evidence-index.md`; `CURRENT_P4_STATUS.md`; battle smoke records in `CURRENT_FRONTEND_REBUILD_PLAN.md`. | Can remove only `NEEDS_ENGINE_SUPPORT`; battle keyword-unit breadth remains open. |
| `FU-265c03a141` / `OGS·008/024` / 绅士决斗 | `GENTLEMAN_DUEL_POWER_PLUS_3_THEN_MUTUAL_POWER_DAMAGE`; fixture `p2-preflight-play-gentleman-duel-power-then-mutual-damage`; Stage 4C-75 audit/evidence in `CURRENT_SERVER_RULE_AUDIT.md`; `rules-evidence-index.md`. | Can remove only `NEEDS_ENGINE_SUPPORT`; mutual damage, battle, cleanup, layer and target-stack breadth remain open. |

## Matrix Transition Audit

- 4D-03SC-E `FU-64a7f67581`: `stage4B.statusFlags ["IMPLEMENTED_TESTED","NEEDS_ENGINE_SUPPORT"] -> ["IMPLEMENTED_TESTED"]`; `stage4B.fullOfficialBlockers ["NEEDS_ENGINE_SUPPORT"] -> []`; `fullOfficial=false` unchanged.
- 4D-03SD-E `FU-6c99fc0e2e`: `stage4B.statusFlags ["IMPLEMENTED_TESTED","NEEDS_ENGINE_SUPPORT","NEEDS_FAQ_REVIEW"] -> ["IMPLEMENTED_TESTED","NEEDS_FAQ_REVIEW"]`; `stage4B.fullOfficialBlockers ["NEEDS_ENGINE_SUPPORT","NEEDS_FAQ_REVIEW"] -> ["NEEDS_FAQ_REVIEW"]`; `fullOfficial=false` unchanged.
- 4D-03SE-E `FU-90673ef9fd`: `stage4B.statusFlags ["IMPLEMENTED_TESTED","NEEDS_ENGINE_SUPPORT","NEEDS_FAQ_REVIEW"] -> ["IMPLEMENTED_TESTED","NEEDS_FAQ_REVIEW"]`; `stage4B.fullOfficialBlockers ["NEEDS_ENGINE_SUPPORT","NEEDS_FAQ_REVIEW"] -> ["NEEDS_FAQ_REVIEW"]`; `fullOfficial=false` unchanged.
- 4D-03SF-E `FU-fda6183f9d`: `stage4B.statusFlags ["IMPLEMENTED_TESTED","NEEDS_ENGINE_SUPPORT"] -> ["IMPLEMENTED_TESTED"]`; `stage4B.fullOfficialBlockers ["NEEDS_ENGINE_SUPPORT"] -> []`; `fullOfficial=false` unchanged.
- 4D-03SG-E `FU-265c03a141`: `stage4B.statusFlags ["IMPLEMENTED_TESTED","NEEDS_ENGINE_SUPPORT"] -> ["IMPLEMENTED_TESTED"]`; `stage4B.fullOfficialBlockers ["NEEDS_ENGINE_SUPPORT"] -> []`; `fullOfficial=false` unchanged.

## Count Audit

- all functional units `NEEDS_ENGINE_SUPPORT`: 428 -> 423
- implemented-tested evidence residual: 13 -> 8
- payment-cost `NEEDS_ENGINE_SUPPORT`: remains 35
- targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 203 -> 200
- cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT`: 164 -> 161
- hidden-info-random-zone `NEEDS_ENGINE_SUPPORT`: remains 144
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 217 -> 214
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: remains 21
- payment-cost `NEEDS_AUTOMATED_TEST_EVIDENCE`: remains 328
- payment-cost `NEEDS_FAQ_REVIEW`: remains 92
- primary FAQ residual: remains 61
- `fullOfficialTrue`: remains 0
- `ready`: remains false

## Required Development Follow-Up

- FAQ/evidence lane must decide whether 后巷酒吧 and 劫掠船巷 still need FAQ blockers after official FAQ review.
- Runtime/test lane must cover any breadth that is still only representative: optional move, complete battlefield lifecycle, cleanup/replacement-duration, control-zone movement, layer/continuous-effect, complete battle damage and target-stack behavior.
- Final-gate lane must rerun frontend/browser/formal E2E and full 1009/811 readiness checks before any final readiness claim.

## Validation Audit

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- `git diff --check` passed.
- conflict-marker scan over `docs` and `tests` was clean.
- matrix count script passed with all FU `NEEDS_ENGINE_SUPPORT=423`, implemented-tested evidence residual `8`, payment-cost `35`, targeting-stack-timing `200`, cleanup-replacement-duration `161`, hidden-info-random-zone `144`, payment-or-targeting-stack-timing `214`, payment-and-targeting-stack-timing `21`, `fullOfficialTrue=0`, `ready=false`.
- PaymentEngineCoverageAuditTests `697/697` passed.
- ConformanceFixtureRunnerTests `3019/3019` passed.
- backend full `5344/5344` passed.

Frontend build / Chrome smoke are intentionally skipped for this candidate because no frontend/browser files, server runtime or protocol behavior are changed.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- `git diff --check`: passed.
- conflict-marker scan over `docs` and `tests`: clean.
- matrix count script: passed; current counts are total functional units `811`, snapshot entries `1009`, `NEEDS_ENGINE_SUPPORT=423`, payment-cost `35`, targeting-stack-timing `200`, cleanup-replacement-duration `161`, hidden-info-random-zone `144`, payment-or-targeting-stack-timing `214`, payment-and-targeting-stack-timing `21`, payment-cost automated evidence `328`, payment-cost FAQ review `92`, primary FAQ residual `61`, `fullOfficialTrue=0`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"`: passed, `697/697`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed, `3019/3019`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed, `5344/5344`.
- Frontend build / Chrome smoke were skipped because this candidate changes no frontend/browser files, server runtime or protocol behavior.

## Closure Judgment

This is acceptable only as row-level `NEEDS_ENGINE_SUPPORT` evidence synchronization. FAQ closure, full official breadth, frontend/browser/formal E2E and final readiness remain open.
