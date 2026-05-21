# 4D-03SC..03SG E_CARD_MATRIX_READINESS Implemented-Tested Evidence Bundle Candidate

Status: candidate prepared on `DOC_MATRIX_CURRENT`; project remains **NOT READY**.

## Scope

This bundle is matrix/current-docs plus `PaymentEngineCoverageAuditTests.cs` baseline synchronization only. It does not modify server runtime, frontend runtime, protocol core fields, official catalog data, browser/Chrome scripts, formal 18-step E2E scripts, `fullOfficial`, final readiness flags or `riftbound-dotnet.sln`.

The shared-board authorization used for this candidate is the standing `2026-05-22 01:07 A_MAIN` no-idle scope plus the current user continuation after the `2026-05-22 01:46 DOC_MATRIX` pause entry. The batch only selects current-matrix rows where `stage4B.freezeStatus=IMPLEMENTED_TESTED`, `stage4B.fullOfficialBlockers` contains `NEEDS_ENGINE_SUPPORT`, and representative automated/runtime-window evidence already exists.

## Selected Rows

| Stage | functionalUnit | cardId | collectorId | Card | Effect / oracle | Evidence basis |
|---|---|---:|---|---|---|---|
| 4D-03SC-E | `FU-64a7f67581` | 31504 | `OGN·262/298` | 天顶之刃 | `ZENITH_BLADE_STUN_ENEMY_BATTLEFIELD_UNIT_NO_MOVE` | Existing representative evidence: `p2-preflight-play-zenith-blade-stun-enemy-battlefield-unit.fixture.json`, `p4-play-zenith-blade-base-unit-target-rejected.fixture.json`, `p4-play-zenith-blade-friendly-target-rejected.fixture.json`, `ConformanceFixtureRunnerTests`, `rules-evidence-index.md`, `CURRENT_SERVER_RULE_AUDIT.md`. |
| 4D-03SD-E | `FU-6c99fc0e2e` | 31521 | `OGN·277/298` | 后巷酒吧 | `BATTLEFIELD_RULE_DOMAIN` | Existing representative evidence: `P79BattlefieldMovedUnitGainsTemporaryPower`, `P79BattlefieldMovedUnitPowerSkipsOpponentControlledSource`, `P79BattlefieldMovePowerSeedMovesUnitAndAppliesBonus`, `CURRENT_P7_9_STATUS.md`, `CURRENT_SERVER_RULE_AUDIT.md`, `CURRENT_CARD_EFFECT_STAGE3B_BATTLEFIELD_LIFECYCLE_EVIDENCE.md`. |
| 4D-03SE-E | `FU-90673ef9fd` | 31530 | `OGN·285/298` | 劫掠船巷 | `BATTLEFIELD_RULE_DOMAIN` | Existing representative evidence: Stage 4C-73 Plunder Alley battlefield defend move-to-base route in `CURRENT_SERVER_RULE_AUDIT.md`, `rules-evidence-index.md`, focused Plunder Alley regression and adjacent battlefield regressions recorded there. |
| 4D-03SF-E | `FU-fda6183f9d` | 31586 | `OGS·007/024` | 盖伦 | `GAREN_PLAY_KEYWORD_UNIT` | Existing representative evidence: `p2-preflight-play-garen-keyword-unit.fixture.json`, `p4-declare-battle-single-combatants.fixture.json`, `ConformanceFixtureRunnerTests`, `rules-evidence-index.md`, `CURRENT_P4_STATUS.md`, battle smoke references in `CURRENT_FRONTEND_REBUILD_PLAN.md`. |
| 4D-03SG-E | `FU-265c03a141` | 31587 | `OGS·008/024` | 绅士决斗 | `GENTLEMAN_DUEL_POWER_PLUS_3_THEN_MUTUAL_POWER_DAMAGE` | Existing representative evidence: `p2-preflight-play-gentleman-duel-power-then-mutual-damage.fixture.json`, `ConformanceFixtureRunnerTests`, Stage 4C-75 Gentleman Duel audit/evidence in `CURRENT_SERVER_RULE_AUDIT.md`, `rules-evidence-index.md`. |

## Blocker Disposition

