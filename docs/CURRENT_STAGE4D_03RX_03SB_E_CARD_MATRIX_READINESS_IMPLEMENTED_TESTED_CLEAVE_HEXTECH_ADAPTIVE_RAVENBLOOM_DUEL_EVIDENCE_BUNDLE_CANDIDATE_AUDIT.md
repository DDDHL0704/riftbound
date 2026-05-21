# 4D-03RX..03SB Candidate Audit

Status: audit prepared on `DOC_MATRIX_CURRENT`; project remains **NOT READY**.

## Authorization And Write Scope

The latest shared-board `A_MAIN` entry authorizes `DOC_MATRIX_CURRENT` to keep the docs window active on implemented-tested current-matrix rows that still carry `NEEDS_ENGINE_SUPPORT` and already have representative automated/runtime-window evidence. This batch stays inside that matrix / current-doc / audit-test-baseline synchronization lane.

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
| `FU-44f29ad8f7` / `OGN·004/298` / 顺劈 | `CLEAVE_OVERWHELM_3`; `rules-evidence-index.md` entries for `p2-preflight-play-cleave-overwhelm-attacking-power` and `p4-play-swift-cleave-in-spell-duel-focus`; `CURRENT_P4_STATUS.md`; fixture coverage in `ConformanceFixtureRunnerTests`. | Can remove only `NEEDS_ENGINE_SUPPORT`; full battle, cleanup, layer and target-stack breadth remains open. |
| `FU-441cb9fb7f` / `OGN·009/298` / 海克斯射线 | `HEXTECH_RAY_DAMAGE_3`; Stage 4C-72 representative evidence in `CURRENT_SERVER_RULE_AUDIT.md`; fixture coverage for damage stack, end-turn cleanup and swift spell-duel focus in `ConformanceFixtureRunnerTests`; `rules-evidence-index.md` evidence lines. | Can remove only `NEEDS_ENGINE_SUPPORT`; FAQ refs remain `NEEDS_FAQ_REVIEW`. |
| `FU-7f4a387b92` / `OGN·056/298` / 自适应机器人 | `ADAPTIVE_ROBOT_CONQUER_BOON_PLAY_UNIT`; `rules-evidence-index.md` representative conquer/boon evidence; `CURRENT_FRONTEND_REBUILD_PLAN.md`; fixture coverage in `ConformanceFixtureRunnerTests`. | Can remove only `NEEDS_ENGINE_SUPPORT`; battle, cleanup, layer and target-stack breadth remains open. |
| `FU-bf81341dd2` / `OGN·103/298` / 拉文布鲁姆学生 | `RAVENBLOOM_STUDENT_SPELL_TRIGGER_PLAY_UNIT`; `rules-evidence-index.md` spell-trigger / spell-power / target-rejected evidence; `CURRENT_P4_STATUS.md`; fixture coverage in `ConformanceFixtureRunnerTests`. | Can remove only `NEEDS_ENGINE_SUPPORT`; FAQ refs remain `NEEDS_FAQ_REVIEW`. |
| `FU-2779c06158` / `OGN·128/298` / 决斗 | `DUEL_MUTUAL_POWER_DAMAGE`; Stage 4C-82 representative evidence in `CURRENT_SERVER_RULE_AUDIT.md`; `rules-evidence-index.md`; mutual damage and reversed target-order rejection fixtures in `ConformanceFixtureRunnerTests`; `CURRENT_P4_STATUS.md`. | Can remove only `NEEDS_ENGINE_SUPPORT`; battle, layer and target-stack breadth remains open. |

## Matrix Transition Audit