| Stage | Closed in this bundle | Still open after this bundle |
|---|---|---|
| 4D-03SC-E | `NEEDS_ENGINE_SUPPORT` | Full official breadth remains open; optional move branch, battle/cleanup/control/target-stack breadth remain representative-only. |
| 4D-03SD-E | `NEEDS_ENGINE_SUPPORT` | `NEEDS_FAQ_REVIEW`; battlefield non-play-domain, cleanup and control-zone breadth remain representative-only. |
| 4D-03SE-E | `NEEDS_ENGINE_SUPPORT` | `NEEDS_FAQ_REVIEW`; battlefield FAQ, battle/control/target-stack breadth remain representative-only. |
| 4D-03SF-E | `NEEDS_ENGINE_SUPPORT` | Full official breadth remains open; combat keyword and full battle lifecycle breadth remain representative-only. |
| 4D-03SG-E | `NEEDS_ENGINE_SUPPORT` | Full official breadth remains open; mutual damage, cleanup/layer and target-stack breadth remain representative-only. |

## Count Movement

| Metric | Before | After |
|---|---:|---:|
| all functional units `NEEDS_ENGINE_SUPPORT` | 428 | 423 |
| implemented-tested evidence residual | 13 | 8 |
| payment-cost `NEEDS_ENGINE_SUPPORT` | 35 | 35 |
| targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 203 | 200 |
| cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT` | 164 | 161 |
| hidden-info-random-zone `NEEDS_ENGINE_SUPPORT` | 144 | 144 |
| payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 217 | 214 |
| payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 21 | 21 |
| payment-cost `NEEDS_AUTOMATED_TEST_EVIDENCE` | 328 | 328 |
| payment-cost `NEEDS_FAQ_REVIEW` | 92 | 92 |
| primary FAQ residual | 61 | 61 |
| `fullOfficialTrue` | 0 | 0 |
| `ready` | false | false |

## Developer-Window Gaps

- FAQ / official adjudication remains required before 后巷酒吧 and 劫掠船巷 can lose `NEEDS_FAQ_REVIEW`.
- Full official coverage remains open for all five selected rows; this candidate does not claim exhaustive battle, cleanup, control-zone, layer, target-stack, frontend, browser, formal E2E or 1009/811 readiness coverage.
- Any future reduction of automated-evidence, FAQ, hidden-info, payment-cost or fullOfficial blockers must be handled in a separately authorized batch with matching runtime/test/rules evidence.

## Validation

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- `git diff --check` passed.
- conflict-marker scan over `docs` and `tests` was clean.
- matrix count script passed with all FU `NEEDS_ENGINE_SUPPORT=423`, implemented-tested evidence residual `8`, payment-cost `35`, targeting-stack-timing `200`, cleanup-replacement-duration `161`, hidden-info-random-zone `144`, payment-or-targeting-stack-timing `214`, payment-and-targeting-stack-timing `21`, `fullOfficialTrue=0`, `ready=false`.
- PaymentEngineCoverageAuditTests `697/697` passed.
- ConformanceFixtureRunnerTests `3019/3019` passed.
- backend full `5344/5344` passed.

Frontend build / Chrome smoke are not part of this candidate because this bundle changes no frontend/runtime/browser asset or script.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- `git diff --check`: passed.
- conflict-marker scan over `docs` and `tests`: clean.
- matrix count script: passed; current counts are total functional units `811`, snapshot entries `1009`, `NEEDS_ENGINE_SUPPORT=423`, payment-cost `35`, targeting-stack-timing `200`, cleanup-replacement-duration `161`, hidden-info-random-zone `144`, payment-or-targeting-stack-timing `214`, payment-and-targeting-stack-timing `21`, payment-cost automated evidence `328`, payment-cost FAQ review `92`, primary FAQ residual `61`, `fullOfficialTrue=0`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"`: passed, `697/697`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed, `3019/3019`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed, `5344/5344`.
- Frontend build / Chrome smoke were skipped because this bundle changes no frontend/runtime/browser asset or script.

## Why Not Ready

This bundle reduces only row-level `NEEDS_ENGINE_SUPPORT` where existing representative evidence is already present. FAQ review, full official breadth, frontend/browser/formal E2E and final readiness remain open.