- 4D-03RX-E `FU-44f29ad8f7`: `stage4B.statusFlags ["IMPLEMENTED_TESTED","NEEDS_ENGINE_SUPPORT"] -> ["IMPLEMENTED_TESTED"]`; `stage4B.fullOfficialBlockers ["NEEDS_ENGINE_SUPPORT"] -> []`; `fullOfficial=false` unchanged.
- 4D-03RY-E `FU-441cb9fb7f`: `stage4B.statusFlags ["IMPLEMENTED_TESTED","NEEDS_ENGINE_SUPPORT","NEEDS_FAQ_REVIEW"] -> ["IMPLEMENTED_TESTED","NEEDS_FAQ_REVIEW"]`; `stage4B.fullOfficialBlockers ["NEEDS_ENGINE_SUPPORT","NEEDS_FAQ_REVIEW"] -> ["NEEDS_FAQ_REVIEW"]`; `fullOfficial=false` unchanged.
- 4D-03RZ-E `FU-7f4a387b92`: `stage4B.statusFlags ["IMPLEMENTED_TESTED","NEEDS_ENGINE_SUPPORT"] -> ["IMPLEMENTED_TESTED"]`; `stage4B.fullOfficialBlockers ["NEEDS_ENGINE_SUPPORT"] -> []`; `fullOfficial=false` unchanged.
- 4D-03SA-E `FU-bf81341dd2`: `stage4B.statusFlags ["IMPLEMENTED_TESTED","NEEDS_ENGINE_SUPPORT","NEEDS_FAQ_REVIEW"] -> ["IMPLEMENTED_TESTED","NEEDS_FAQ_REVIEW"]`; `stage4B.fullOfficialBlockers ["NEEDS_ENGINE_SUPPORT","NEEDS_FAQ_REVIEW"] -> ["NEEDS_FAQ_REVIEW"]`; `fullOfficial=false` unchanged.
- 4D-03SB-E `FU-2779c06158`: `stage4B.statusFlags ["IMPLEMENTED_TESTED","NEEDS_ENGINE_SUPPORT"] -> ["IMPLEMENTED_TESTED"]`; `stage4B.fullOfficialBlockers ["NEEDS_ENGINE_SUPPORT"] -> []`; `fullOfficial=false` unchanged.

## Count Audit

- all functional units `NEEDS_ENGINE_SUPPORT`: 433 -> 428
- implemented-tested evidence residual: 18 -> 13
- payment-cost `NEEDS_ENGINE_SUPPORT`: remains 35
- targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 208 -> 203
- cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT`: 167 -> 164
- hidden-info-random-zone `NEEDS_ENGINE_SUPPORT`: remains 144
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 222 -> 217
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: remains 21
- payment-cost `NEEDS_AUTOMATED_TEST_EVIDENCE`: remains 328
- payment-cost `NEEDS_FAQ_REVIEW`: remains 92
- primary FAQ residual: remains 61
- `fullOfficialTrue`: remains 0
- `ready`: remains false

## Required Development Follow-Up

- A future FAQ/evidence lane must decide whether 海克斯射线 and 拉文布鲁姆学生 still need FAQ blockers after official FAQ review.
- A future runtime/test lane must cover any breadth that is still only representative: complete battle/spell-duel lifecycle, cleanup/replacement-duration, layer/continuous-effect and target-stack behavior.
- A future final-gate lane must rerun frontend/browser/formal E2E and full 1009/811 readiness checks before any final readiness claim.

## Validation Plan

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `git diff --check`
- conflict-marker scan over `docs` and `tests`
- matrix count script over the current JSON
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`

Frontend build / Chrome smoke are intentionally skipped for this candidate because no frontend/browser files, server runtime or protocol behavior are changed.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- `git diff --check`: passed.
- conflict-marker scan over `docs` and `tests`: clean.
- matrix count script: passed; current counts are total functional units `811`, snapshot entries `1009`, `NEEDS_ENGINE_SUPPORT=428`, payment-cost `35`, targeting-stack-timing `203`, cleanup-replacement-duration `164`, hidden-info-random-zone `144`, payment-or-targeting-stack-timing `217`, payment-and-targeting-stack-timing `21`, payment-cost automated evidence `328`, payment-cost FAQ review `92`, primary FAQ residual `61`, `fullOfficialTrue=0`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"`: passed, `697/697`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed, `3019/3019`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed, `5344/5344`.
- Frontend build / Chrome smoke were skipped because this candidate changes no frontend/browser files, server runtime or protocol behavior.

## Closure Judgment

This is acceptable only as row-level `NEEDS_ENGINE_SUPPORT` evidence synchronization. FAQ closure, full official breadth, frontend/browser/formal E2E and final readiness remain open.

## Validation Results

Passed on `DOC_MATRIX_CURRENT`:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `git diff --check`
- conflict-marker scan over `docs` and `tests` returned no matches
- matrix count script: all `NEEDS_ENGINE_SUPPORT=428`, implemented-tested evidence residual `13`
- PaymentEngineCoverageAuditTests: 697/697 passed
- ConformanceFixtureRunnerTests: 3019/3019 passed
- backend full test: 5344/5344 passed
